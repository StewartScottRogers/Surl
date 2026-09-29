using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The configured accounts, checked in constant time (ADR-0032, section 8). Each password is
/// kept as the SHA-256 of its UTF-8 bytes, and a password sent is compared as the SHA-256 of
/// its bytes with <see cref="CryptographicOperations.FixedTimeEquals"/>, so the comparison's
/// length never depends on the password's. An unknown user name is compared against a random
/// dummy hash, so it costs the same work as a wrong password and gets the same answer. For
/// Digest it also keeps each named account's <c>H(name:surl:password)</c> under every
/// <see cref="DigestAlgorithm"/>, computed once here, and never the clear password (ADR-0036);
/// for NTLM, each named account's NT hash (ADR-0039).
/// </summary>
public sealed class AccountBook
{
    private readonly Dictionary<string, byte[]> passwordHashes = new(StringComparer.Ordinal);
    private readonly byte[] dummyPasswordHash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
    private readonly ISecretComparer secretComparer;
    private readonly Dictionary<string, DigestAccount> digestAccounts = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<string> dummyUserHashes = [.. Enum.GetValues<DigestAlgorithm>()
        .Select(algorithm => DigestCalculation.HashHex(algorithm, RandomNumberGenerator.GetBytes(32)))];
    private readonly DigestAccount dummyDigestAccount;
    private readonly Dictionary<string, NtlmAccount> ntlmAccounts = new(StringComparer.Ordinal);
    private readonly NtlmAccount dummyNtlmAccount = new(null, RandomNumberGenerator.GetBytes(16));

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
        dummyDigestAccount = new DigestAccount(null, [dummyUserHashes, dummyUserHashes]);
        var named = new List<Account>();
        foreach (var account in accounts)
        {
            if (!passwordHashes.TryAdd(account.UserName, SHA256.HashData(Encoding.UTF8.GetBytes(account.Password))))
            {
                throw new ArgumentException($"User {account.UserName} is given twice.", nameof(accounts));
            }

            named.AddRange(account.UserName.Length > 0 ? [account] : []);
        }

        AddDigestAccounts(named);
        foreach (var account in named)
        {
            ntlmAccounts[account.UserName] =
                new NtlmAccount(account.UserName, NtlmV2Calculation.ComputeNtHash(account.Password));
        }
    }

    private void AddDigestAccounts(List<Account> named)
    {
        // Every UTF-8 spelling first, so an ISO-8859-1 spelling never shadows one. An ASCII name
        // is its own ISO-8859-1 spelling, so its entry carries both sets of hashes (ADR-0036).
        foreach (var account in named)
        {
            var utf8Spelling = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(account.UserName));
            var iso88591Hashes = utf8Spelling == account.UserName ? CreateIso88591UserHashes(account) : dummyUserHashes;
            digestAccounts[utf8Spelling] =
                new DigestAccount(account.UserName, [CreateUserHashes(account, Encoding.UTF8), iso88591Hashes]);
        }

        foreach (var account in named.Where(IsIso88591Only))
        {
            digestAccounts.TryAdd(
                account.UserName,
                new DigestAccount(account.UserName, [CreateUserHashes(account, Encoding.Latin1), dummyUserHashes]));
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

    /// <summary>
    /// The comparer every check of a secret, or of a value computed from one, goes through.
    /// </summary>
    internal ISecretComparer SecretComparer => secretComparer;

    /// <summary>
    /// The account a Digest <c>username</c> names, as received (one character per byte). A name
    /// is found by the UTF-8 bytes of an account's name, with that account's user hashes over
    /// UTF-8; or, for an account whose name and password are all ISO-8859-1, by those bytes,
    /// with hashes over ISO-8859-1 (ADR-0036). Every entry holds exactly two sets of hashes -
    /// the second the ISO-8859-1 set of an ASCII name, or random - so every check costs two
    /// comparisons. Any other name gets a dummy with random hashes, which matches nothing.
    /// </summary>
    /// <param name="userName">The <c>username</c> parameter, unquoted.</param>
    /// <returns>The account's two sets of user hashes, or the dummy's.</returns>
    internal DigestAccount FindDigestAccount(string userName) =>
        digestAccounts.GetValueOrDefault(userName, dummyDigestAccount);

    /// <summary>
    /// The account an NTLM <c>UserName</c> names, matched exactly, with the NT hash of its
    /// password computed at start-up. Any other name, the empty one included, gets a dummy with
    /// a random hash, so it costs the same work and matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="userName">The user name as sent.</param>
    /// <returns>The account's name and NT hash, or the dummy's.</returns>
    internal NtlmAccount FindNtlmAccount(string userName) =>
        ntlmAccounts.GetValueOrDefault(userName, dummyNtlmAccount);

    private static IReadOnlyList<string> CreateUserHashes(Account account, Encoding encoding) =>
        [.. Enum.GetValues<DigestAlgorithm>().Select(algorithm => DigestCalculation.ComputeUserHash(
            algorithm, encoding, account.UserName, DigestAuthenticationMethod.Realm, account.Password))];

    private IReadOnlyList<string> CreateIso88591UserHashes(Account account) =>
        IsIso88591Only(account) ? CreateUserHashes(account, Encoding.Latin1) : dummyUserHashes;

    private static bool IsIso88591Only(Account account) =>
        !$"{account.UserName}{account.Password}".Any(character => character > 'ÿ');

    private bool CheckSecret(string? userName, ReadOnlySpan<byte> secret)
    {
        byte[]? expectedHash = null;
        var known = userName is not null && passwordHashes.TryGetValue(userName, out expectedHash);
        var matches = secretComparer.FixedTimeEquals(SHA256.HashData(secret), expectedHash ?? dummyPasswordHash);

        return known && matches;
    }
}
