using Surl.Http2;

namespace Surl.Protocol.Socks;

/// <summary>
/// Finds the first HTTP/2 connection preface (<see cref="Http2ConnectionPreface.Preface"/>) in a
/// stream of bytes given chunk by chunk, however the chunks split it.
/// </summary>
internal sealed class SocksHttp2PrefaceFinder
{
    // The latest bytes given, one fewer than the preface at most: the start of a preface still arriving.
    private byte[] tail = [];
    private bool hasDroppedBytes;

    /// <summary>
    /// Searches the next chunk of the stream. Once it has returned <see langword="true"/>, the finder
    /// is spent.
    /// </summary>
    /// <param name="chunk">The next bytes of the stream.</param>
    /// <param name="length">How many of the chunk's bytes belong up to the preface's last byte.</param>
    /// <param name="opensStream">Whether the preface is the first thing in the stream.</param>
    /// <returns>Whether the preface ends in this chunk.</returns>
    public bool TryFind(ReadOnlySpan<byte> chunk, out int length, out bool opensStream)
    {
        var preface = Http2ConnectionPreface.Preface;
        byte[] searched = [.. tail, .. chunk];
        var start = searched.AsSpan().IndexOf(preface);
        if (start < 0)
        {
            var kept = Math.Min(searched.Length, preface.Length - 1);
            hasDroppedBytes |= kept < searched.Length;
            length = 0;
            opensStream = false;
            tail = searched[^kept..];

            return false;
        }

        length = start + preface.Length - tail.Length;
        opensStream = start == 0 && !hasDroppedBytes;

        return true;
    }
}
