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
/// <see cref="DigestAlgorithm"/>, computed once here (ADR-0036); for NTLM, each named account's
/// NT hash (ADR-0039); and for AWS Signature Version 4, each named account's password as UTF-8
/// bytes, since every signing key derives from the secret itself (ADR-0043); and, for the
/// challenge-response mail logins, the same bytes (ADR-0049, section 5).
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
    private readonly Dictionary<string, AwsSigV4Account> awsSigV4Accounts = new(StringComparer.Ordinal);
    private readonly AwsSigV4Account dummyAwsSigV4Account = new(null, RandomNumberGenerator.GetBytes(40));
    private readonly Dictionary<string, ChallengeResponseAccount> challengeResponseAccounts = new(StringComparer.Ordinal);
    private readonly ChallengeResponseAccount dummyChallengeResponseAccount = new(null, RandomNumberGenerator.GetBytes(16));

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
            awsSigV4Accounts[account.UserName] = new AwsSigV4Account(account.UserName, Encoding.UTF8.GetBytes(account.Password));
            challengeResponseAccounts[account.UserName] =
                new ChallengeResponseAccount(account.UserName, Encoding.UTF8.GetBytes(account.Password));
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
    /// Whether an account of exactly <paramref name="userName"/> exists, its password unused: how
    /// SASL <c>EXTERNAL</c> matches a client certificate's name (ADR-0049, section 4). The empty
    /// name, a Bearer token's account, never matches.
    /// </summary>
    /// <param name="userName">The name, compared ordinally.</param>
    /// <returns><see langword="true"/> only when a named account has that name.</returns>
    internal bool HasNamedAccount(string userName) => userName.Length > 0 && passwordHashes.ContainsKey(userName);

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

    /// <summary>
    /// The account an AWS Signature Version 4 access key ID names, matched exactly, with the
    /// UTF-8 bytes of its password as the secret access key (ADR-0043). Any other key, the
    /// empty one included, gets a dummy with a random secret, so it costs the same work and
    /// matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="accessKeyId">The access key ID as sent.</param>
    /// <returns>The account's name and secret, or the dummy's.</returns>
    internal AwsSigV4Account FindAwsSigV4Account(string accessKeyId) =>
        awsSigV4Accounts.GetValueOrDefault(accessKeyId, dummyAwsSigV4Account);

    /// <summary>
    /// The account a <c>CRAM-MD5</c>, <c>DIGEST-MD5</c> or <c>APOP</c> user name names, matched
    /// exactly, with the UTF-8 bytes of its password (ADR-0049, section 5). Any other name, the
    /// empty one and none included, gets a dummy with a random password, so it costs the same
    /// work and matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="userName">The user name as sent, or <see langword="null"/> when it cannot be read.</param>
    /// <returns>The account's name and password, or the dummy's.</returns>
    internal ChallengeResponseAccount FindChallengeResponseAccount(string? userName) =>
        challengeResponseAccounts.GetValueOrDefault(userName ?? string.Empty, dummyChallengeResponseAccount);

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
