using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SSH server upstream curl's <c>scp://</c> and <c>sftp://</c> transfers talk to
/// (ADR-0051). So far it runs the transport layer and user authentication: it exchanges
/// identification lines, sends its <c>SSH_MSG_KEXINIT</c> and agrees the algorithms with the
/// client's, runs the key exchange method, signing the exchange hash with one of its
/// <see cref="SshHostKeySet"/>, exchanges <c>NEWKEYS</c>, and from then on encrypts and
/// authenticates every packet with the cipher and MAC agreed, re-keying when the client or
/// <see cref="SshReExchangeLimits"/> asks. It then answers the <c>ssh-userauth</c> service as
/// <see cref="SshUserAuthentication"/> says, every credential judged by its
/// <see cref="ISshAuthenticationPolicy"/>. After the login it runs the connection protocol as
/// <see cref="SshConnectionProtocol"/> says: <c>session</c> channels, each <c>exec</c> of an SCP
/// command and the <c>sftp</c> subsystem handed to its handler. Given a <see cref="ContentStore"/>,
/// it answers the <c>sftp</c> subsystem's read side as <see cref="SftpSession"/> says; without one,
/// and for SCP until BL-164, both are answered <c>CHANNEL_FAILURE</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Opening.</b> <c>SSH-2.0-surl</c> CR LF is sent as soon as the connection is served. The
/// client's identification line must come first and be an SSH 2.0 one of at most 255 bytes
/// (ADR-0051 decision 1), else <c>DISCONNECT</c> 8. The server's <c>KEXINIT</c> then offers
/// <see cref="SshAlgorithmOffer"/>'s lists, and each algorithm is the first on the client's list
/// the server also offers (RFC 4253, section 7.1); a list with none in common is
/// <c>DISCONNECT</c> 3. Strict key exchange and a wrongly guessed first key exchange packet are
/// handled as <see cref="SshTransportHandshake"/> says.
/// </para>
/// <para>
/// <b>Key exchange.</b> <c>ecdh-sha2-nistp256/384/521</c>, <c>diffie-hellman-group14-sha256</c>,
/// <c>group16-sha512</c>, <c>group18-sha512</c> and <c>diffie-hellman-group-exchange-sha256</c>
/// are run; <c>curve25519-sha256</c> (BL-167) is <c>DISCONNECT</c> 11, "Key exchange not
/// implemented", at its first message. A client public value that is not a point on the curve
/// or not in 1 &lt; e &lt; p - 1 is <c>DISCONNECT</c> 2; a group exchange request no RFC 3526
/// group fits is <c>DISCONNECT</c> 3.
/// </para>
/// <para>
/// <b>Packet protection.</b> After <c>NEWKEYS</c> each direction uses the cipher and MAC agreed
/// for it: <c>aes256-gcm@openssh.com</c> and <c>aes128-gcm@openssh.com</c>, or
/// <c>aes256-ctr</c>, <c>aes192-ctr</c> and <c>aes128-ctr</c> with <c>hmac-sha2-256</c>,
/// <c>hmac-sha2-512</c> or their <c>-etm@openssh.com</c> forms. A MAC or tag that does not
/// verify is <c>DISCONNECT</c> 5; <c>chacha20-poly1305@openssh.com</c> (BL-169) is
/// <c>DISCONNECT</c> 11, "Packet protection not implemented", once <c>NEWKEYS</c> is exchanged.
/// </para>
/// <para>
/// <b>Transport messages.</b> After the first exchange a client's <c>KEXINIT</c> starts a
/// re-exchange, and the server starts one itself before reading on once either direction has
/// carried 1 GiB or an hour has passed under one set of keys. <c>IGNORE</c>, <c>DEBUG</c> and
/// <c>UNIMPLEMENTED</c> are skipped, and any other message the server does not know is answered
/// <c>UNIMPLEMENTED</c> with its sequence number (RFC 4253, section 11.4); so is a
/// connection-protocol message the server never expects, such as a reply to a request it did not
/// send. A connection-protocol message before the login is <c>DISCONNECT</c> 2. A client whose
/// first <c>KEXINIT</c> lists <c>ext-info-c</c> is sent <c>EXT_INFO</c> with
/// <c>server-sig-algs</c> right after the first <c>NEWKEYS</c> (RFC 8308).
/// </para>
/// <para>
/// <b>Limits.</b> A packet longer than <see cref="ExchangeLimits.MaxMessageBytes"/> with its
/// length field, or badly framed, is <c>DISCONNECT</c> 2 before its body is read. Everything
/// from accept to <c>USERAUTH_SUCCESS</c> runs under <see cref="ExchangeLimits.HeadTimeout"/>, which
/// closes with no bytes (ADR-0006 section 5, ADR-0051 decision 9). Every <c>DISCONNECT</c> is
/// given <see cref="DisconnectWriteDeadline"/> to be written, and writes are then completed.
/// </para>
/// </remarks>
public sealed class SshProtocolServer : IConnectionProtocolServer
{
    /// <summary>
    /// How long a <c>DISCONNECT</c> may take to write (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan DisconnectWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly SshHostKeySet hostKeys;
    private readonly SshAlgorithmOffer offer;
    private readonly ISshRandomSource randomSource;
    private readonly ISshAuthenticationPolicy authenticationPolicy;
    private readonly SshReExchangeLimits reExchangeLimits;
    private readonly ISshChannelHandlers channelHandlers;

