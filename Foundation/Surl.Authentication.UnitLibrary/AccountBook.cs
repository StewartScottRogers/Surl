using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The accounts, checked in constant time (ADR-0032, section 8). Each password is
/// kept as the SHA-256 of its UTF-8 bytes, and a password sent is compared as the SHA-256 of
/// its bytes with <see cref="CryptographicOperations.FixedTimeEquals"/>, so the comparison's
/// length never depends on the password's. An unknown user name is compared against a random
/// dummy hash, so it costs the same work as a wrong password and gets the same answer. For
/// Digest it also keeps each named account's <c>H(name:surl:password)</c> under every
/// <see cref="DigestAlgorithm"/>, computed once here (ADR-0036); for NTLM, each named account's
/// NT hashes, one for each way an upstream curl build hashes the password (ADR-0039,
/// <see cref="NtlmPasswordHashes"/>); and for AWS Signature Version 4, each named account's password as UTF-8
/// bytes, since every signing key derives from the secret itself (ADR-0043); and, for the
/// challenge-response mail logins, the same bytes (ADR-0049, section 5); and, for SASL
/// <c>SCRAM-SHA-1</c> and <c>SCRAM-SHA-256</c>, each named account's <see cref="ScramCredentials"/>
/// under each hash, over a random salt and 4096 iterations (BL-416).
/// <para>
/// The accounts are a store a test controls while the server runs (ADR-0124, decision 3; ADR-0131):
/// <see cref="Add"/>, <see cref="Remove"/>, <see cref="Reset"/>, <see cref="TakeSnapshot"/> and
/// <see cref="Restore"/>. Every view <see cref="ForScheme"/> makes reads the same changing set, so
/// every login of every protocol is judged by a change at once. A change is published on the
/// event stream as a <see cref="StoreChangedEvent"/> naming at most a user name.
/// </para>
/// </summary>
public sealed class AccountBook
{
    private readonly byte[] dummyPasswordHash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
    private readonly ISecretComparer secretComparer;
    private readonly DigestAccount dummyDigestAccount;
    private readonly NtlmAccount dummyNtlmAccount = new(null, NtlmPasswordHashes.ComputeRandom());
    private readonly AwsSigV4Account dummyAwsSigV4Account = new(null, RandomNumberGenerator.GetBytes(40));
    private readonly AzureStorageAccount dummyAzureStorageAccount = new(null, RandomNumberGenerator.GetBytes(64));
    private readonly ChallengeResponseAccount dummyChallengeResponseAccount = new(null, RandomNumberGenerator.GetBytes(16));
    private readonly byte[] dummyScramSaltKey = RandomNumberGenerator.GetBytes(32);
    private readonly AccountStore store;
    private readonly Func<DerivedAccount, bool>? isAdmitted;
    private FilteredAccountSet? filtered;

    /// <summary>
    /// Keeps <paramref name="accounts"/>, which <c>Surl.Cli</c> and <see cref="UserFileParser"/>
    /// have already checked, publishing no change.
    /// </summary>
    /// <param name="accounts">Every configured account; no user name twice.</param>
    /// <exception cref="ArgumentException">A user name is given twice.</exception>
    public AccountBook(IEnumerable<Account> accounts)
        : this(accounts, CryptographicSecretComparer.Instance)
    {
    }

    /// <summary>
    /// Keeps <paramref name="accounts"/>, publishing every later change on <paramref name="events"/>.
    /// </summary>
    /// <param name="accounts">Every configured account; no user name twice. A reset returns to them.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <exception cref="ArgumentException">A user name is given twice.</exception>
    public AccountBook(IEnumerable<Account> accounts, ISurlEventPublisher events)
        : this(accounts, CryptographicSecretComparer.Instance, null, events ?? throw new ArgumentNullException(nameof(events)))
    {
    }

    /// <summary>
    /// As the public constructor, comparing through <paramref name="secretComparer"/>, with each
    /// account's SCRAM salt under each hash from <paramref name="createScramSalt"/>: random
    /// <see cref="ScramCredentials.SaltLength"/> bytes when it is <see langword="null"/>, fixed
    /// in tests so the RFCs' examples replay.
    /// </summary>
    /// <param name="accounts">Every configured account; no user name twice.</param>
    /// <param name="secretComparer">The comparer every secret goes through.</param>
    /// <param name="createScramSalt">Makes one account's salt under one hash.</param>
    /// <param name="events">Where changes are published; nowhere when <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A user name is given twice.</exception>
    internal AccountBook(
        IEnumerable<Account> accounts,
        ISecretComparer secretComparer,
        Func<ScramHash, byte[]>? createScramSalt = null,
        ISurlEventPublisher? events = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        this.secretComparer = secretComparer;
        store = new AccountStore(accounts, events ?? DiscardingSurlEventPublisher.Instance, createScramSalt);
        dummyDigestAccount = new DigestAccount(null, [store.DummyUserHashes, store.DummyUserHashes]);
    }

