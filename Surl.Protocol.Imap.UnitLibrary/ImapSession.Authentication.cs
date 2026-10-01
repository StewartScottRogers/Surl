using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// <c>STARTTLS</c> and <c>AUTHENTICATE</c> (ADR-0055, decisions 10 and 11; ADR-0049, sections 6
/// and 7).
/// </summary>
internal sealed partial class ImapSession
{
    private static readonly string AuthenticateCompleted = ImapResponses.Completed("AUTHENTICATE");

    // The continuations that end the exchange but not the session (ADR-0049, section 7).
    private static readonly Dictionary<SaslContinuationOutcome, string> AbandonedExchangeResponses = new()
    {
        [SaslContinuationOutcome.Cancelled] = ImapResponses.AuthenticationCancelled,
        [SaslContinuationOutcome.NotBase64] = ImapResponses.CannotDecodeResponse,
    };

    // STARTTLS: refused once the connection is TLS, or when the server has no certificate, in
    // which case it was never advertised (ADR-0055, decision 11).
    private async ValueTask<bool> AnswerStartTlsAsync(ImapArguments arguments)
    {
        var refusal = !arguments.IsAtEnd ? ImapResponses.InvalidArguments
            : connection.TlsSession is not null ? ImapResponses.AlreadyUsingTls
            : isTlsUpgradeAvailable ? null
            : ImapResponses.StartTlsNotAvailable;
        return refusal is null ? await UpgradeToTlsAsync() : await ReplyAsync($"{tag} {refusal}");
    }

    // OK, then every byte pipelined after the STARTTLS line thrown away unrun, then the handshake
    // (ADR-0010, section 1). The session stays not authenticated, and the login with no
    // credentials is asked about again with the TLS session. A failed handshake throws
    // TlsHandshakeException, which the engine notes.
    private async ValueTask<bool> UpgradeToTlsAsync()
    {
        await WriteLineAsync($"{tag} {ImapResponses.BeginTls}", CancellationToken);
        var discarded = lineReader.DiscardBuffered();
        if (discarded > 0)
        {
            context.Log.Note($"Discarded {discarded} bytes sent after STARTTLS");
        }

        await connection.UpgradeToTlsAsync(CancellationToken);
        anonymousVerdict = null;
        return true;
    }

    // AUTHENTICATE <mechanism> [<initial response>]: the initial response is base64, "=" for an
    // empty one (RFC 4959); one that cannot be decoded starts no exchange.
    private ValueTask<bool> AnswerAuthenticateAsync(ImapArguments arguments)
    {
        var mechanism = arguments.TryReadSpace() ? arguments.ReadAtom() : null;
        var word = mechanism is null ? null : ReadInitialResponseWord(arguments);
        if (word is null)
        {
            return ReplyAsync($"{tag} {ImapResponses.InvalidArguments}");
        }

        if (word.Length == 0)
        {
            return RunSaslExchangeAsync(mechanism!, null);
        }

        return DecodeInitialResponse(word) is { } initialResponse
            ? RunSaslExchangeAsync(mechanism!, initialResponse)
            : ReplyAsync($"{tag} {ImapResponses.CannotDecodeResponse}");
    }

    // The initial response as sent: empty when there is none; null when the rest of the command
    // is not one space and one word.
    private static string? ReadInitialResponseWord(ImapArguments arguments)
    {
        if (!arguments.TryReadSpace())
        {
            return arguments.IsAtEnd ? string.Empty : null;
        }

        var word = arguments.ReadRun(IsInitialResponseByte);
        return arguments.IsAtEnd ? word : null;
    }

    private static bool IsInitialResponseByte(byte value) => value is > 0x20 and < 0x7F;

    private static byte[]? DecodeInitialResponse(string word) =>
        word == "=" ? [] : SaslContinuationLine.Classify(Encoding.ASCII.GetBytes(word)).Response;

    // The server frames the exchange - "+ " continuations, the client's base64 responses, "*" -
    // and the policy decides every step (ADR-0049, section 6).
    private async ValueTask<bool> RunSaslExchangeAsync(string mechanism, byte[]? initialResponse)
    {
        // A null array, or a bare null beside a memory, converts to an empty memory, which means
        // "=", not "none sent".
        ReadOnlyMemory<byte>? sent = default;
        if (initialResponse is not null)
        {
            sent = initialResponse;
        }

        var exchange = mailAuthenticationPolicy.StartSaslExchange(
            new SaslExchangeStart(context.Scheme, mechanism, sent, connection.TlsSession));
        var step = await exchange.BeginAsync(CancellationToken);
        while (step.Outcome == SaslLoginOutcome.Challenge)
        {
            NoteCheckedLogin(step);
            await WriteLineAsync("+ " + Convert.ToBase64String(step.Challenge.Span), CancellationToken);
            var read = await lineReader.ReadSaslContinuationAsync(CancellationToken);
            if (read.Response is null)
            {
                return await AnswerNoResponseAsync(read.Outcome);
            }

            step = await exchange.ContinueAsync(read.Response, CancellationToken);
        }

        NoteCheckedLogin(step);
        return await ReplyAsync($"{tag} {AcceptSaslLogin(step)}");
    }

    // A cancel or an undecodable response ends the command and the session goes on; a limit or
    // the peer's close ends the session as a command's would (ADR-0055, decision 12).
    private ValueTask<bool> AnswerNoResponseAsync(SaslContinuationOutcome outcome)
    {
        if (AbandonedExchangeResponses.TryGetValue(outcome, out var response))
        {
            return ReplyAsync($"{tag} {response}");
        }

        if (outcome == SaslContinuationOutcome.Closed)
        {
            return ValueTask.FromResult(false);
        }

        var limit = outcome == SaslContinuationOutcome.LineTooLong ? ImapCommandReadOutcome.LineTooLong : ImapCommandReadOutcome.HeadTimedOut;
        return CloseWithAsync(LimitResponse(new ImapCommandReadResult(limit, tag, null)));
    }

    // The note is written before the response (ADR-0038; ADR-0049, section 6).
    private void NoteCheckedLogin(SaslLoginStep step)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }
    }

    // An accepted login opens the account's view, the anonymous owner's when it was not checked;
    // every refusal is in ADR-0049 section 7's IMAP words.
    private string AcceptSaslLogin(SaslLoginStep step)
    {
        if (step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked)
        {
            view = mailStore.ViewFor(step.AccountName);
            return AuthenticateCompleted;
        }

        return step.Outcome switch
        {
            SaslLoginOutcome.RefusedCredentials => ImapResponses.LoginFailed,
            SaslLoginOutcome.RefusedPlaintext => ImapResponses.EncryptionRequired,
            _ => ImapResponses.UnsupportedMechanism,
        };
    }
}
