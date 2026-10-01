using System.Formats.Asn1;
using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// One LDAP connection's exchange (ADR-0072 decisions 2, 3, 6 and 7): reads each
/// <c>LDAPMessage</c>, answers it in order, and ends at an unbind, a close, a Notice of
/// Disconnection or a limit.
/// </summary>
internal sealed class LdapSession
{
    private static readonly LdapRootDseFacts NothingOffered = new([], IsStartTlsOffered: false);

    private static readonly IReadOnlyDictionary<int, string> WriteOperationNames = new Dictionary<int, string>
    {
        [6] = "modify",
        [8] = "add",
        [10] = "delete",
        [12] = "modifyDN",
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly LdapDirectory directory;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly LdapBindJudge bindJudge;
    private readonly LdapMessageFrameReader reader;
    private bool isBound;

    public LdapSession(IConnection connection, ExchangeContext context, LdapDirectory directory, IAuthenticationPolicy authenticationPolicy)
    {
        this.connection = connection;
        this.context = context;
        this.directory = directory;
        this.authenticationPolicy = authenticationPolicy;
        bindJudge = new LdapBindJudge(authenticationPolicy, connection, context);
        reader = new LdapMessageFrameReader(connection, context.Limits.MaxMessageBytes);
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    /// <summary>
    /// Answers every message until the exchange ends. An exchange the engine cancels for a
    /// limit - the idle timeout or the maximum duration, which it does not tell apart (ADR-0059
    /// decision 5) - is answered with the Notice of Disconnection <c>unavailable</c>; one
    /// cancelled at shutdown ends with no farewell.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task RunAsync()
    {
        try
        {
            var frame = await ReadFirstFrameAsync();
            while (frame?.Message is { } message)
            {
                if (!await AnswerAsync(message))
                {
                    return;
                }

                frame = await reader.ReadFrameAsync(CancellationToken);
            }

            await AnswerNoFrameAsync(frame);
        }
        catch (OperationCanceledException) when (context.IsCancelledForALimit)
        {
            await SendNoticeOfDisconnectionAsync(LdapResultCode.Unavailable, "idle timeout or maximum duration");
        }
    }

    // The head timeout runs until the first message is whole; null when it ran out. Later
    // messages are bounded by the engine's idle timeout (ADR-0072 decision 6).
    private async ValueTask<LdapFrameReadResult?> ReadFirstFrameAsync()
    {
        if (context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan)
        {
            return await reader.ReadFrameAsync(CancellationToken);
        }

        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadFrameAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async ValueTask AnswerNoFrameAsync(LdapFrameReadResult? frame)
    {
        switch (frame?.Outcome)
        {
            case null:
                context.Log.Note("The first message was not complete within the head timeout; closed with no reply.");
                break;
            case LdapFrameReadOutcome.ConnectionClosed:
                break;
            case LdapFrameReadOutcome.ConnectionClosedMidMessage:
                context.Log.Note("The client closed the connection part way through a message.");
                break;
            case LdapFrameReadOutcome.MessageTooLarge:
                await SendNoticeOfDisconnectionAsync(
                    LdapResultCode.ProtocolError,
                    string.Create(CultureInfo.InvariantCulture, $"a message of {frame.AnnouncedBytes} bytes is past --max-message {context.Limits.MaxMessageBytes}"));
                break;
            default:
                await SendNoticeOfDisconnectionAsync(LdapResultCode.ProtocolError, LdapDiagnostics.Of(frame.Outcome));
                break;
        }
    }

    // True while the connection stays open for the next message.
    private async ValueTask<bool> AnswerAsync(byte[] bytes)
    {
        var decoded = LdapMessageDecoder.Decode(bytes, LdapProtocolServer.MaxFilterDepth);
        if (decoded.Message is not { } message)
        {
            await SendNoticeOfDisconnectionAsync(LdapResultCode.ProtocolError, LdapDiagnostics.Of(decoded.Outcome));
            return false;
        }

        if (ResponseTagOf(message.Operation) is { } responseTag && message.Controls.Any(control => control.Criticality))
        {
            var refusal = new LdapResult(LdapResultCode.UnavailableCriticalExtension, string.Empty, "critical control not supported");
            await SendAsync(LdapMessageEncoder.EncodeResultResponse(message.MessageId, responseTag, refusal));
            return true;
        }

        return await AnswerOperationAsync(message.MessageId, message.Operation);
    }

    private async ValueTask<bool> AnswerOperationAsync(int messageId, LdapProtocolOperation operation)
    {
        switch (operation)
        {
            case LdapAbandonRequest:
                return true;
            case LdapUnbindRequest:
                return false;
            case LdapUnrecognizedOperation unrecognized:
                return await AnswerUnrecognizedAsync(messageId, unrecognized.Tag);
            default:
                await AnswerRequestAsync(messageId, operation);
                return true;
        }
    }

    // The operations that have a response.
    private ValueTask AnswerRequestAsync(int messageId, LdapProtocolOperation operation) => operation switch
    {
        LdapBindRequest bind => AnswerBindAsync(messageId, bind),
        LdapSearchRequest search => AnswerSearchAsync(messageId, search),
        LdapCompareRequest compare => AnswerCompareAsync(messageId, compare),
        _ => SendAsync(LdapMessageEncoder.EncodeExtendedResponse(
            messageId, new LdapResult(LdapResultCode.ProtocolError, string.Empty, "unsupported extended operation"), null, null)),
    };

    // A bind resets the connection's identity: a failed bind leaves it anonymous (RFC 4511,
    // section 4.2.1).
    private async ValueTask AnswerBindAsync(int messageId, LdapBindRequest bind)
    {
        isBound = false;
        var result = await bindJudge.JudgeAsync(bind);
        isBound = result.ResultCode == LdapResultCode.Success;
        await SendAsync(LdapMessageEncoder.EncodeBindResponse(messageId, result, null));
    }

    private async ValueTask AnswerSearchAsync(int messageId, LdapSearchRequest search)
    {
        var outcome = search.BaseObject.Length == 0 || await MayReadAsync()
            ? directory.Search(search, NothingOffered)
            : new LdapSearchOutcome([], BindFirst());
        foreach (var entry in outcome.Entries)
        {
            await SendAsync(LdapMessageEncoder.EncodeSearchResultEntry(messageId, entry.ObjectName, entry.Attributes));
        }

        await SendAsync(LdapMessageEncoder.EncodeSearchResultDone(messageId, outcome.Result));
        context.Log.Note(
            $"LDAP search {LdapLogText.Render(search.BaseObject)} scope {ScopeNameOf(search.Scope)}: {outcome.Entries.Count} entries, {LdapLogText.NameOf(outcome.Result.ResultCode)}");
    }

    private async ValueTask AnswerCompareAsync(int messageId, LdapCompareRequest compare)
    {
        var result = await MayReadAsync() ? directory.Compare(compare) : BindFirst();
        await SendAsync(LdapMessageEncoder.EncodeResultResponse(messageId, LdapTags.CompareResponse, result));
    }

    // A write is refused, the directory being read-only over LDAP; any other operation ends the
    // connection with the Notice of Disconnection (ADR-0072 decision 3).
    private async ValueTask<bool> AnswerUnrecognizedAsync(int messageId, Asn1Tag tag)
    {
        if (tag.TagClass != TagClass.Application || !WriteOperationNames.TryGetValue(tag.TagValue, out var operationName))
        {
            await SendNoticeOfDisconnectionAsync(LdapResultCode.ProtocolError, "unknown operation");
            return false;
        }

        context.Log.Note($"LDAP {operationName} refused: the directory is read-only");
        var refusal = new LdapResult(LdapResultCode.UnwillingToPerform, string.Empty, "the directory is read-only over LDAP");
        await SendAsync(LdapMessageEncoder.EncodeResultResponse(messageId, new Asn1Tag(TagClass.Application, tag.TagValue + 1, isConstructed: true), refusal));
        return true;
    }

    // A bound connection reads the whole directory; an unbound one only under --allow-anonymous,
    // which the policy says by accepting a login with no credentials unchecked.
    private async ValueTask<bool> MayReadAsync()
    {
        if (isBound)
        {
            return true;
        }

        var anonymous = new PasswordLogin(context.Scheme, null, null, connection.TlsSession);
        return await authenticationPolicy.CheckPasswordLoginAsync(anonymous, CancellationToken) == PasswordLoginVerdict.AcceptedUnchecked;
    }

    // Nothing is learned about the directory before a bind (ADR-0006, section 3).
    private static LdapResult BindFirst() => new(LdapResultCode.InsufficientAccessRights, string.Empty, "bind first");

    private static string ScopeNameOf(LdapSearchScope scope) => scope switch
    {
        LdapSearchScope.BaseObject => "base",
        LdapSearchScope.SingleLevel => "one",
        _ => "sub",
    };

    private static Asn1Tag? ResponseTagOf(LdapProtocolOperation operation) => operation switch
    {
        LdapBindRequest => LdapTags.BindResponse,
        LdapSearchRequest => LdapTags.SearchResultDone,
        LdapCompareRequest => LdapTags.CompareResponse,
        LdapExtendedRequest => LdapTags.ExtendedResponse,
        _ => null,
    };

    private ValueTask SendAsync(byte[] message) => connection.WriteAsync(message, CancellationToken);

    // Written within the limit-reply deadline, linked to shutdown alone, since the exchange's
    // own token may already be cancelled (ADR-0059 decision 3).
    private async ValueTask SendNoticeOfDisconnectionAsync(LdapResultCode code, string diagnostic)
    {
        context.Log.Note($"LDAP Notice of Disconnection: {LdapLogText.NameOf(code)}: {diagnostic}");
        using var deadline = new CancellationTokenSource(LdapProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            await connection.WriteAsync(LdapMessageEncoder.EncodeNoticeOfDisconnection(new LdapResult(code, string.Empty, diagnostic)), cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note("The Notice of Disconnection was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
