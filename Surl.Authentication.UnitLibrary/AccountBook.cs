using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The configured accounts, checked in constant time (ADR-0032, section 8). Each password is
/// kept as the SHA-256 of its UTF-8 bytes, and a password sent is compared as the SHA-256 of
/// its bytes with <see cref="CryptographicOperations.FixedTimeEquals"/>, so the comparison's
/// length never depends on the password's. An unknown user name is compared against a random
/// dummy hash, so it costs the same work as a wrong password and gets the same answer.
/// </summary>
public sealed class AccountBook
{
    private readonly Dictionary<string, byte[]> passwordHashes = new(StringComparer.Ordinal);
    private readonly byte[] dummyPasswordHash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
    private readonly ISecretComparer secretComparer;

    /// <summary>
    /// Keeps <paramref name="accounts"/>, which <c>Surl.Cli</c> and <see cref="UserFileParser"/>
    /// have already checked.
    /// </summary>
    /// <param name="accounts">Every configured account; no user name twice.</param>
    /// <exception cref="ArgumentException">A user name is given twice.</exception>
    public AccountBook(IEnumerable<Account> accounts)
        : this(accounts, CryptographicSecretComparer.Instance)
    {
    }

    internal AccountBook(IEnumerable<Account> accounts, ISecretComparer secretComparer)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.secretComparer = secretComparer;
        foreach (var account in accounts)
        {
            if (!passwordHashes.TryAdd(account.UserName, SHA256.HashData(Encoding.UTF8.GetBytes(account.Password))))
            {
                throw new ArgumentException($"User {account.UserName} is given twice.", nameof(accounts));
            }
        }
    }

    /// <summary>
    /// Whether any account is configured. With none, every login is refused and HTTP serves
    /// anonymous reads (ADR-0032, section 4).
    /// </summary>
    public bool HasAccounts => passwordHashes.Count > 0;

    /// <summary>
    /// Checks a user name and password. The empty user name is a Bearer token's account and is
    /// never matched here (ADR-0032, section 1).
    /// </summary>
    /// <param name="userName">The user name as sent, or <see langword="null"/> when none was.</param>
    /// <param name="password">The password bytes as sent.</param>
    /// <returns><see langword="true"/> only when an account with that name has that password.</returns>
    public bool CheckPassword(string? userName, ReadOnlySpan<byte> password) =>
        CheckSecret(string.IsNullOrEmpty(userName) ? null : userName, password);

    /// <summary>
    /// Checks a Bearer token against the empty-name account's password (ADR-0032, section 1).
    /// </summary>
    /// <param name="token">The token bytes as sent.</param>
    /// <returns><see langword="true"/> only when the token is configured.</returns>
    public bool CheckBearerToken(ReadOnlySpan<byte> token) => CheckSecret(string.Empty, token);

    private bool CheckSecret(string? userName, ReadOnlySpan<byte> secret)
    {
        byte[]? expectedHash = null;
        var known = userName is not null && passwordHashes.TryGetValue(userName, out expectedHash);
        var matches = secretComparer.FixedTimeEquals(SHA256.HashData(secret), expectedHash ?? dummyPasswordHash);

        return known && matches;
    }
}
