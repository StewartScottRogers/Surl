using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The changing account set an <see cref="AccountBook"/> and every scheme view of it read: the
/// accounts configured at start, and the set as tests have since added, removed, reset and
/// restored it (ADR-0124, decision 3). Each change derives what it must outside the lock, then
/// swaps in a new <see cref="AccountSet"/> under the lock and publishes one
/// <see cref="StoreChangedEvent"/> naming at most a user name, so the events keep the changes'
/// order and a login in flight reads either the set before or the set after.
/// </summary>
internal sealed class AccountStore
{
    private readonly Lock changeLock = new();
    private readonly IReadOnlyList<DerivedAccount> configured;
    private readonly ISurlEventPublisher events;
    private readonly Func<ScramHash, byte[]> createScramSalt;
    private AccountSet current;

    /// <summary>
    /// Derives every account of <paramref name="accounts"/>; they are the accounts a reset returns to.
    /// </summary>
    /// <param name="accounts">The configured accounts; no user name twice.</param>
    /// <param name="events">Where each change is published.</param>
    /// <param name="createScramSalt">Makes one account's SCRAM salt under one hash; random when <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A user name is given twice.</exception>
    public AccountStore(IEnumerable<Account> accounts, ISurlEventPublisher events, Func<ScramHash, byte[]>? createScramSalt)
    {
        this.events = events;
        this.createScramSalt = createScramSalt ?? (_ => RandomNumberGenerator.GetBytes(ScramCredentials.SaltLength));
        var names = new HashSet<string>(StringComparer.Ordinal);
        var derived = new List<DerivedAccount>();
        foreach (var account in accounts)
        {
            if (!names.Add(account.UserName))
            {
                throw new ArgumentException($"User {account.UserName} is given twice.", nameof(accounts));
            }

            derived.Add(Derive(account));
        }

        configured = derived;
        current = new AccountSet(configured);
    }

    /// <summary>
    /// Random user hashes, one per <see cref="DigestAlgorithm"/>, that match no answer: the second
    /// set of a Digest entry with no ISO-8859-1 spelling, and an unknown name's.
    /// </summary>
    public IReadOnlyList<string> DummyUserHashes { get; } = [.. Enum.GetValues<DigestAlgorithm>()
        .Select(algorithm => DigestCalculation.HashHex(algorithm, RandomNumberGenerator.GetBytes(32)))];

    /// <summary>
    /// The set as it is now.
    /// </summary>
    public AccountSet Current => Volatile.Read(ref current);

    /// <summary>
    /// Adds <paramref name="account"/>, deriving its credentials exactly as for an account read at start.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <exception cref="ArgumentException">An account of that user name exists.</exception>
    public void Add(Account account)
    {
        var derived = Derive(account);
        lock (changeLock)
        {
            if (current.PasswordHashes.ContainsKey(account.UserName))
            {
                throw new ArgumentException($"User {account.UserName} already has an account.", nameof(account));
            }

            Swap([.. current.Accounts, derived], StoreChange.AccountAdded, account.UserName);
        }
    }

    /// <summary>
    /// Removes the account of <paramref name="userName"/>, if there is one.
    /// </summary>
    /// <param name="userName">The user name, compared ordinally.</param>
    /// <returns><see langword="true"/> when an account was removed.</returns>
    public bool Remove(string userName)
    {
        lock (changeLock)
        {
            if (!current.PasswordHashes.ContainsKey(userName))
            {
                return false;
            }

            Swap([.. current.Accounts.Where(account => account.UserName != userName)], StoreChange.AccountRemoved, userName);

            return true;
        }
    }

    /// <summary>
    /// Returns the set to exactly the accounts configured at start.
    /// </summary>
    public void Reset()
    {
        lock (changeLock)
        {
            Swap(configured, StoreChange.Reset, string.Empty);
        }
    }

    /// <summary>
    /// Copies the set as it is now.
    /// </summary>
    /// <returns>The snapshot.</returns>
    public AccountSnapshot TakeSnapshot() => new(this, Current.Accounts);