    /// <summary>
    /// Creates an SSH server that serves <paramref name="hostKeys"/>, offers <paramref name="offer"/>
    /// and logs users in through <paramref name="authenticationPolicy"/>.
    /// </summary>
    /// <param name="hostKeys">The host keys; <paramref name="offer"/>'s host-key algorithms are theirs.</param>
    /// <param name="offer">The algorithms the server's <c>KEXINIT</c> offers.</param>
    /// <param name="authenticationPolicy">Who may log in (ADR-0051, decision 7).</param>
    /// <param name="randomSource">
    /// Where the <c>KEXINIT</c> cookie, the packet padding and a finite-field key exchange's
    /// private exponent come from.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="offer"/> names a host-key algorithm no key in <paramref name="hostKeys"/> signs with.</exception>
    public SshProtocolServer(SshHostKeySet hostKeys, SshAlgorithmOffer offer, ISshAuthenticationPolicy authenticationPolicy, ISshRandomSource randomSource)
        : this(hostKeys, offer, authenticationPolicy, randomSource, SshReExchangeLimits.Default)
    {
    }

    /// <summary>
    /// Creates an SSH server that also serves <paramref name="contentStore"/> over the <c>sftp</c>
    /// subsystem (ADR-0054, decisions 5 to 8).
    /// </summary>
    /// <param name="hostKeys">The host keys; <paramref name="offer"/>'s host-key algorithms are theirs.</param>
    /// <param name="offer">The algorithms the server's <c>KEXINIT</c> offers.</param>
    /// <param name="authenticationPolicy">Who may log in (ADR-0051, decision 7).</param>
    /// <param name="randomSource">Where the cookie, the padding and a finite-field private exponent come from.</param>
    /// <param name="contentStore">The content store SFTP reads.</param>
    /// <exception cref="ArgumentException"><paramref name="offer"/> names a host-key algorithm no key in <paramref name="hostKeys"/> signs with.</exception>
    public SshProtocolServer(SshHostKeySet hostKeys, SshAlgorithmOffer offer, ISshAuthenticationPolicy authenticationPolicy, ISshRandomSource randomSource, ContentStore contentStore)
        : this(hostKeys, offer, authenticationPolicy, randomSource, SshReExchangeLimits.Default, new SshContentChannelHandlers(contentStore))
    {
    }

    /// <summary>
    /// Creates an SSH server that re-keys itself at <paramref name="reExchangeLimits"/>.
    /// </summary>
    /// <param name="hostKeys">The host keys; <paramref name="offer"/>'s host-key algorithms are theirs.</param>
    /// <param name="offer">The algorithms the server's <c>KEXINIT</c> offers.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    /// <param name="randomSource">Where the cookie, the padding and a finite-field private exponent come from.</param>
    /// <param name="reExchangeLimits">When the server starts a key re-exchange itself.</param>
    /// <param name="channelHandlers">
    /// The handlers an accepted <c>exec</c> or <c>subsystem</c> goes to;
    /// <see cref="SshNoChannelHandlers"/> when <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="offer"/> names a host-key algorithm no key in <paramref name="hostKeys"/> signs with.</exception>
    internal SshProtocolServer(
        SshHostKeySet hostKeys,
        SshAlgorithmOffer offer,
        ISshAuthenticationPolicy authenticationPolicy,
        ISshRandomSource randomSource,
        SshReExchangeLimits reExchangeLimits,
        ISshChannelHandlers? channelHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(hostKeys);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(randomSource);
        var unsigned = offer.ServerHostKey.FirstOrDefault(algorithm => !hostKeys.SignatureAlgorithms.Contains(algorithm));
        if (unsigned is not null)
        {
            throw new ArgumentException($"The offer names the host-key algorithm {unsigned}, but no host key given signs with it.", nameof(offer));
        }

        this.hostKeys = hostKeys;
        this.offer = offer;
        this.randomSource = randomSource;
        this.authenticationPolicy = authenticationPolicy;
        this.reExchangeLimits = reExchangeLimits;
        this.channelHandlers = channelHandlers ?? SshNoChannelHandlers.Instance;
    }

    /// <summary>
    /// The schemes answered: <c>scp</c> and <c>sftp</c>, two subsystems of one SSH server.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["scp", "sftp"]);

