namespace Surl.Conformance;

/// <summary>
/// A <c>--user-file</c> (ADR-0032, section 2) written under the temporary directory for one
/// test, holding the account <c>tester:secret</c> and the Bearer token <c>the-bearer-token</c>.
/// Disposing it deletes the file.
/// </summary>
internal sealed class AccountsFile : IDisposable
{
    /// <summary>The account's user name.</summary>
    public const string User = "tester";

    /// <summary>The account's password.</summary>
    public const string Password = "secret";

    /// <summary>The Bearer token: the password of the account with the empty name.</summary>
    public const string BearerToken = "the-bearer-token";

    private AccountsFile(string path) => Path = path;

    /// <summary>Gets the file's full path, to pass to <c>--user-file</c>.</summary>
    public string Path { get; }

    /// <summary>Writes the file and returns it.</summary>
    public static async Task<AccountsFile> WriteAsync(CancellationToken cancellationToken)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"surl-conformance-users-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, $"{User}:{Password}\n:{BearerToken}\n", cancellationToken);
        return new AccountsFile(path);
    }

    /// <inheritdoc/>
    public void Dispose() => File.Delete(Path);
}