    /// <summary>
    /// Replaces the set with <paramref name="snapshot"/>'s accounts.
    /// </summary>
    /// <param name="snapshot">A snapshot taken of this store.</param>
    /// <exception cref="ArgumentException">The snapshot was taken of another account book.</exception>
    public void Restore(AccountSnapshot snapshot)
    {
        if (snapshot.Store != this)
        {
            throw new ArgumentException("The snapshot was taken of another account book.", nameof(snapshot));
        }

        lock (changeLock)
        {
            Swap(snapshot.Accounts, StoreChange.Restored, string.Empty);
        }
    }

    private void Swap(IReadOnlyList<DerivedAccount> accounts, StoreChange change, string name)
    {
        Volatile.Write(ref current, new AccountSet(accounts));
        events.Publish(new StoreChangedEvent(StoreKind.Accounts, change, name, null, null));
    }

    private DerivedAccount Derive(Account account) =>
        new(
            account.UserName,
            account.Schemes,
            SHA256.HashData(Encoding.UTF8.GetBytes(account.Password)),
            account.UserName.Length > 0 ? DeriveNamed(account) : null);

    private NamedAccountCredentials DeriveNamed(Account account)
    {
        var password = Encoding.UTF8.GetBytes(account.Password);

        return new NamedAccountCredentials(
            DeriveDigest(account),
            DeriveIso88591Digest(account),
            new NtlmAccount(account.UserName, NtlmPasswordHashes.Compute(account.Password)),
            new AwsSigV4Account(account.UserName, password),
            DeriveAzureStorage(account),
            new ChallengeResponseAccount(account.UserName, password),
            new Dictionary<ScramHash, ScramCredentials>
            {
                [ScramHash.Sha1] = DeriveScram(ScramHash.Sha1, account),
                [ScramHash.Sha256] = DeriveScram(ScramHash.Sha256, account),
            });
    }

    // An ASCII name is its own ISO-8859-1 spelling, so its entry carries both sets of hashes (ADR-0036).
    private KeyValuePair<string, DigestAccount> DeriveDigest(Account account)
    {
        var utf8Spelling = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(account.UserName));
        var iso88591Hashes = utf8Spelling == account.UserName && IsIso88591Only(account)
            ? CreateUserHashes(account, Encoding.Latin1)
            : DummyUserHashes;

        return new(utf8Spelling, new DigestAccount(account.UserName, [CreateUserHashes(account, Encoding.UTF8), iso88591Hashes]));
    }

    private KeyValuePair<string, DigestAccount>? DeriveIso88591Digest(Account account) =>
        IsIso88591Only(account) && account.UserName.Any(character => character > '\u007f')
            ? new(account.UserName, new DigestAccount(account.UserName, [CreateUserHashes(account, Encoding.Latin1), DummyUserHashes]))
            : null;

    // A named account whose password is the base64 text of a key signs Shared Key and SAS with it.
    private static AzureStorageAccount? DeriveAzureStorage(Account account)
    {
        var key = new byte[account.Password.Length];

        return Convert.TryFromBase64String(account.Password, key, out var length) && length > 0
            ? new AzureStorageAccount(account.UserName, key[..length])
            : null;
    }

    private ScramCredentials DeriveScram(ScramHash hash, Account account) =>
        ScramCredentials.Derive(hash, account.UserName, account.Password, createScramSalt(hash), ScramCredentials.DefaultIterationCount);

    private static IReadOnlyList<string> CreateUserHashes(Account account, Encoding encoding) =>
        [.. Enum.GetValues<DigestAlgorithm>().Select(algorithm => DigestCalculation.ComputeUserHash(
            algorithm, encoding, account.UserName, DigestAuthenticationMethod.Realm, account.Password))];

    private static bool IsIso88591Only(Account account) =>
        !$"{account.UserName}{account.Password}".Any(character => character > 'ÿ');
}
