using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Surl.Conformance;

/// <summary>
/// A loopback listener that reads the first TLS record a client sends on its first connection -
/// the <c>ClientHello</c> - and hangs up, so a test can see which extensions the pinned upstream
/// curl build asks for. The client always fails its handshake afterwards; only the recording
/// counts. It stops listening once it has the ClientHello; disposing it stops it too.
/// </summary>
internal sealed class TlsClientHelloRecorder : IDisposable
{
    private const byte HandshakeRecord = 22;
    private const byte ClientHelloMessage = 1;

    private readonly TcpListener listener;
    private readonly Task<IReadOnlyList<int>> recording;

    private TlsClientHelloRecorder(CancellationToken cancellationToken)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        recording = RecordAsync(cancellationToken);
    }

    /// <summary>Gets the <c>https</c> URL of <paramref name="path"/> on this recorder.</summary>
    public string UrlOf(string path) => $"https://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{path}";

    /// <summary>Starts a recorder listening on <c>127.0.0.1</c>.</summary>
    public static TlsClientHelloRecorder Start(CancellationToken cancellationToken) => new(cancellationToken);

    /// <summary>
    /// Returns the extension types of the recorded <c>ClientHello</c>, in the order the client sent
    /// them, waiting at most <paramref name="timeout"/> for it.
    /// </summary>
    public Task<IReadOnlyList<int>> ExtensionTypesAsync(TimeSpan timeout) => recording.WaitAsync(timeout);

    /// <inheritdoc/>
    public void Dispose() => listener.Stop();

    private async Task<IReadOnlyList<int>> RecordAsync(CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        var header = new byte[5];
        await stream.ReadExactlyAsync(header, cancellationToken);
        if (header[0] != HandshakeRecord)
        {
            throw new InvalidDataException($"The client's first TLS record has content type {header[0]}, not handshake.");
        }

        var fragment = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        await stream.ReadExactlyAsync(fragment, cancellationToken);

        // Stopping the listener refuses the connections still waiting to be accepted - DoH opens
        // one per query - so the client fails them at once rather than waiting for a handshake.
        listener.Stop();
        return ExtensionTypesOf(fragment);
    }

    // The handshake header (4), client_version (2), random (32), then the session id, cipher
    // suites and compression methods, each behind its length, then the extensions block (RFC 8446,
    // section 4.1.2). The whole ClientHello fits one record for every hello curl sends a probe.
    private static List<int> ExtensionTypesOf(ReadOnlySpan<byte> handshake)
    {
        if (handshake[0] != ClientHelloMessage)
        {
            throw new InvalidDataException($"The client's first handshake message has type {handshake[0]}, not ClientHello.");
        }

        var at = 4 + 2 + 32;
        at += 1 + handshake[at];
        at += 2 + BinaryPrimitives.ReadUInt16BigEndian(handshake[at..]);
        at += 1 + handshake[at];
        var end = at + 2 + BinaryPrimitives.ReadUInt16BigEndian(handshake[at..]);
        at += 2;

        var types = new List<int>();
        while (at < end)
        {
            types.Add(BinaryPrimitives.ReadUInt16BigEndian(handshake[at..]));
            at += 4 + BinaryPrimitives.ReadUInt16BigEndian(handshake[(at + 2)..]);
        }

        return types;
    }
}
