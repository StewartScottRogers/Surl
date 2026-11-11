using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The SASL negotiation of one stream (RFC 6120 section 6.4; ADR-0114 decision 3): each
/// <c>&lt;auth/&gt;</c>, <c>&lt;response/&gt;</c> and <c>&lt;abort/&gt;</c> answered with a
/// <c>&lt;challenge/&gt;</c>, <c>&lt;success/&gt;</c> or <c>&lt;failure/&gt;</c>, every mechanism
/// run by the policy's <see cref="ISaslExchange"/>. The third failed attempt (any condition but
/// <c>aborted</c>) is answered, then ends the stream with <c>policy-violation</c> (6.4.5).
/// </summary>
/// <param name="policy">The policy that runs every mechanism.</param>
/// <param name="context">The exchange: its scheme, log and cancellation.</param>
/// <param name="randomSource">Where an anonymous login's name comes from.</param>
/// <param name="write">Writes text to the client.</param>
internal sealed class XmppSaslLogin(
    ISaslAuthenticationPolicy policy, ExchangeContext context, IXmppRandomSource randomSource, Func<string, Task> write)
{
    /// <summary>How many failed attempts one stream may make; the last ends it (decision 3).</summary>
    public const int MaxFailures = 3;

    private ISaslExchange? exchange;
    private string domain = string.Empty;
    private int failures;
    private string? firstMessageMechanism;
    private bool authzidRefused;

    /// <summary>The bare JID the stream logged in as once SASL succeeded; <see langword="null"/> until then.</summary>
    public string? BareJid { get; private set; }

    /// <summary>
    /// Answers <c>&lt;auth/&gt;</c> (6.4.2): its mechanism must be one offered, and its text, when
    /// present, base64 or <c>=</c> for an empty initial response.
    /// </summary>
    /// <param name="auth">The element.</param>
    /// <param name="offered">The mechanisms the stream's features offered.</param>
    /// <param name="streamDomain">The stream's domain, of the JID a success gives.</param>
    /// <param name="tlsSession">The connection's TLS session, or <see langword="null"/>.</param>
    /// <returns>A task that completes once the answer is written.</returns>
    /// <exception cref="XmppStreamErrorException">The attempt was the last allowed.</exception>
    public async Task StartAsync(XElement auth, IReadOnlyList<string> offered, string streamDomain, TlsSession? tlsSession)
    {
        exchange = null;
        var mechanism = (string?)auth.Attribute("mechanism");
        if (mechanism is null)
        {
            await FailAsync(XmppSaslFailures.MalformedRequest, "An <auth/> named no mechanism");
            return;
        }

        if (!offered.Contains(mechanism, StringComparer.Ordinal))
        {
            await FailAsync(XmppSaslFailures.InvalidMechanism, "An <auth/> named a mechanism not offered");
            return;
        }

        if (!TryDecode(auth.Value, out var initialResponse))
        {
            await FailAsync(XmppSaslFailures.IncorrectEncoding, "An <auth/>'s initial response was not base64");
            return;
        }

        domain = streamDomain;
        authzidRefused = false;
        firstMessageMechanism = mechanism;
        if (auth.Value.Length != 0)
        {
            initialResponse = CheckFirstMessage(initialResponse);
        }

        var start = new SaslExchangeStart(
            context.Scheme, mechanism, auth.Value.Length == 0 ? null : (ReadOnlyMemory<byte>?)initialResponse, tlsSession, CanCarrySecurityLayer: true);
        exchange = policy.StartSaslExchange(start);
        await AnswerStepAsync(await exchange.BeginAsync(context.CancellationToken));
    }

    /// <summary>
    /// Answers <c>&lt;response/&gt;</c> (6.4.3) with the exchange's next step.
    /// </summary>
    /// <param name="response">The element.</param>
    /// <returns>A task that completes once the answer is written.</returns>
    /// <exception cref="XmppStreamErrorException">No exchange is under way, or the attempt was the last allowed.</exception>
    public async Task ContinueAsync(XElement response)
    {
        var current = exchange ?? throw NoExchange("response");
        if (!TryDecode(response.Value, out var data))
        {
            await FailAsync(XmppSaslFailures.IncorrectEncoding, "A <response/> was not base64");
            return;
        }

        if (firstMessageMechanism is not null)
        {
            data = CheckFirstMessage(data);
        }

        await AnswerStepAsync(await current.ContinueAsync(data, context.CancellationToken));
    }

    // RFC 6120 6.5.6: the authzid of the client's first message, read before the exchange sees it.
    // The exchange still runs, so a refused authzid costs what a wrong password does; its answer is
    // then replaced by invalid-authzid.
    private byte[] CheckFirstMessage(byte[] message)
    {
        var mechanism = firstMessageMechanism;
        firstMessageMechanism = null;
        var forwarded = message;
        var isAcceptable = true;
        if (mechanism == "PLAIN")
        {
            isAcceptable = XmppAuthzid.IsAcceptablePlain(message, domain, randomSource, out forwarded);
        }
        else if (mechanism!.StartsWith("SCRAM-", StringComparison.Ordinal))
        {
            isAcceptable = XmppAuthzid.IsAcceptableScram(message);
        }

        authzidRefused = !isAcceptable;
        return forwarded;
    }

    /// <summary>
    /// Answers <c>&lt;abort/&gt;</c> (6.4.4) with <c>aborted</c>; it is not counted as a failed attempt.
    /// </summary>
    /// <returns>A task that completes once the answer is written.</returns>
    /// <exception cref="XmppStreamErrorException">No exchange is under way.</exception>
    public async Task AbortAsync()
    {
        _ = exchange ?? throw NoExchange("abort");
        exchange = null;
        context.Log.Note("The client aborted the SASL exchange; answered aborted.");
        await write(XmppSaslText.Failure(XmppSaslFailures.Aborted));
    }

    /// <summary>
    /// Answers an <c>&lt;auth/&gt;</c> sent before TLS while TLS is required (5.3.1, 6.5.4); the
    /// stream is kept, and the attempt counted.
    /// </summary>
    /// <returns>A task that completes once the answer is written.</returns>
    /// <exception cref="XmppStreamErrorException">The attempt was the last allowed.</exception>
    public Task RefuseBeforeTlsAsync() =>
        FailAsync(XmppSaslFailures.EncryptionRequired, "A SASL <auth/> arrived before TLS while TLS is required");

    private static XmppStreamErrorException NoExchange(string element) =>
        new(XmppStreamErrors.PolicyViolation, $"A SASL <{element}/> arrived with no exchange under way");

    // RFC 6120 6.4.2: "=" is a response present and empty; no text at all is no response. A
    // <response/> with no text is an empty response.
    private static bool TryDecode(string text, out byte[] data)
    {
        data = [];
        if (text is "" or "=")
        {
            return true;
        }

        var buffer = new byte[text.Length];
        if (!Convert.TryFromBase64String(text, buffer, out var length))
        {
            return false;
        }

        data = buffer[..length];
        return true;
    }

    private async Task AnswerStepAsync(SaslLoginStep step)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }

        if (authzidRefused)
        {
            await FailAsync(XmppSaslFailures.InvalidAuthzid, "The client's authzid is not the account's");
            return;
        }

        switch (step.Outcome)
        {
            case SaslLoginOutcome.Challenge:
                await write(XmppSaslText.Challenge(step.Challenge));
                break;
            case SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked:
                await SucceedAsync(step);
                break;
            default:
                await FailAsync(RefusalCondition(step.Outcome), "The SASL login was refused");
                break;
        }
    }

    private static string RefusalCondition(SaslLoginOutcome outcome) => outcome switch
    {
        SaslLoginOutcome.RefusedPlaintext => XmppSaslFailures.EncryptionRequired,
        SaslLoginOutcome.RefusedMechanism => XmppSaslFailures.InvalidMechanism,
        _ => XmppSaslFailures.NotAuthorized,
    };

    // Decision 3: the account must make a bare JID on the stream's domain, else not-authorized.
    private async Task SucceedAsync(SaslLoginStep step)
    {
        exchange = null;
        var bareJid = XmppJids.BareJid(step.AccountName, domain, randomSource);
        if (bareJid is null)
        {
            await FailAsync(XmppSaslFailures.NotAuthorized, "The account's name cannot be a JID on this stream's domain");
            return;
        }

        BareJid = bareJid;
        context.Log.Note($"Logged in as {bareJid}.");
        await write(XmppSaslText.Success(step.AdditionalSuccessData));
    }

    private async Task FailAsync(string condition, string reason)
    {
        exchange = null;
        failures++;
        context.Log.Note($"{reason}; answered {condition}.");
        await write(XmppSaslText.Failure(condition));
        if (failures >= MaxFailures)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, $"The stream failed SASL {MaxFailures} times");
        }
    }
}
