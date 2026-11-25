using Surl.Cli;

namespace Surl.Mcp;

/// <summary>
/// The <see cref="IConfigFileSource"/> <see cref="StartServerSafetyCheck"/> parses with: it reads no file, and records
/// that the parser asked for one, so a <c>-K</c> is seen however <c>surl</c> lets it be spelled.
/// </summary>
internal sealed class ConfigFileReadRecorder : IConfigFileSource
{
    /// <summary>Whether the parser asked to read a config file.</summary>
    public bool WasReadRequested { get; private set; }

    /// <inheritdoc/>
    public byte[]? Read(string path, int maxByteCount)
    {
        WasReadRequested = true;
        return null;
    }

    /// <inheritdoc/>
    public string IdentityOf(string path) => path;
}
