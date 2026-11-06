using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// A <c>natss</c> client over the in-memory TLS upgrade the mail servers' tests use
/// (<see cref="InMemoryConnection.UpgradeToTlsAsync"/>): its first chunk is what the client sends
/// before TLS (a stand-in ClientHello beginning <c>0x16</c>, or plain text), which the stand-in
/// handshake consumes, and the chunks after it are what the client sends inside TLS, as plaintext.
/// </summary>
internal sealed class UnitTestTlsClientConnection : IConnection
{
    /// <summary>A stand-in ClientHello: a TLS handshake record header and nothing more.</summary>
    public static readonly byte[] ClientHello = [0x16, 0x03, 0x01, 0x00, 0x00];

    private readonly InMemoryConnection inner;
    private readonly int firstChunkLength;
    private readonly bool clientSpeaksFirst;
    private readonly TimeProvider clock;

    /// <param name="clientSpeaksFirst">
    /// Whether the client sends its first chunk at once (TLS first); when <see langword="false"/>
    /// it sends nothing until the server has written something (INFO first).
    /// </param>
    /// <param name="firstChunk">What the client sends before TLS; empty for nothing.</param>
    /// <param name="insideTls">The lines the client sends once TLS is in force.</param>
    /// <param name="clock">Times peeks past the end of the script.</param>
    /// <param name="closesWhenExhausted">Whether the client half-closes once the script is exhausted.</param>
    /// <param name="upgradeFails">Whether the handshake fails.</param>
    /// <param name="handshakeHangs">Whether the handshake waits until it is cancelled.</param>
    public UnitTestTlsClientConnection(
        bool clientSpeaksFirst,
        byte[] firstChunk,
        IEnumerable<string> insideTls,
        TimeProvider clock,
        bool closesWhenExhausted = true,
        bool upgradeFails = false,
        bool handshakeHangs = false)
    {
        this.clientSpeaksFirst = clientSpeaksFirst;
        this.clock = clock;
        firstChunkLength = firstChunk.Length;
        HandshakeHangs = handshakeHangs;
        inner = new InMemoryConnection(
            [firstChunk, .. insideTls.Select(line => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(line))],
            new IPEndPoint(IPAddress.Loopback, 4222),
            peerHalfClosesWhenExhausted: closesWhenExhausted,
            upgradeFails: upgradeFails,
            peekClock: clock);
    }

    public bool HandshakeHangs { get; }

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public bool UpgradeRequested { get; private set; }

    /// <summary>Everything the server wrote, before and inside TLS, as text.</summary>
    public string Written => Encoding.UTF8.GetString(inner.WrittenBytes);

    /// <summary>What the server wrote before TLS was in force.</summary>
    public string WrittenBeforeTls { get; private set; } = string.Empty;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) =>
        clientSpeaksFirst || inner.WrittenBytes.Length > 0 ? inner.PeekByteAsync(wait, cancellationToken) : ValueTask.FromResult<byte?>(null);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public async ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        UpgradeRequested = true;
        WrittenBeforeTls = Written;
        if (HandshakeHangs)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, clock, cancellationToken);
        }

        var consumed = new byte[firstChunkLength];
        var read = 0;
        while (read < consumed.Length)
        {
            read += await inner.ReadAsync(consumed.AsMemory(read), cancellationToken);
        }

        return await inner.UpgradeToTlsAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
