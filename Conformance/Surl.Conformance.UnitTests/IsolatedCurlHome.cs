namespace Surl.Conformance;

/// <summary>
/// A temporary directory curl runs with as its <c>HOME</c> and <c>USERPROFILE</c>, so an
/// <c>scp://</c> or <c>sftp://</c> run reads neither the operator's <c>known_hosts</c> nor their
/// <c>id_rsa</c> (ADR-0051 decision 8), and where a test keeps the local files a transfer reads
/// or writes. Disposing it deletes the directory.
/// </summary>
internal sealed class IsolatedCurlHome : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("surl-conformance-curl-home-");

    /// <summary>Gets the environment variables that point curl at this directory.</summary>
    public IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>
    {
        ["HOME"] = directory.FullName,
        ["USERPROFILE"] = directory.FullName,
    };

    /// <summary>Gets the full path of <paramref name="name"/> in this directory.</summary>
    public string PathOf(string name) => Path.Combine(directory.FullName, name);

    /// <summary>Writes <paramref name="contents"/> to <paramref name="name"/> and returns its full path.</summary>
    public async Task<string> WriteFileAsync(string name, byte[] contents, CancellationToken cancellationToken)
    {
        var path = PathOf(name);
        await File.WriteAllBytesAsync(path, contents, cancellationToken);
        return path;
    }

    /// <inheritdoc/>
    public void Dispose() => directory.Delete(recursive: true);
}
