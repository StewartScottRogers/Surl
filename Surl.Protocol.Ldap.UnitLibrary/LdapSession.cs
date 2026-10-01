using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// One LDAP connection's exchange (ADR-0072 decisions 2 to 7): reads each <c>LDAPMessage</c> -
/// inside the SASL security layer once a bind has installed one - answers it in order, upgrades
/// the connection after <c>StartTLS</c>, and ends at an unbind, a close, a Notice of
/// Disconnection or a limit.
/// </summary>
internal sealed class LdapSession
{
    /// <summary>The <c>StartTLS</c> extended operation's OID (RFC 4511, section 4.14).</summary>
    public const string StartTlsName = "1.3.6.1.4.1.1466.20037";

    private const int SecurityLayerLengthBytes = 4;

    // What a search whose base is not the root DSE's hands the directory, which reads it only for the root DSE.
    private static readonly LdapRootDseFacts UnusedRootDseFacts = new([], IsStartTlsOffered: false);

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
    private readonly ISaslAuthenticationPolicy saslAuthenticationPolicy;
    private readonly bool isTlsUpgradeAvailable;
    private readonly LdapBindJudge bindJudge;
    private readonly LdapMessageFrameReader reader;
    private bool isBound;
    private ISaslSecurityLayer? securityLayer;

    public LdapSession(
        IConnection connection,
        ExchangeContext context,
        LdapDirectory directory,
        IAuthenticationPolicy authenticationPolicy,
        ISaslAuthenticationPolicy saslAuthenticationPolicy,
        bool isTlsUpgradeAvailable)
    {
        this.connection = connection;
        this.context = context;
        this.directory = directory;
        this.authenticationPolicy = authenticationPolicy;
        this.saslAuthenticationPolicy = saslAuthenticationPolicy;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;
        bindJudge = new LdapBindJudge(authenticationPolicy, saslAuthenticationPolicy, connection, context);
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

                frame = await ReadNextFrameAsync();
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

    // Inside the security layer each message is one buffer, unprotected before it is decoded
    // (ADR-0072 decision 4).
    private async ValueTask<LdapFrameReadResult> ReadNextFrameAsync()
    {
        if (securityLayer is not { } layer)
        {
            return await reader.ReadFrameAsync(CancellationToken);
        }

        var buffer = await reader.ReadSecurityLayerBufferAsync(layer.MaximumProtectedBytes, CancellationToken);
        if (buffer.Message is not { } protectedBytes)
        {
            return buffer;
        }

        return layer.TryUnprotect(protectedBytes, out var message)
            ? LdapFrameReadResult.Read(message)
            : LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.SecurityLayerRefused);
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
            case LdapFrameReadOutcome.SecurityLayerRefused:
                context.Log.Note("A security-layer buffer failed its check; closed with no reply.");
                break;
            case LdapFrameReadOutcome.MessageTooLarge when securityLayer is not null:
                context.Log.Note(string.Create(
                    CultureInfo.InvariantCulture,
                    $"A security-layer buffer of {frame.AnnouncedBytes} bytes is past what the layer or --max-message allows; closed with no reply."));
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
        _ => AnswerExtendedAsync(messageId, (LdapExtendedRequest)operation),
    };

    // StartTLS where it is recognized; any other extended operation is unknown (ADR-0072 decision 3).
    private ValueTask AnswerExtendedAsync(int messageId, LdapExtendedRequest extended) =>
        extended.RequestName == StartTlsName && IsStartTlsRecognized
            ? AnswerStartTlsAsync(messageId)
            : SendAsync(LdapMessageEncoder.EncodeExtendedResponse(
                messageId, new LdapResult(LdapResultCode.ProtocolError, string.Empty, "unsupported extended operation"), null, null));

    // StartTLS is offered only by a server with a certificate, on a connection not yet TLS
    // (ADR-0072 decision 5); otherwise it is an unknown extended operation, or refused
    // operationsError on a connection already TLS.
    private bool IsStartTlsRecognized => isTlsUpgradeAvailable || connection.TlsSession is not null;

