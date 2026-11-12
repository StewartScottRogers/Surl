using Surl.Content;
using Surl.Dtls;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server for the <c>coaps</c> scheme (ADR-0097 decisions 0 and 6): CoAP over DTLS 1.2
/// (RFC 7252 section 9.1). Each flow is one client endpoint and one DTLS association, handshaken
/// by <see cref="DtlsServerSession"/> as ADR-0087 decides, in the PreSharedKey, RawPublicKey and
/// Certificate modes the <see cref="CoapDtlsCredentials"/> make live; once it completes, every
/// application data record is served as a CoAP message through the same code that serves
/// <c>coap</c>.
/// </summary>
/// <remarks>
/// <para>
/// A datagram before the handshake completes is <see cref="DtlsServerSession"/>'s alone: no CoAP
/// message is read from it, so a plaintext CoAP request gets nothing. A handshake that fails ends
/// the flow with the alert ADR-0087 decision 10 names; an unknown PSK identity ends with
/// <c>decrypt_error</c>, like a wrong key.
/// </para>
/// <para>
/// A peer is authenticated when its handshake used a PSK identity, which is noted as
/// <c>Login accepted: psk &lt;identity&gt;</c>, or presented a client certificate verified against
/// <see cref="CoapDtlsCredentials.ClientCertificateAuthorities"/>; a certificate or RawPublicKey
/// handshake without one encrypts but does not authenticate. Block sizes shrink to what one record
/// carries (ADR-0097 decision 4). At the idle timeout or maximum duration the association is
/// closed with a warning <c>close_notify</c> (ADR-0087 decision 9). It writes no refusals: a flow
/// past a limit gets nothing.
/// </para>
/// </remarks>
public sealed class SecureCoapProtocolServer : IDatagramProtocolServer
{
    /// <summary>
    /// Room left in one record for a block's header and options when the block size is chosen: a
    /// block of <c>16 &lt;&lt; SZX</c> bytes is used only when it and this many bytes fit (ADR-0097 decision 4).
    /// </summary>
    internal const int BlockHeaderRoom = 64;

    /// <summary>How long the <c>close_notify</c> at a limit may take to send.</summary>
    internal static readonly TimeSpan CloseNotifyDeadline = TimeSpan.FromSeconds(1);

    private readonly ContentStore contentStore;
    private readonly CoapAccessRules accessRules;
    private readonly CoapObservations observations;
    private readonly CoapDtlsCredentials credentials;
    private readonly ICoapRandomSource? random;
    private DtlsCookieSecrets? cookieSecrets;

    /// <summary>
    /// Creates a <c>coaps</c> server that serves <paramref name="contentStore"/> under
    /// <paramref name="accessRules"/>, shares <paramref name="observations"/> with every other CoAP
    /// server of the process, and handshakes with <paramref name="credentials"/>, drawing every
    /// random value (not the DTLS handshake's) from each exchange's seedable identifier source.
    /// </summary>
    /// <param name="contentStore">The resources: every visible file of the store.</param>
    /// <param name="accessRules">Who may read and write.</param>
    /// <param name="observations">Every observer of the process (ADR-0097 decision 0).</param>
    /// <param name="credentials">The certificate, the pre-shared keys, or both, and any <c>--cacert</c> anchors.</param>
    /// <exception cref="ArgumentException"><paramref name="credentials"/> has neither a certificate nor pre-shared keys.</exception>
    public SecureCoapProtocolServer(
        ContentStore contentStore, CoapAccessRules accessRules, CoapObservations observations, CoapDtlsCredentials credentials)
        : this(contentStore, accessRules, observations, credentials, random: null, cookieSecrets: null)
    {
    }

    internal SecureCoapProtocolServer(
        ContentStore contentStore,
        CoapAccessRules accessRules,
        CoapObservations observations,
        CoapDtlsCredentials credentials,
        ICoapRandomSource? random,
        DtlsCookieSecrets? cookieSecrets)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(accessRules);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(credentials);
        if (credentials.Certificate is null && credentials.PreSharedKeys is null)
        {
            throw new ArgumentException("A coaps server needs a certificate or pre-shared keys (ADR-0087 decision 3).", nameof(credentials));
        }

