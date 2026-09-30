using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SSH server upstream curl's <c>scp://</c> and <c>sftp://</c> transfers talk to
/// (ADR-0051). So far it runs the connection's first key exchange: it exchanges identification
/// lines, reads and writes unencrypted binary packets, sends its <c>SSH_MSG_KEXINIT</c> and
/// agrees the algorithms with the client's, runs the key exchange method, signing the exchange
/// hash with one of its <see cref="SshHostKeySet"/>, and exchanges <c>NEWKEYS</c>; it then ends
/// the connection with <c>DISCONNECT</c> 11, "Packet protection not implemented" (BL-161 builds
/// it).
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
/// <b>Limits.</b> A packet longer than <see cref="ExchangeLimits.MaxMessageBytes"/> with its
/// length field, or badly framed, is <c>DISCONNECT</c> 2 before its body is read. Everything
/// up to the end of the opening runs under <see cref="ExchangeLimits.HeadTimeout"/>, which
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

    /// <summary>
    /// Creates an SSH server that serves <paramref name="hostKeys"/> and offers <paramref name="offer"/>.
    /// </summary>
    /// <param name="hostKeys">The host keys; <paramref name="offer"/>'s host-key algorithms are theirs.</param>
    /// <param name="offer">The algorithms the server's <c>KEXINIT</c> offers.</param>
    /// <param name="randomSource">
    /// Where the <c>KEXINIT</c> cookie, the packet padding and a finite-field key exchange's
    /// private exponent come from.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="offer"/> names a host-key algorithm no key in <paramref name="hostKeys"/> signs with.</exception>
    public SshProtocolServer(SshHostKeySet hostKeys, SshAlgorithmOffer offer, ISshRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(hostKeys);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(randomSource);
        var unsigned = offer.ServerHostKey.FirstOrDefault(algorithm => !hostKeys.SignatureAlgorithms.Contains(algorithm));
        if (unsigned is not null)
        {
            throw new ArgumentException($"The offer names the host-key algorithm {unsigned}, but no host key given signs with it.", nameof(offer));
        }

        this.hostKeys = hostKeys;
        this.offer = offer;
        this.randomSource = randomSource;
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
        try
        {
            var keys = await new SshTransportHandshake(connection, context, offer, hostKeys, randomSource).RunAsync(cancellation.Token);

            // BL-161 protects packets with the keys; until then the connection ends after NEWKEYS.
            throw new SshDisconnectRequiredException(
                SshDisconnectReason.ByApplication,
                "Packet protection not implemented",
                $"The SSH packet protection {keys.Algorithms.CipherClientToServer}/{keys.Algorithms.CipherServerToClient} "
                + "is not built yet; the connection was ended after NEWKEYS.");
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
            await WriteDisconnectAsync(connection, context, refusal);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The SSH connection was not open within the head timeout; closed with no reply.");
        }
    }

    // The DISCONNECT gets one second to be written, and then writes are completed; a peer
    // that does not read it in time is closed all the same (ADR-0006, section 5).
    private async Task WriteDisconnectAsync(IConnection connection, ExchangeContext context, SshDisconnectRequiredException refusal)
    {
        context.Log.Note($"SSH disconnect sent: {(uint)refusal.Reason} {refusal.Description}");
        using var deadline = new CancellationTokenSource(DisconnectWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await new SshPacketWriter(connection, randomSource).WriteAsync(DisconnectPayload(refusal), cancellation.Token);
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