    // A bind resets the connection's identity: a failed bind leaves it anonymous (RFC 4511,
    // section 4.2.1). A security layer starts with the first message after the response, and a
    // later bind's layer replaces it (ADR-0072 decision 4).
    private async ValueTask AnswerBindAsync(int messageId, LdapBindRequest bind)
    {
        isBound = false;
        var answer = await bindJudge.JudgeAsync(bind);
        isBound = answer.IsBound;
        await SendAsync(answer.Encode(messageId));
        if (answer.SecurityLayer is { } layer)
        {
            securityLayer = layer;
            context.Log.Note($"LDAP security layer: {answer.Mechanism}");
        }
    }

    // ADR-0072 decision 5: success, then every byte read past the request thrown away unrun,
    // then the handshake. A failed handshake throws TlsHandshakeException, which the engine notes.
    private async ValueTask AnswerStartTlsAsync(int messageId)
    {
        var refusal = connection.TlsSession is not null ? "TLS is already established"
            : bindJudge.IsBindInProgress ? "a SASL bind is in progress"
            : securityLayer is not null ? "a security layer is installed"
            : null;
        if (refusal is not null)
        {
            context.Log.Note($"LDAP StartTLS refused: {LdapLogText.NameOf(LdapResultCode.OperationsError)}");
            await SendAsync(LdapMessageEncoder.EncodeExtendedResponse(
                messageId, new LdapResult(LdapResultCode.OperationsError, string.Empty, refusal), StartTlsName, null));
            return;
        }

        await SendAsync(LdapMessageEncoder.EncodeExtendedResponse(
            messageId, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty), StartTlsName, null));
        context.Log.Note("LDAP StartTLS accepted");
        var discarded = reader.DiscardReadAhead();
        if (discarded > 0)
        {
            context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"Discarded {discarded} bytes sent after StartTLS"));
        }

        await connection.UpgradeToTlsAsync(CancellationToken);
    }

    private async ValueTask AnswerSearchAsync(int messageId, LdapSearchRequest search)
    {
        var outcome = search.BaseObject.Length == 0 ? directory.Search(search, RootDseFacts())
            : await MayReadAsync() ? directory.Search(search, UnusedRootDseFacts)
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

    // The root DSE's per-connection part, computed afresh for each search, so a StartTLS
    // upgrade changes it (ADR-0072 decisions 1 and 5).
    private LdapRootDseFacts RootDseFacts() => new(
        saslAuthenticationPolicy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession)),
        IsStartTlsOffered: isTlsUpgradeAvailable && connection.TlsSession is null);

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

    private ValueTask SendAsync(byte[] message) => connection.WriteAsync(Protected(message), CancellationToken);

    // Inside the security layer each message goes as one buffer: a 4-byte big-endian length,
    // then the protected bytes (ADR-0072 decision 4).
    private byte[] Protected(byte[] message)
    {
        if (securityLayer is not { } layer)
        {
            return message;
        }

        var protectedBytes = layer.Protect(message);
        var buffer = new byte[SecurityLayerLengthBytes + protectedBytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(buffer, protectedBytes.Length);
        protectedBytes.CopyTo(buffer, SecurityLayerLengthBytes);

        return buffer;
    }

    // Written within the limit-reply deadline, linked to shutdown alone, since the exchange's
    // own token may already be cancelled (ADR-0059 decision 3).
    private async ValueTask SendNoticeOfDisconnectionAsync(LdapResultCode code, string diagnostic)
    {
        context.Log.Note($"LDAP Notice of Disconnection: {LdapLogText.NameOf(code)}: {diagnostic}");
        using var deadline = new CancellationTokenSource(LdapProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            await connection.WriteAsync(Protected(LdapMessageEncoder.EncodeNoticeOfDisconnection(new LdapResult(code, string.Empty, diagnostic))), cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note("The Notice of Disconnection was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