    // A view of source's store admitting only the accounts isAdmitted admits: every secret and
    // value derived from one is shared, never derived again, and a name it leaves out finds the
    // dummies as an unknown name does (ADR-0102, decision 4.3).
    private AccountBook(AccountBook source, Func<DerivedAccount, bool> isAdmitted)
    {
        secretComparer = source.secretComparer;
        store = source.store;
        dummyDigestAccount = new DigestAccount(null, [store.DummyUserHashes, store.DummyUserHashes]);
        this.isAdmitted = isAdmitted;
    }

    /// <summary>
    /// The user names of the accounts this book or view holds now, in the order they were
    /// configured or added; the empty name is a Bearer token's account.
    /// </summary>
    public IReadOnlyList<string> UserNames => [.. Current.Accounts.Select(account => account.UserName)];

    /// <summary>
    /// Whether any account is configured. With none, every login is refused and HTTP serves
    /// anonymous reads (ADR-0032, section 4).
    /// </summary>
    public bool HasAccounts => Current.PasswordHashes.Count > 0;

    /// <summary>
    /// The comparer every check of a secret, or of a value computed from one, goes through.
    /// </summary>
    internal ISecretComparer SecretComparer => secretComparer;

    // The set this book judges by: the store's, or, for a scheme view, the store's as the view
    // admits it, built again only when the store's set has changed.
    private AccountSet Current => isAdmitted is null ? store.Current : FilterCurrent(isAdmitted);

    /// <summary>
    /// The view of these accounts a server answering <paramref name="scheme"/> judges logins by:
    /// every account <see cref="Account.MayLogInOn"/> that scheme, and none other, so a login
    /// naming an account restricted to other schemes is refused exactly as one naming no account
    /// (ADR-0102, decision 4.3; ADR-0032, section 8). The view reads the same changing accounts
    /// as this book, so an account added, removed, reset or restored later is judged through it at once.
    /// </summary>
    /// <param name="scheme">The scheme the server answers, compared without regard to case.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public AccountBook ForScheme(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        var admitted = isAdmitted;

