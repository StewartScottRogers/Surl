using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Builds the connections and exchange context the SOCKS tests drive the server with, and reads
/// the fixtures recorded from pinned upstream curl (Fixtures/README.md).
/// </summary>
internal static class SocksTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>What the recorder answered inside each granted tunnel, playing the origin.</summary>
    public const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    public static readonly byte[] Granted = [0x00, 0x5A, 0, 0, 0, 0, 0, 0];

    public static readonly byte[] Rejected = [0x00, 0x5B, 0, 0, 0, 0, 0, 0];

    public static byte[] Fixture(string folder, string file)
    {
        using var stream = typeof(SocksTestKit).Assembly.GetManifestResourceStream($"Fixtures/{folder}/{file}")
            ?? throw new InvalidOperationException($"No fixture Fixtures/{folder}/{file}.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);

        return bytes.ToArray();
    }

    public static byte[] Latin1Bytes(string text) => Encoding.Latin1.GetBytes(text);

    public static InMemoryConnection ConnectionOf(params byte[][] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk));

    public static InMemoryConnection HeldOpenConnectionOf(params byte[][] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk), peerHalfClosesWhenExhausted: false);

    /// <summary>A SOCKS4 request: <c>04</c>, <paramref name="command"/>, the port, the address, the user ID and its zero, and any trailing bytes.</summary>
    public static byte[] Socks4Request(byte command, int port, byte[] address, string userId, params byte[] trailing) =>
        [0x04, command, (byte)(port >> 8), (byte)port, .. address, .. Latin1Bytes(userId), 0x00, .. trailing];

    public static ExchangeContext Context(
        RecordingExchangeLog log,
        TimeProvider? clock = null,
        CancellationToken cancellationToken = default,
        string scheme = "socks4") =>
        new(
            1,
            new ListenUrl(scheme, "127.0.0.1", 1080),
            new IPEndPoint(IPAddress.Loopback, 1080),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            clock ?? new ManualTimeProvider(Now),
            cancellationToken);

    /// <summary>
    /// Serves <paramref name="client"/> with a SOCKS server reaching <paramref name="origins"/>.
    /// </summary>
    public static Task ServeAsync(IConnection client, IOriginConnector origins, RecordingExchangeLog? log = null) =>
        new SocksProtocolServer(origins).ServeAsync(client, Context(log ?? new RecordingExchangeLog()));

    /// <summary>The SOCKS5 success reply the recorder sent: <c>ATYP 01</c>, 127.0.0.1:8080.</summary>
    public static readonly byte[] Socks5Succeeded = [0x05, 0x00, 0x00, 0x01, 127, 0, 0, 1, 0x1F, 0x90];

    /// <summary>A SOCKS5 failure reply with <paramref name="code"/> and <c>0.0.0.0:0</c>.</summary>
    public static byte[] Socks5Failure(byte code) => [0x05, code, 0x00, 0x01, 0, 0, 0, 0, 0, 0];

    /// <summary>A SOCKS5 request: <c>05</c>, <paramref name="command"/>, <c>00</c>, <paramref name="addressType"/>, the address bytes and the port.</summary>
    public static byte[] Socks5Request(byte command, byte addressType, byte[] address, int port) =>
        [0x05, command, 0x00, addressType, .. address, (byte)(port >> 8), (byte)port];

    /// <summary>A SOCKS5 domain-name request for CONNECT.</summary>
    public static byte[] Socks5DomainRequest(string host, int port) =>
        Socks5Request(0x01, 0x03, [(byte)host.Length, .. Latin1Bytes(host)], port);

    /// <summary>
    /// Serves <paramref name="client"/> with a SOCKS server on a <paramref name="scheme"/> listener
    /// with <paramref name="accounts"/>, reaching <paramref name="origins"/>.
    /// </summary>
    public static Task ServeAsync(
        IConnection client, IOriginConnector origins, string scheme, IAuthenticationPolicy? accounts = null, RecordingExchangeLog? log = null) =>
        new SocksProtocolServer(origins, accounts).ServeAsync(client, Context(log ?? new RecordingExchangeLog(), scheme: scheme));
}
