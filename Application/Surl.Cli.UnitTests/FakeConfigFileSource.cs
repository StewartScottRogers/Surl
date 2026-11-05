using System.Text;

namespace Surl.Cli;

/// <summary>
/// An <see cref="IConfigFileSource"/> over config files held in memory, by path as given (<c>-</c> for
/// standard input); any other path cannot be read. Paths that differ only in case name one file.
/// </summary>
internal sealed class FakeConfigFileSource : IConfigFileSource
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    /// <summary>Holds each file's text, encoded as UTF-8.</summary>
    /// <param name="files">Pairs of path and text.</param>
    public FakeConfigFileSource(params (string Path, string Text)[] files)
    {
        foreach (var (path, text) in files)
        {
            this.files[path] = Encoding.UTF8.GetBytes(text);
        }
    }

    /// <summary>The <c>maxByteCount</c> of the last read.</summary>
    public int LastMaxByteCount { get; private set; }

    /// <summary>Holds <paramref name="content"/> as the file <paramref name="path"/>.</summary>
    /// <param name="path">The path as given.</param>
    /// <param name="content">The file's bytes.</param>
    /// <returns>This source.</returns>
    public FakeConfigFileSource WithBytes(string path, byte[] content)
    {
        files[path] = content;
        return this;
    }

    public byte[]? Read(string path, int maxByteCount)
    {
        LastMaxByteCount = maxByteCount;
        return files.TryGetValue(path, out var content) ? content : null;
    }

    public string IdentityOf(string path) => path.ToUpperInvariant();
}
