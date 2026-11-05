using System.Text;
using Surl.Cli;

namespace Surl.Console;

/// <summary>An <see cref="IConfigFileSource"/> over config files held in memory, by path; any other path cannot be read.</summary>
/// <param name="files">The text of each file, by path as given (<c>-</c> for standard input).</param>
internal sealed class FakeConfigFileSource(IReadOnlyDictionary<string, string> files) : IConfigFileSource
{
    public byte[]? Read(string path, int maxByteCount) =>
        files.TryGetValue(path, out var text) ? Encoding.UTF8.GetBytes(text) : null;

    public string IdentityOf(string path) => path;
}
