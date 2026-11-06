using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Builds the connections, origins and exchange context the proxy tests drive the server with,
/// and reads the fixtures recorded from pinned upstream curl (Fixtures/README.md).
/// </summary>
internal static class ProxyTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public const string DateField = "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\n";

    public static byte[] Fixture(string folder, string file)
    {
        using var stream = typeof(ProxyTestKit).Assembly.GetManifestResourceStream($"Fixtures/{folder}/{file}")
            ?? throw new InvalidOperationException($"No fixture Fixtures/{folder}/{file}.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);

        return bytes.ToArray();
    }

    public static string FixtureText(string folder, string file) => Latin1(Fixture(folder, file));

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static ReadOnlyMemory<byte> Bytes(string text) => Encoding.Latin1.GetBytes(text);

    public static InMemoryConnection Connection(params string[] chunks) =>
        new(chunks.Select(Bytes));

    public static InMemoryConnection HeldOpenConnection(params string[] chunks) =>
        new(chunks.Select(Bytes), peerHalfClosesWhenExhausted: false);

    public static InMemoryConnection ConnectionOf(params byte[][] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk));

    public static ExchangeContext Context(
        RecordingExchangeLog log,
        ExchangeLimits? limits = null,
        TimeProvider? clock = null,
        CancellationToken cancellationToken = default) =>
        new(
            1,
            new ListenUrl("http", "127.0.0.1", 3128),
            new IPEndPoint(IPAddress.Loopback, 3128),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            clock ?? new ManualTimeProvider(Now),
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// Serves <paramref name="client"/> with a proxy reaching <paramref name="origins"/>, and
    /// returns what the proxy wrote to curl.
    /// </summary>
    public static async Task<string> ServeAsync(InMemoryConnection client, InMemoryOriginConnector origins, RecordingExchangeLog? log = null, ExchangeLimits? limits = null)
    {
        var server = new HttpProxyProtocolServer(origins);

        await server.ServeAsync(client, Context(log ?? new RecordingExchangeLog(), limits));

        return Latin1(client.WrittenBytes);
    }

    /// <summary>The fixed-text answer the proxy makes itself, as curl reads it.</summary>
    public static string Answer(int code, string reason, bool closes = false)
    {
        var body = $"{code} {reason}\n";

        return $"HTTP/1.1 {code} {reason}\r\n{DateField}Server: surl\r\nContent-Type: text/plain; charset=us-ascii\r\nContent-Length: {body.Length}\r\n"
            + (closes ? "Connection: close\r\n" : string.Empty) + "\r\n" + body;
    }
}