        return new AccountBook(
            this,
            admitted is null ? account => account.MayLogInOn(scheme) : account => admitted(account) && account.MayLogInOn(scheme));
    }

    /// <summary>
    /// Adds <paramref name="account"/> while the server runs, deriving every credential it carries
    /// exactly as for an account read at start, and publishes <see cref="StoreChange.AccountAdded"/>
    /// with its user name. Logins made from now on, on every scheme it may log in on, are judged
    /// with it. A scheme view adds to the book it was made from.
    /// </summary>
    /// <param name="account">The account, checked as <c>Surl.Cli</c> checks a <c>--user</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="account"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An account of that user name exists.</exception>
    public void Add(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        store.Add(account);
    }

    /// <summary>
    /// Removes the account of <paramref name="userName"/> while the server runs and publishes
    /// <see cref="StoreChange.AccountRemoved"/> with its user name. A login naming it from now on is
    /// refused exactly as one naming no account; a session already logged in as it runs on until
    /// it ends (ADR-0124, decision 3).
    /// </summary>
    /// <param name="userName">The user name, compared ordinally; the empty name is the Bearer token's account.</param>
    /// <returns><see langword="true"/> when an account was removed; <see langword="false"/> when none has that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is <see langword="null"/>.</exception>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);

        return store.Remove(userName);
    }

    /// <summary>
    /// Returns the accounts to exactly those configured at start, removing every account added
    /// since and bringing back every one removed, and publishes <see cref="StoreChange.Reset"/>.
    /// </summary>
    public void Reset() => store.Reset();

    /// <summary>
    /// Copies the accounts as they are now: password hashes and what was derived from each
    /// password, never a clear password.
    /// </summary>
    /// <returns>The snapshot, which only this book and its views restore.</returns>
    public AccountSnapshot TakeSnapshot() => store.TakeSnapshot();

    /// <summary>
    /// Replaces the accounts with <paramref name="snapshot"/>'s and publishes <see cref="StoreChange.Restored"/>.
    /// </summary>
    /// <param name="snapshot">A snapshot taken of this book or one of its views.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The snapshot was taken of another account book.</exception>
    public void Restore(AccountSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        store.Restore(snapshot);
    }

    /// <summary>
    /// The names of the accounts that may not log in on <paramref name="scheme"/>.
    /// </summary>
    /// <param name="scheme">The scheme, compared without regard to case.</param>
    /// <returns>The names, compared ordinally.</returns>
    internal IReadOnlySet<string> NamesRefusedOn(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return Current.Accounts.Where(account => !account.MayLogInOn(scheme)).Select(account => account.UserName).ToHashSet(StringComparer.Ordinal);
    }

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
    internal bool HasNamedAccount(string userName) => userName.Length > 0 && Current.PasswordHashes.ContainsKey(userName);

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
        Current.DigestAccounts.GetValueOrDefault(userName, dummyDigestAccount);

    /// <summary>
    /// The account an NTLM <c>UserName</c> names, matched exactly, with the NT hashes of its
    /// password computed when the account was read or added (<see cref="NtlmPasswordHashes"/>). Any other name, the
    /// empty one included, gets a dummy with as many random hashes, so it costs the same work and
    /// matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="userName">The user name as sent.</param>
    /// <returns>The account's name and NT hashes, or the dummy's.</returns>
    internal NtlmAccount FindNtlmAccount(string userName) =>
        Current.NtlmAccounts.GetValueOrDefault(userName, dummyNtlmAccount);

    /// <summary>
    /// The account an AWS Signature Version 4 access key ID names, matched exactly, with the
    /// UTF-8 bytes of its password as the secret access key (ADR-0043). Any other key, the
    /// empty one included, gets a dummy with a random secret, so it costs the same work and
    /// matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="accessKeyId">The access key ID as sent.</param>
    /// <returns>The account's name and secret, or the dummy's.</returns>
    internal AwsSigV4Account FindAwsSigV4Account(string accessKeyId) =>
        Current.AwsSigV4Accounts.GetValueOrDefault(accessKeyId, dummyAwsSigV4Account);

    /// <summary>
    /// The Azure Storage account named <paramref name="accountName"/>, with its key decoded from
    /// the password's base64 text (ADR-0100 decision 4.1), or a dummy with a random key and no
    /// name when no account has that name or its password is not base64, so the check costs the
    /// same and matches nothing (ADR-0109 decision 3.4).
    /// </summary>
    /// <param name="accountName">The storage account name as sent.</param>
    /// <returns>The account's name and key, or the dummy's.</returns>
    internal AzureStorageAccount FindAzureStorageAccount(string accountName) =>
        Current.AzureStorageAccounts.GetValueOrDefault(accountName, dummyAzureStorageAccount);

    /// <summary>
    /// The account a <c>CRAM-MD5</c>, <c>DIGEST-MD5</c> or <c>APOP</c> user name names, matched
    /// exactly, with the UTF-8 bytes of its password (ADR-0049, section 5). Any other name, the
    /// empty one and none included, gets a dummy with a random password, so it costs the same
    /// work and matches nothing (ADR-0032, section 8).
    /// </summary>
    /// <param name="userName">The user name as sent, or <see langword="null"/> when it cannot be read.</param>
    /// <returns>The account's name and password, or the dummy's.</returns>
    internal ChallengeResponseAccount FindChallengeResponseAccount(string? userName) =>
        Current.ChallengeResponseAccounts.GetValueOrDefault(userName ?? string.Empty, dummyChallengeResponseAccount);

    /// <summary>
    /// The SCRAM credentials under <paramref name="hash"/> of the account <paramref name="userName"/>
    /// names, matched exactly, derived when the account was read or added (RFC 5802 section 3). Any other name, the empty
    /// one included, gets a dummy with random keys and a salt that is the same every time that
    /// name is asked about, as RFC 5802 section 9 advises, with the same iteration count: its
    /// <c>server-first-message</c> cannot be told from an account's, it costs the same work, and
    /// its proof matches nothing (ADR-0032, section 8; ADR-0006, section 3).
    /// </summary>
    /// <param name="hash">The mechanism's hash.</param>
    /// <param name="userName">The user name as sent, decoded.</param>
    /// <returns>The account's credentials, or the dummy's.</returns>
    internal ScramCredentials FindScramCredentials(ScramHash hash, string userName) =>
        Current.ScramAccounts[hash].GetValueOrDefault(userName) ?? new ScramCredentials(
            null,
            HMACSHA256.HashData(dummyScramSaltKey, Encoding.UTF8.GetBytes($"{hash.MechanismName}\0{userName}"))[..ScramCredentials.SaltLength],
            ScramCredentials.DefaultIterationCount,
            RandomNumberGenerator.GetBytes(hash.Length),
            RandomNumberGenerator.GetBytes(hash.Length));

    private AccountSet FilterCurrent(Func<DerivedAccount, bool> admits)
    {
        var source = store.Current;
        var cached = Volatile.Read(ref filtered);
        if (cached is not null && cached.Source == source)
        {
            return cached.Set;
        }

        var set = source.Where(admits);
        Volatile.Write(ref filtered, new FilteredAccountSet(source, set));

        return set;
    }

    private bool CheckSecret(string? userName, ReadOnlySpan<byte> secret)
    {
        byte[]? expectedHash = null;
        var known = userName is not null && Current.PasswordHashes.TryGetValue(userName, out expectedHash);
        var matches = secretComparer.FixedTimeEquals(SHA256.HashData(secret), expectedHash ?? dummyPasswordHash);

        return known && matches;
    }

    // A scheme view's set and the store's set it was filtered from.
    private sealed record FilteredAccountSet(AccountSet Source, AccountSet Set);
}
