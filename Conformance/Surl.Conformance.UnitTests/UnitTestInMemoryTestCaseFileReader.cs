using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Included files held in memory by path; a path not held throws <see cref="FileNotFoundException"/>.
/// </summary>
internal sealed class UnitTestInMemoryTestCaseFileReader : IUpstreamTestCaseFileReader
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public UnitTestInMemoryTestCaseFileReader With(string path, string latin1Text)
    {
        files[path] = Encoding.Latin1.GetBytes(latin1Text);
        return this;
    }

    private readonly HashSet<string> refused = new(StringComparer.Ordinal);

    public UnitTestInMemoryTestCaseFileReader Refusing(string path)
    {
        refused.Add(path);
        return this;
    }

    public byte[] ReadAllBytes(string path)
    {
        if (refused.Contains(path))
        {
            throw new ArgumentException($"Path {path} is refused.", nameof(path));
        }

        return files.TryGetValue(path, out var bytes) ? bytes : throw new FileNotFoundException($"No file {path}.", path);
    }
}
