namespace Surl.Conformance;

/// <summary>
/// A temporary <c>--user-file</c> whose one account is the test KDC's user principal,
/// <c>tester@SURL.TEST</c>: a Kerberos ticket is a login only when an account has the principal's
/// name (ADR-0057 decision 10). Its password is never used. Disposing it deletes the file.
/// </summary>
internal sealed class KerberosAccountsFile : IDisposable
{
    private KerberosAccountsFile(string path) => Path = path;

    /// <summary>Gets the file's full path, to pass to <c>--user-file</c>.</summary>
    public string Path { get; }

    /// <summary>Writes the file and returns it.</summary>
    public static async Task<KerberosAccountsFile> WriteAsync(CancellationToken cancellationToken)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"surl-conformance-kerberos-users-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, $"{KerberosTestKdcOnLoopback.UserPrincipal}:unused\n", cancellationToken);
        return new KerberosAccountsFile(path);
    }

    /// <inheritdoc/>
    public void Dispose() => File.Delete(Path);
}
