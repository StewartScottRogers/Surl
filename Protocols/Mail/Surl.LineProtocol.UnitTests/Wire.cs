using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Builds in-memory connections from Latin-1 text, one inbound read per chunk.
/// </summary>
internal static class Wire
{
    public static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    public static string Text(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    public static InMemoryConnection Connection(params string[] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)Bytes(chunk)));

    public static InMemoryConnection OpenConnection(params string[] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)Bytes(chunk)), peerHalfClosesWhenExhausted: false);

    /// <summary>
    /// Every way to cut <paramref name="text"/> into two reads, and the one-byte-per-read split.
    /// </summary>
    public static IEnumerable<string[]> Splits(string text)
    {
        for (var cut = 1; cut < text.Length; cut++)
        {
            yield return [text[..cut], text[cut..]];
        }

        yield return text.Select(character => character.ToString()).ToArray();
    }

    public static CrlfLineReader Reader(IConnection connection, ExchangeLimits? limits = null, TimeProvider? clock = null) =>
        new(connection, limits ?? ExchangeLimits.Default, clock ?? new ManualTimeProvider());

    public static async Task<string> RemainingTextAsync(InMemoryConnection connection, CancellationToken cancellationToken)
    {
        var remaining = new MemoryStream();
        var chunk = new byte[64];
        for (var read = await connection.ReadAsync(chunk, cancellationToken); read > 0; read = await connection.ReadAsync(chunk, cancellationToken))
        {
            remaining.Write(chunk, 0, read);
        }

        return Text(remaining.ToArray());
    }
}
