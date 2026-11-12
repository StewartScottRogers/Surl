using Surl.Cli;

namespace Surl.Api;

/// <summary>
/// The <see cref="IConfigFileSource"/> <see cref="SurlServerOptions.ConfigFiles"/> are read through
/// (ADR-0101 decision 2): the same file reader the start's other files use, so a test gives a config
/// file in memory as it gives a <c>--user-file</c>; the disk when the host seam is null.
/// </summary>
/// <param name="readFile">Reads a file's bytes, or throws as <see cref="File.ReadAllBytes(string)"/> does; null reads the disk.</param>
internal sealed class ApiConfigFileSource(Func<string, byte[]>? readFile) : IConfigFileSource
{
    /// <inheritdoc/>
    public byte[]? Read(string path, int maxByteCount)
    {
        try
        {
            var content = (readFile ?? File.ReadAllBytes)(path);
            return content.Length <= maxByteCount ? content : content[..maxByteCount];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string IdentityOf(string path) => Path.GetFullPath(path);
}
