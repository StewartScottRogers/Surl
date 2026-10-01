using System.IO.Compression;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Compresses the server's packet payloads as one zlib stream (RFC 1950) that runs from the
/// moment compression starts until the next <c>NEWKEYS</c>, each payload ended with a sync
/// flush so the client can inflate it whole (RFC 4253, section 6.2; ADR-0051, decision 2).
/// </summary>
internal sealed class SshZlibCompressor : IDisposable
{
    /// <summary>The compression method RFC 4253 section 6.2 names, started at <c>NEWKEYS</c>.</summary>
    public const string Zlib = "zlib";

    /// <summary>
    /// OpenSSH's delayed compression method (<c>PROTOCOL</c>, section 2.2): started only once
    /// <c>USERAUTH_SUCCESS</c> is sent.
    /// </summary>
    public const string DelayedZlib = "zlib@openssh.com";

    private readonly MemoryStream compressed = new();
    private readonly ZLibStream deflater;

    /// <summary>
    /// Starts a new zlib stream.
    /// </summary>
    public SshZlibCompressor() => deflater = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true);

    /// <summary>
    /// Compresses <paramref name="payload"/> as the stream's next piece.
    /// </summary>
    /// <param name="payload">The message, message number first.</param>
    /// <returns>The compressed bytes, the zlib header before the first payload's.</returns>
    public byte[] Compress(ReadOnlySpan<byte> payload)
    {
        deflater.Write(payload);
        deflater.Flush();
        var bytes = compressed.ToArray();
        compressed.SetLength(0);

        return bytes;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        deflater.Dispose();
        compressed.Dispose();
    }
}
