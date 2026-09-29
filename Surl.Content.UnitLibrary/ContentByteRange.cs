namespace Surl.Content;

/// <summary>
/// The bytes of a file to read: the whole file, an inclusive range <c>first..last</c>
/// clamped to the file's length, or an unsatisfiable range that carries the length.
/// </summary>
/// <remarks>
/// Protocol-neutral: an HTTP <c>Range</c> header, an FTP <c>REST</c> offset, a TFTP or SFTP
/// read are each turned into a first and last offset by their own protocol server, and
/// selected here against the file's length.
/// </remarks>
public sealed class ContentByteRange
{
    private ContentByteRange(bool isSatisfiable, long firstByte, long lastByte, long fileLength)
    {
        IsSatisfiable = isSatisfiable;
        FirstByte = firstByte;
        LastByte = lastByte;
        FileLength = fileLength;
    }

    /// <summary>
    /// Whether the range names bytes the file has. A zero-length whole file is satisfiable
    /// and reads as zero bytes.
    /// </summary>
    public bool IsSatisfiable { get; }

    /// <summary>
    /// The offset of the first byte to read; 0 when the range is unsatisfiable.
    /// </summary>
    public long FirstByte { get; }

    /// <summary>
    /// The offset of the last byte to read, inclusive; <see cref="FirstByte"/> − 1 when no
    /// byte is read (a zero-length whole file, or an unsatisfiable range).
    /// </summary>
    public long LastByte { get; }

    /// <summary>
    /// How many bytes the range reads: <see cref="LastByte"/> − <see cref="FirstByte"/> + 1.
    /// </summary>
    public long ByteCount => LastByte - FirstByte + 1;

    /// <summary>
    /// The length of the file the range was selected against.
    /// </summary>
    public long FileLength { get; }

    /// <summary>
    /// Selects every byte of a file.
    /// </summary>
    /// <param name="fileLength">The file's length in bytes.</param>
    /// <returns>A satisfiable range from 0 to <paramref name="fileLength"/> − 1.</returns>
    public static ContentByteRange WholeFile(long fileLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);
        return new(true, 0, fileLength - 1, fileLength);
    }

    /// <summary>
    /// Selects the inclusive range <paramref name="firstByte"/>..<paramref name="lastByte"/>
    /// of a file.
    /// </summary>
    /// <param name="fileLength">The file's length in bytes.</param>
    /// <param name="firstByte">The offset of the first byte wanted.</param>
    /// <param name="lastByte">The offset of the last byte wanted, inclusive; clamped to
    /// <paramref name="fileLength"/> − 1 when it is past the end.</param>
    /// <returns>The clamped range, or an unsatisfiable range carrying
    /// <paramref name="fileLength"/> when <paramref name="firstByte"/> is at or past the
    /// end.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A length or offset is negative, or
    /// <paramref name="firstByte"/> is greater than <paramref name="lastByte"/>.</exception>
    public static ContentByteRange Select(long fileLength, long firstByte, long lastByte)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);
        ArgumentOutOfRangeException.ThrowIfNegative(firstByte);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstByte, lastByte);
        if (firstByte >= fileLength)
        {
            return new(false, 0, -1, fileLength);
        }

        return new(true, firstByte, Math.Min(lastByte, fileLength - 1), fileLength);
    }
}
