namespace Surl.Authentication;

/// <summary>
/// One unchanging set of accounts with every lookup a login makes built over it. A change to the
/// accounts builds a new set and swaps it in whole, so a login in flight reads one consistent set
/// and never a dictionary being written (ADR-0124, decision 3).
/// </summary>
internal sealed class AccountSet
{
    /// <summary>
    /// Builds the lookups over <paramref name="accounts"/>, whose names are all different.
    /// </summary>
    /// <param name="accounts">The accounts, in the order they were configured or added.</param>
    public AccountSet(IReadOnlyList<DerivedAccount> accounts)
    {
        Accounts = accounts;
        foreach (var account in accounts)
        {
            PasswordHashes[account.UserName] = account.PasswordHash;
            if (account.Named is { } named)
            {
                AddNamed(account.UserName, named);
            }
        }

        // Every UTF-8 spelling first, so an ISO-8859-1 spelling never shadows one (ADR-0036).
        foreach (var iso88591Digest in accounts.Select(account => account.Named?.Iso88591Digest).OfType<KeyValuePair<string, DigestAccount>>())
        {
            DigestAccounts.TryAdd(iso88591Digest.Key, iso88591Digest.Value);
        }
    }

    /// <summary>
    /// The accounts, in the order they were configured or added.
    /// </summary>
    public IReadOnlyList<DerivedAccount> Accounts { get; }

    /// <summary>
    /// Each account's password SHA-256, by name.
    /// </summary>
    public Dictionary<string, byte[]> PasswordHashes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account's Digest entries, by spelling.
    /// </summary>
    public Dictionary<string, DigestAccount> DigestAccounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account's NT hashes, by name.
    /// </summary>
    public Dictionary<string, NtlmAccount> NtlmAccounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account's Signature Version 4 secret, by name.
    /// </summary>
    public Dictionary<string, AwsSigV4Account> AwsSigV4Accounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account whose password is base64, with its Azure Storage key, by name.
    /// </summary>
    public Dictionary<string, AzureStorageAccount> AzureStorageAccounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account's challenge-response secret, by name.
    /// </summary>
    public Dictionary<string, ChallengeResponseAccount> ChallengeResponseAccounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Each named account's SCRAM credentials, by hash and then by name.
    /// </summary>
    public Dictionary<ScramHash, Dictionary<string, ScramCredentials>> ScramAccounts { get; } = new()
    {
        [ScramHash.Sha1] = new(StringComparer.Ordinal),
        [ScramHash.Sha256] = new(StringComparer.Ordinal),
    };

    /// <summary>
    /// The set of the accounts <paramref name="isAdmitted"/> admits, sharing every derived value.
    /// </summary>
    /// <param name="isAdmitted">Whether an account stays.</param>
    /// <returns>The new set.</returns>
    public AccountSet Where(Func<DerivedAccount, bool> isAdmitted) => new([.. Accounts.Where(isAdmitted)]);

    private void AddNamed(string userName, NamedAccountCredentials named)
    {
        DigestAccounts[named.Digest.Key] = named.Digest.Value;
        NtlmAccounts[userName] = named.Ntlm;
        AwsSigV4Accounts[userName] = named.AwsSigV4;
        ChallengeResponseAccounts[userName] = named.ChallengeResponse;
        foreach (var (hash, credentials) in named.Scram)
        {
            ScramAccounts[hash][userName] = credentials;
        }

        if (named.AzureStorage is { } azureStorage)
        {
            AzureStorageAccounts[userName] = azureStorage;
        }
    }
}
