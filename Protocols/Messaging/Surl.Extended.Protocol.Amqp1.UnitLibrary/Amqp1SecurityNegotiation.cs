using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Negotiates one connection's protocol headers and SASL layer (AMQP 1.0 Part 2 section 2.2 and
/// Part 5; ADR-0107 decisions 1 and 2), up to the point where AMQP frames follow.
/// </summary>
/// <remarks>
/// The container never speaks first: each header the client sends decides the next layer. A SASL
/// header is answered in kind and runs SASL; a plain AMQP header is let through only under
/// <see cref="Amqp1SecurityNegotiationOptions.AllowsAnonymous"/>; a TLS header on a plaintext
/// connection with a certificate is answered in kind and upgraded, and the client starts again
/// inside TLS. Every other header is answered <c>AMQP 3 1 0 0</c> and closed. After a successful
/// <c>sasl-outcome</c> only <c>AMQP 0 1 0 0</c> is accepted; anything else - a further SASL frame
/// included - is answered <c>AMQP 0 1 0 0</c> and closed. SASL frames are at most 512 bytes; a
/// larger one, an AMQP frame, or one out of order closes the connection with no bytes, since the
/// SASL layer has no error frame. Headers and frames are read exactly, never a byte past them, so
/// pipelined bytes are kept for the next layer. A failed TLS handshake throws
/// <see cref="TlsHandshakeException"/>, which the engine notes. One instance serves one connection.
/// </remarks>
/// <param name="policy">Offers the mechanisms and runs each login but <c>ANONYMOUS</c>.</param>
/// <param name="options">What the negotiation allows.</param>
/// <param name="connection">The connection.</param>
/// <param name="context">The exchange's context: its scheme, its log and its cancellation token.</param>
public sealed class Amqp1SecurityNegotiation(
    ISaslAuthenticationPolicy policy,
    Amqp1SecurityNegotiationOptions options,
    IConnection connection,
    ExchangeContext context)
{
    /// <summary>The <c>ANONYMOUS</c> mechanism (RFC 4505), offered under <c>--allow-anonymous</c> only.</summary>
    public const string AnonymousMechanism = "ANONYMOUS";

    /// <summary><c>sasl-code</c> 0, <c>ok</c>.</summary>
    public const byte SaslCodeOk = 0;

    /// <summary><c>sasl-code</c> 1, <c>auth</c>: the login was refused.</summary>
    public const byte SaslCodeAuth = 1;

    private const int ScramServerFinalChallenge = 2;

    private readonly Amqp1FrameReader frameReader = new(connection);

    private CancellationToken CancellationToken => context.CancellationToken;

    /// <summary>Reads the client's headers and runs SASL until AMQP frames follow or the connection is closed.</summary>
    /// <returns>Whether AMQP frames follow, and the account logged in.</returns>
    /// <exception cref="TlsHandshakeException">The TLS handshake after a TLS header failed.</exception>
    public async ValueTask<Amqp1SecurityNegotiationResult> NegotiateAsync()
    {
        var header = await ReadHeaderAsync();
        if (header is null)
        {
            return Amqp1SecurityNegotiationResult.Closed;
        }

        switch (Amqp1ProtocolHeader.Identify(header))
        {
            case Amqp1ProtocolId.Sasl:
                await WriteHeaderAsync(Amqp1ProtocolId.Sasl);
                return await RunSaslAsync();
            case Amqp1ProtocolId.Amqp when options.AllowsAnonymous:
                await WriteHeaderAsync(Amqp1ProtocolId.Amqp);
                return new(true, null);
            case Amqp1ProtocolId.Tls when options.CanUpgradeToTls && connection.TlsSession is null:
                await WriteHeaderAsync(Amqp1ProtocolId.Tls);
                await connection.UpgradeToTlsAsync(CancellationToken);
                return await NegotiateAsync();
            default:
                return await RefuseHeaderAsync(Amqp1ProtocolId.Sasl);
        }
    }

    private async ValueTask<Amqp1SecurityNegotiationResult> RunSaslAsync()
    {
        var offered = OfferedMechanisms();
        await WriteFrameAsync(new Amqp1SaslMechanisms([.. offered.Select(mechanism => new Amqp1Symbol(mechanism))]));

        var init = await ReadSaslPerformativeAsync<Amqp1SaslInit>();
        var login = init is null ? null : await LogInAsync(init, offered);
        return login is null ? await CloseAsync() : await AnswerLoginAsync(login);
    }

    private async ValueTask<Amqp1SecurityNegotiationResult> AnswerLoginAsync(SaslLogin login)
    {
        await WriteFrameAsync(new Amqp1SaslOutcome(login.IsAccepted ? SaslCodeOk : SaslCodeAuth)
        {
            AdditionalData = login.AdditionalData.IsEmpty ? null : login.AdditionalData.ToArray(),
        });
        return login.IsAccepted ? await ExpectAmqpHeaderAsync(login.AccountName) : await CloseAsync();
    }

    private List<string> OfferedMechanisms()
    {
        List<string> offered = [.. policy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession))];
        if (options.AllowsAnonymous && !offered.Contains(AnonymousMechanism))
        {
            offered.Add(AnonymousMechanism);
        }

        return offered;
    }

    // The login's end, or null when the client broke the SASL layer off.
    private async ValueTask<SaslLogin?> LogInAsync(Amqp1SaslInit init, List<string> offered)
    {
        var mechanism = init.Mechanism.Value;
        if (!offered.Contains(mechanism))
        {
            return SaslLogin.Refused;
        }

        if (mechanism == AnonymousMechanism && options.AllowsAnonymous)
        {
            return new SaslLogin(true, null, ReadOnlyMemory<byte>.Empty);
        }

        // Written out, because a bare null would bind through byte[]'s conversion and read as an empty response.
        var initialResponse = init.InitialResponse is { } response ? new ReadOnlyMemory<byte>(response) : (ReadOnlyMemory<byte>?)null;
        var exchange = policy.StartSaslExchange(new SaslExchangeStart(context.Scheme, mechanism, initialResponse, connection.TlsSession));
        var step = await exchange.BeginAsync(CancellationToken);
        Note(step);
        return await FollowChallengesAsync(exchange, step, mechanism);
    }

    private async ValueTask<SaslLogin?> FollowChallengesAsync(ISaslExchange exchange, SaslLoginStep step, string mechanism)
    {
        var challengesIssued = 0;
        while (step.Outcome == SaslLoginOutcome.Challenge)
        {
            challengesIssued++;
            if (IsScramServerFinal(mechanism, challengesIssued))
            {
                return await EndWithScramServerFinalAsync(exchange, step.Challenge);
            }

            await WriteFrameAsync(new Amqp1SaslChallenge(step.Challenge.ToArray()));
            var response = await ReadSaslPerformativeAsync<Amqp1SaslResponse>();
            if (response is null)
            {
                return null;
            }

            step = await exchange.ContinueAsync(response.Response, CancellationToken);
            Note(step);
        }

        return SaslLogin.Ending(step, step.AdditionalSuccessData);
    }

    // SCRAM's server-final message (RFC 5802) is additional data with success, which AMQP carries in
    // sasl-outcome; the policy issues it as a second challenge, as the mail servers need, so it is
    // answered here with the empty response a mail client would send.
    private async ValueTask<SaslLogin> EndWithScramServerFinalAsync(ISaslExchange exchange, ReadOnlyMemory<byte> serverFinal)
    {
        var final = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken);
        Note(final);
        return SaslLogin.Ending(final, serverFinal);
    }

    private static bool IsScramServerFinal(string mechanism, int challengesIssued) =>
        challengesIssued == ScramServerFinalChallenge && mechanism.StartsWith("SCRAM-", StringComparison.Ordinal);

    private async ValueTask<Amqp1SecurityNegotiationResult> ExpectAmqpHeaderAsync(string? accountName)
    {
        var header = await ReadHeaderAsync();
        if (header is null)
        {
            return Amqp1SecurityNegotiationResult.Closed;
        }

        if (Amqp1ProtocolHeader.Identify(header) != Amqp1ProtocolId.Amqp)
        {
            return await RefuseHeaderAsync(Amqp1ProtocolId.Amqp);
        }

        await WriteHeaderAsync(Amqp1ProtocolId.Amqp);
        return new(true, accountName);
    }

    // The performative when the next frame is a SASL frame of 512 bytes at most carrying a T; null otherwise.
    private async ValueTask<T?> ReadSaslPerformativeAsync<T>()
        where T : Amqp1Performative
    {
        var read = await frameReader.ReadFrameAsync(Amqp1FrameLimits.MinMaxFrameSize, CancellationToken);
        if (read.Frame is not { Type: Amqp1FrameType.Sasl } frame)
        {
            return null;
        }

        try
        {
            return Amqp1FrameBody.Read(frame).Performative as T;
        }
        catch (Amqp1DecodeException)
        {
            return null;
        }
    }

    // The 8 header bytes, or null when the client closed before sending them all.
    private async ValueTask<byte[]?> ReadHeaderAsync()
    {
        var header = new byte[Amqp1ProtocolHeader.Bytes];
        var filled = 0;
        while (filled < header.Length)
        {
            var read = await connection.ReadAsync(header.AsMemory(filled), CancellationToken);
            if (read == 0)
            {
                return null;
            }

            filled += read;
        }

        return header;
    }

    private async ValueTask<Amqp1SecurityNegotiationResult> RefuseHeaderAsync(Amqp1ProtocolId answered)
    {
        await WriteHeaderAsync(answered);
        return await CloseAsync();
    }

    private async ValueTask<Amqp1SecurityNegotiationResult> CloseAsync()
    {
        await connection.CompleteWritesAsync(CancellationToken);
        return Amqp1SecurityNegotiationResult.Closed;
    }

    private ValueTask WriteHeaderAsync(Amqp1ProtocolId protocolId) =>
        connection.WriteAsync(Amqp1ProtocolHeader.Of(protocolId), CancellationToken);

    private ValueTask WriteFrameAsync(Amqp1Performative performative) =>
        connection.WriteAsync(Amqp1Frame.Carrying(0, performative).Encode(), CancellationToken);

    private void Note(SaslLoginStep step)
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

    // How a SASL login ended: accepted with its account and any additional data, or refused.
    private sealed record SaslLogin(bool IsAccepted, string? AccountName, ReadOnlyMemory<byte> AdditionalData)
    {
        public static SaslLogin Refused { get; } = new(false, null, ReadOnlyMemory<byte>.Empty);

        public static SaslLogin Ending(SaslLoginStep step, ReadOnlyMemory<byte> additionalData) =>
            step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked
                ? new(true, step.AccountName, additionalData)
                : Refused;
    }
}
