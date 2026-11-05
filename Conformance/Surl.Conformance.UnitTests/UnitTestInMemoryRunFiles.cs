using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A test case run's files held in memory by path.
/// </summary>
internal sealed class UnitTestInMemoryRunFiles : IUpstreamTestCaseRunFiles
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public UnitTestInMemoryRunFiles With(string path, string latin1Text)
    {
        files[path] = Encoding.Latin1.GetBytes(latin1Text);
        return this;
    }

    public byte[]? ReadAllBytesOrNull(string path) => files.GetValueOrDefault(path);

    public bool Exists(string path) => files.ContainsKey(path);
}