    /// <summary>
    /// Opens the SSH connection on <paramref name="connection"/> until it ends.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        var transport = new SshTransportHandshake(connection, context, offer, hostKeys, randomSource, reExchangeLimits);
        try
        {
            var firstExchange = await transport.RunAsync(cancellation.Token);
            var authentication = new SshUserAuthentication(
                transport,
                authenticationPolicy,
                context.Log,
                firstExchange.SessionIdentifier,
                () => headTimeout.CancelAfter(Timeout.InfiniteTimeSpan));
            var connectionProtocol = new SshConnectionProtocol(transport, channelHandlers, context);

            // However the message loop ends, every channel ends and its handler returns before
            // the loop's outcome - its exception included - is taken up below.
            var answering = AnswerMessagesAsync(transport, authentication, connectionProtocol, cancellation.Token);
            await Task.WhenAny(answering);
            await connectionProtocol.EndChannelsAsync();
            connectionProtocol.Dispose();
            await answering;
        }
        catch (SshExchangeEndedException ended) when (ended.Note is not null)
        {
            context.Log.Note(ended.Note);
        }
        catch (SshExchangeEndedException)
        {
        }
        catch (SshDisconnectRequiredException refusal)
        {
            context.Log.Note(refusal.Message);
            await WriteDisconnectAsync(connection, transport.PacketWriter, context, refusal);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The SSH connection was not open within the head timeout; closed with no reply.");
        }
    }

    // Reads the client's messages after the first key exchange until the client closes the
    // connection or sends a DISCONNECT, and starts a re-exchange between two of them once one is
    // due.
    private static async Task AnswerMessagesAsync(
        SshTransportHandshake transport,
        SshUserAuthentication authentication,
        SshConnectionProtocol connectionProtocol,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (transport.ReExchangeIsDue)
            {
                await connectionProtocol.ReExchangeAsync(null, cancellationToken);
            }

            byte[] payload;
            try
            {
                payload = await transport.ReadMessageAsync(cancellationToken);
            }
            catch (SshExchangeEndedException ended) when (ended.Note is null)
            {
                return;
            }

            await AnswerMessageAsync(transport, authentication, connectionProtocol, payload, cancellationToken);
        }
    }

    private static async Task AnswerMessageAsync(
        SshTransportHandshake transport,
        SshUserAuthentication authentication,
        SshConnectionProtocol connectionProtocol,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        switch (payload[0])
        {
            case SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented:
                return;
            case SshMessageNumber.KeyExchangeInit:
                await connectionProtocol.ReExchangeAsync(payload, cancellationToken);
                return;
            case var messageNumber when SshUserAuthentication.Answers(messageNumber):
                await authentication.AnswerAsync(payload, cancellationToken);
                return;
            case var messageNumber when authentication.IsLoggedIn && SshConnectionProtocol.Answers(messageNumber):
                await connectionProtocol.AnswerAsync(payload, cancellationToken);
                return;
            default:
                await AnswerUnknownMessageAsync(transport, authentication, connectionProtocol, payload[0], cancellationToken);
                return;
        }
    }

    // A connection-protocol message before the login is out of order (RFC 4252, section 6);
    // anything else not known is answered UNIMPLEMENTED (RFC 4253, section 11.4).
    private static ValueTask AnswerUnknownMessageAsync(
        SshTransportHandshake transport,
        SshUserAuthentication authentication,
        SshConnectionProtocol connectionProtocol,
        byte messageNumber,
        CancellationToken cancellationToken)
    {
        if (messageNumber is >= SshMessageNumber.FirstConnectionMessage and <= SshMessageNumber.LastConnectionMessage && !authentication.IsLoggedIn)
        {
            throw SshDisconnectRequiredException.ProtocolError($"The client sent SSH message {messageNumber} before it logged in.");
        }

        var unimplemented = new SshWireWriter();
        unimplemented.WriteByte(SshMessageNumber.Unimplemented);
        unimplemented.WriteUInt32(unchecked(transport.PacketReader!.SequenceNumber - 1));

        return connectionProtocol.WriteAsync(unimplemented.ToArray(), cancellationToken);
    }

    // The DISCONNECT gets one second to be written, sealed with the server's keys in force, and
    // then writes are completed; a peer that does not read it in time is closed all the same
    // (ADR-0006, section 5).
    private static async Task WriteDisconnectAsync(IConnection connection, SshPacketWriter packetWriter, ExchangeContext context, SshDisconnectRequiredException refusal)
    {
        context.Log.Note($"SSH disconnect sent: {(uint)refusal.Reason} {refusal.Description}");
        using var deadline = new CancellationTokenSource(DisconnectWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await packetWriter.WriteAsync(DisconnectPayload(refusal), cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The DISCONNECT was not written within the one-second write deadline; the connection was closed.");
        }
    }

    private static byte[] DisconnectPayload(SshDisconnectRequiredException refusal)
    {
        var writer = new SshWireWriter();
        writer.WriteByte(SshMessageNumber.Disconnect);
        writer.WriteUInt32((uint)refusal.Reason);
        writer.WriteString(refusal.Description);
        writer.WriteString(string.Empty);

        return writer.ToArray();
    }
}
