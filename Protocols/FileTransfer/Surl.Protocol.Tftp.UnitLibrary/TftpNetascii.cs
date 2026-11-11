using Surl.Content;

namespace Surl.Protocol.Tftp;

/// <summary>
/// RFC 1350 netascii as the server sends it: every LF becomes CR LF and every CR becomes CR NUL
/// (RFC 764's NVT ASCII, which RFC 1350 names). ADR-0013's netascii amendment records the answer.
/// </summary>
internal static class TftpNetascii
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    /// <summary>
    /// Converts stored bytes to netascii.
    /// </summary>
    /// <param name="stored">The file's bytes as stored.</param>
    /// <returns>The bytes sent on the wire.</returns>
    public static byte[] Encode(ReadOnlySpan<byte> stored)
    {
        var encoded = new List<byte>(stored.Length + 16);
        foreach (var value in stored)
        {
            switch (value)
            {
                case LineFeed:
                    encoded.Add(CarriageReturn);
                    encoded.Add(LineFeed);
                    break;
                case CarriageReturn:
                    encoded.Add(CarriageReturn);
                    encoded.Add(0);
                    break;
                default:
                    encoded.Add(value);
                    break;
            }
        }

        return [.. encoded];
    }

    /// <summary>
    /// Reads the whole file and converts it to netascii.
    /// </summary>
    /// <param name="contentStore">Where the file is read from.</param>
    /// <param name="mapping">The file's mapped location.</param>
    /// <param name="fileLength">The file's length when the transfer began.</param>
    /// <param name="cancellationToken">Cancelled when the exchange ends.</param>
    /// <returns>The netascii bytes, or <see langword="null"/> when the file shrank while it was read.</returns>
    public static async Task<byte[]?> ReadEncodedAsync(ContentStore contentStore, ContentPathMapping mapping, long fileLength, CancellationToken cancellationToken)
    {
        if (fileLength == 0)
        {
            return [];
        }

        using var stored = new MemoryStream(checked((int)fileLength));
        var copied = await contentStore.CopyFileBytesAsync(mapping, ContentByteRange.Select(fileLength, 0, fileLength - 1), stored, cancellationToken);
        return copied < fileLength ? null : Encode(stored.GetBuffer().AsSpan(0, (int)fileLength));
    }
}
