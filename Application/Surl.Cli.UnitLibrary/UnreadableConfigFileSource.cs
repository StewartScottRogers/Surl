namespace Surl.Cli;

/// <summary>
/// The <see cref="IConfigFileSource"/> of a host that supplies none: every config file is one that
/// cannot be read, so a <c>-K</c> is refused with <c>(37) Could not read config file &lt;file&gt;</c>.
/// </summary>
internal sealed class UnreadableConfigFileSource : IConfigFileSource
{
    /// <summary>The one instance.</summary>
    public static UnreadableConfigFileSource Instance { get; } = new();

    private UnreadableConfigFileSource()
    {
    }

    /// <inheritdoc/>
    public byte[]? Read(string path, int maxByteCount) => null;

    /// <inheritdoc/>
    public string IdentityOf(string path) => path;
}
