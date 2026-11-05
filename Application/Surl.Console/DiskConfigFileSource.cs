using Surl.Cli;

namespace Surl.Console;

/// <summary>
/// The <see cref="IConfigFileSource"/> <c>surl</c> reads <c>-K</c>/<c>--config</c> files through
/// (ADR-0101 decision 1): files from the disk, and <c>-</c> from standard input.
/// </summary>
/// <param name="openStandardInput">Opens standard input; <c>surl</c> passes <see cref="System.Console.OpenStandardInput()"/>.</param>
/// <param name="fileNamesIgnoreCase">
/// Whether the file system compares names ignoring case, so two spellings name one file;
/// <c>surl</c> passes <see langword="true"/> except on Linux.
/// </param>
internal sealed class DiskConfigFileSource(Func<Stream> openStandardInput, bool fileNamesIgnoreCase) : IConfigFileSource
{
    private const string StandardInputPath = "-";

    /// <inheritdoc/>
    public byte[]? Read(string path, int maxByteCount)
    {
        try
        {
            using var stream = path == StandardInputPath ? openStandardInput() : File.OpenRead(path);
            return ReadAtMost(stream, maxByteCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string IdentityOf(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fileNamesIgnoreCase ? fullPath.ToUpperInvariant() : fullPath;
    }

    private static byte[] ReadAtMost(Stream stream, int maxByteCount)
    {
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while (content.Length < maxByteCount
            && (read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, maxByteCount - content.Length))) > 0)
        {
            content.Write(buffer, 0, read);
        }

        return content.ToArray();
    }
}
