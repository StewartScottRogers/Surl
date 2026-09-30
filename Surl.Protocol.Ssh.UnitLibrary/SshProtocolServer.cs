using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SSH server upstream curl's <c>scp://</c> and <c>sftp://</c> transfers talk to
/// (ADR-0051). So far it opens the connection: it exchanges identification lines, reads and
/// writes unencrypted binary packets, sends its <c>SSH_MSG_KEXINIT</c> and agrees the
/// algorithms with the client's, then ends the connection with <c>DISCONNECT</c> 11, "Key
/// exchange not implemented", at the key exchange method's first message (BL-160 builds it).
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

    private readonly SshAlgorithmOffer offer;
    private readonly ISshRandomSource randomSource;

    /// <summary>
    /// Creates an SSH server that offers <paramref name="offer"/>.
    /// </summary>
    /// <param name="offer">The algorithms the server's <c>KEXINIT</c> offers.</param>
    /// <param name="randomSource">Where the <c>KEXINIT</c> cookie and the packet padding come from.</param>
    public SshProtocolServer(SshAlgorithmOffer offer, ISshRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(randomSource);

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
            var algorithms = await new SshTransportHandshake(connection, context, offer, randomSource).RunAsync(cancellation.Token);

            // BL-160 builds the key exchange; until then its first message is refused.
            throw new SshDisconnectRequiredException(
                SshDisconnectReason.ByApplication,
                "Key exchange not implemented",
                $"The SSH key exchange {algorithms.KeyExchange} is not built yet; the connection was ended after the negotiation.");
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
