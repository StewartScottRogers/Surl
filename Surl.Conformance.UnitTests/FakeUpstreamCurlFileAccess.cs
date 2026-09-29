using System.Security.Cryptography;

namespace Surl.Conformance;

/// <summary>
/// An in-memory <see cref="IUpstreamCurlFileAccess"/>: the files it holds exist, every other
/// path does not.
/// </summary>
internal sealed class FakeUpstreamCurlFileAccess : IUpstreamCurlFileAccess
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public FakeUpstreamCurlFileAccess WithFile(string path, byte[] contents)
    {
        files[path] = contents;
        return this;
    }

    public bool FileExists(string path) => files.ContainsKey(path);

    public Stream OpenRead(string path) => new MemoryStream(files[path], writable: false);

    public static string Sha256Of(byte[] contents) => Convert.ToHexString(SHA256.HashData(contents));
}