        this.contentStore = contentStore;
        this.accessRules = accessRules;
        this.observations = observations;
        this.credentials = credentials;
        this.random = random;
        this.cookieSecrets = cookieSecrets;
    }

    /// <summary>The one scheme answered: <c>coaps</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["coaps"]);

    /// <summary>
    /// Handshakes over <paramref name="flow"/>, then answers every CoAP message the association
    /// carries until the client closes it or the exchange is cancelled, which ends the call with
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram, the client's hello.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        return ServeAssociationAsync(flow, context).Unwrap();
    }

    /// <summary>
    /// The largest block SZX whose block and <see cref="BlockHeaderRoom"/> bytes fit one record of
    /// <paramref name="maximumApplicationDataLength"/> bytes. A record carries at least 512 bytes
    /// (the smallest <c>max_fragment_length</c>, RFC 6066 section 4), so the answer is at least 4.
    /// </summary>
    /// <param name="maximumApplicationDataLength">What one record carries.</param>
    /// <returns>An SZX from 4 to 6.</returns>
    internal static int BlockSizeExponentFor(int maximumApplicationDataLength)
    {
        var sizeExponent = CoapBlockOption.LargestSizeExponent;
        while ((16 << sizeExponent) + BlockHeaderRoom > maximumApplicationDataLength)
        {
            sizeExponent--;
        }

        return sizeExponent;
    }

    // Handshakes, serves the association however it ends, sends close_notify when a limit ended
    // it, and hands serving back so its outcome is the call's: a client's close ends it normally,
    // the exchange's cancellation with OperationCanceledException.
    private async Task<Task> ServeAssociationAsync(IDatagramFlow flow, ExchangeContext context)
    {
        using var session = new DtlsServerSession(flow, OptionsFor(context));
        var outcome = await session.HandshakeAsync(context.CancellationToken);
        if (outcome.Result != DtlsHandshakeResult.Completed)
        {
            context.Log.Note($"The DTLS handshake ended: {outcome.Result}{(outcome.Alert is { } alert ? $" ({alert})" : string.Empty)}.");
            return Task.CompletedTask;
        }

        var peerIsAuthenticated = NotePeer(session, context);
        var firstMessage = await session.ReceiveAsync(context.CancellationToken);
        if (firstMessage is null)
        {
            return NoteAssociationEnded(context);
        }

        // The association's end cancels the flow's serving through this source, apart from the exchange.
        using var associationEnded = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        await using var messages = new CoapDtlsFlow(flow, session, firstMessage, associationEnded);
        var serving = ServeMessagesAsync(
            messages,
            context with { CancellationToken = associationEnded.Token },
            BlockSizeExponentFor(session.MaximumApplicationDataLength),
            peerIsAuthenticated);
        await Task.WhenAny(serving);
        if (!context.CancellationToken.IsCancellationRequested)
        {
            return NoteAssociationEnded(context);
        }

        if (context.IsCancelledForALimit)
        {
            await CloseAtALimitAsync(session, context);
        }

        return serving;
    }

    private Task ServeMessagesAsync(CoapDtlsFlow messages, ExchangeContext context, int blockSizeExponent, bool peerIsAuthenticated)
    {
        var source = random ?? new IdentifierCoapRandomSource(context.Identifiers);
        var answerer = new CoapResourceAnswerer(contentStore, accessRules, source, blockSizeExponent);
        return CoapFlowServing.ServeAsync(messages, context, answerer, contentStore, observations, source, peerIsAuthenticated);
    }

    private static Task NoteAssociationEnded(ExchangeContext context)
    {
        context.Log.Note("The client ended the DTLS association.");
        return Task.CompletedTask;
    }

    // ADR-0087 decision 9: one warning close_notify at the idle timeout or maximum duration, on a
    // short deadline of its own, never on the cancelled exchange's token (ADR-0059).
    private static async Task CloseAtALimitAsync(DtlsServerSession session, ExchangeContext context)
    {
        using var deadline = new CancellationTokenSource(CloseNotifyDeadline, context.TimeProvider);
        await session.CloseAsync(deadline.Token);
    }

    // ADR-0097 decision 6: a PSK identity, or a verified client certificate, authenticates.
    private static bool NotePeer(DtlsServerSession session, ExchangeContext context)
    {
        if (session.ClientPreSharedKeyIdentity is { } identity)
        {
            context.Log.Note(new CheckedLogin("psk", identity, IsAccepted: true).Note);
            return true;
        }

        return session.ClientCertificateVerified;
    }

    private DtlsServerOptions OptionsFor(ExchangeContext context) =>
        new(
            credentials.Certificate,
            LazyInitializer.EnsureInitialized(ref cookieSecrets, () => new DtlsCookieSecrets(context.TimeProvider)),
            context.TimeProvider,
            context.Limits.HeadTimeout,
            (int)Math.Min(context.Limits.MaxMessageBytes, int.MaxValue))
        {
            PreSharedKeys = credentials.PreSharedKeys,
            ClientCertificateAuthorities = credentials.ClientCertificateAuthorities,
        };
}
