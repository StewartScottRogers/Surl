using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// The SRP accounts a TLS-SRP server knows, found by the exact bytes of the client's <c>srp</c>
/// extension (ADR-0122 decision 4). An unknown user gets a simulated account so the handshake goes
/// on and fails at the client's <c>Finished</c> exactly as a wrong password does (RFC 5054 section
/// 2.5.1.3): its salt is the first 16 bytes of HMAC-SHA256 over the user name keyed with a secret
/// drawn once, so a name gets the same salt every time; its group is RFC 5054's 2048-bit one; its
/// verifier is random.
/// </summary>
public sealed class TlsSrpAccounts
{
    /// <summary>The length of the simulation secret: 32 bytes.</summary>
    public const int SimulationSecretLength = 32;

    private const int SimulatedSaltLength = 16;

    private readonly Dictionary<string, SrpVerifierEntry> entries = new(StringComparer.Ordinal);
    private readonly byte[] simulationSecret;
    private readonly object gate = new();
    private readonly SrpVerifierEntry[] startEntries;

    /// <summary>Creates the accounts.</summary>
    /// <param name="entries">The known accounts; each user name once.</param>
    /// <param name="simulationSecret">The 32-byte key of unknown users' salts, drawn once per process.</param>
    /// <exception cref="ArgumentException">A user name appears twice, or the secret is not 32 bytes.</exception>
    public TlsSrpAccounts(IEnumerable<SrpVerifierEntry> entries, byte[] simulationSecret)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(simulationSecret);
        if (simulationSecret.Length != SimulationSecretLength)
        {
            throw new ArgumentException($"The simulation secret must be {SimulationSecretLength} bytes.", nameof(simulationSecret));
        }

        foreach (var entry in entries)
        {
            if (!this.entries.TryAdd(entry.UserName, entry))
            {
                throw new ArgumentException($"The SRP user '{entry.UserName}' is given twice.", nameof(entries));
            }
        }

        this.simulationSecret = simulationSecret.ToArray();
        startEntries = [.. this.entries.Values];
    }

    /// <summary>The length of the salt <see cref="Add"/> draws: 16 bytes.</summary>
    public const int AddedSaltLength = 16;

    /// <summary>
    /// Adds an account while handshakes run. The salt is drawn from <paramref name="random"/> (again
    /// while its first byte is zero, as <c>--tlsuser</c>'s is), so a seeded source gives the same
    /// verifier each run. The password is not kept.
    /// </summary>
    /// <param name="userName">The user name; not already known.</param>
    /// <param name="password">The password, hashed as UTF-8.</param>
    /// <param name="random">The salt's source.</param>
    /// <param name="group">The group of the verifier; RFC 5054's 2048-bit group when <see langword="null"/>.</param>
    /// <returns>The entry now held.</returns>
    /// <exception cref="ArgumentException">The user name is already known.</exception>
    public SrpVerifierEntry Add(string userName, string password, RandomNumberGenerator random, SrpGroup? group = null)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(random);
        var salt = new byte[AddedSaltLength];
        do
        {
            random.GetBytes(salt);
        }
        while (salt[0] == 0);

        var chosen = group ?? SrpGroup.Rfc5054Group2048;
        var verifier = Srp6a.ComputeVerifier(chosen, salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(password));
        var entry = new SrpVerifierEntry(userName, salt, Srp6a.ToUnsignedBigEndian(verifier), chosen);
        lock (gate)
        {
            if (!entries.TryAdd(userName, entry))
            {
                throw new ArgumentException($"The SRP user '{userName}' is already known.", nameof(userName));
            }
        }

        return entry;
    }

    /// <summary>
    /// Changes the password of a known account while handshakes run (ADR-0132 amendment 1). The
    /// verifier is re-derived from the new password with a fresh salt drawn as <see cref="Add"/>
    /// draws it; the account keeps its group, and the entry is swapped whole. The password is not
    /// kept. Publishing <c>AccountRemoved</c> then <c>AccountAdded</c> is the host's job, as for
    /// <see cref="Add"/>; this class has no publisher.
    /// </summary>
    /// <param name="userName">The user name; already known.</param>
    /// <param name="password">The new password, hashed as UTF-8; not empty.</param>
    /// <param name="random">The salt's source.</param>
    /// <returns>The entry now held.</returns>
    /// <exception cref="ArgumentException">The user name is not known, or the password is empty.</exception>
    public SrpVerifierEntry SetPassword(string userName, string password, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(random);
        if (password.Length == 0)
        {
            throw new ArgumentException("The password must not be empty.", nameof(password));
        }

        lock (gate)
        {
            if (!entries.TryGetValue(userName, out var existing))
            {
                throw new ArgumentException($"The SRP user '{userName}' has no account.", nameof(userName));
            }

            var salt = new byte[AddedSaltLength];
            do
            {
                random.GetBytes(salt);
            }
            while (salt[0] == 0);

            var verifier = Srp6a.ComputeVerifier(existing.Group, salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(password));
            var entry = new SrpVerifierEntry(userName, salt, Srp6a.ToUnsignedBigEndian(verifier), existing.Group);
            entries[userName] = entry;
            return entry;
        }
    }

    /// <summary>Removes an account; a later handshake for it gets a simulated one.</summary>
    /// <param name="userName">The user name.</param>
    /// <returns>Whether the account was known.</returns>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        lock (gate)
        {
            return entries.Remove(userName);
        }
    }

    /// <summary>Returns to the accounts the instance was created with.</summary>
    public void Reset()
    {
        lock (gate)
        {
            ReplaceWith(startEntries);
        }
    }

    /// <summary>Takes a snapshot: user names, salts, groups and verifiers, never a password.</summary>
    /// <returns>The snapshot, which later changes do not alter.</returns>
    public TlsSrpAccountsSnapshot TakeSnapshot()
    {
        lock (gate)
        {
            return new TlsSrpAccountsSnapshot([.. entries.Values]);
        }
    }

    /// <summary>Replaces every account with the snapshot's.</summary>
    /// <param name="snapshot">A snapshot of this or another instance.</param>
    public void Restore(TlsSrpAccountsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            ReplaceWith(snapshot.Entries);
        }
    }

    private void ReplaceWith(IEnumerable<SrpVerifierEntry> replacement)
    {
        entries.Clear();
        foreach (var entry in replacement)
        {
            entries[entry.UserName] = entry;
        }
    }

    /// <summary>
    /// Finds the account for <paramref name="userName"/>, or simulates one when the name is unknown
    /// or not UTF-8.
    /// </summary>
    /// <param name="userName">The <c>srp</c> extension's user name bytes.</param>
    /// <param name="random">The source of a simulated account's verifier.</param>
    /// <returns>The account; <see cref="TlsSrpAccount.IsSimulated"/> says which.</returns>
    public TlsSrpAccount Find(byte[] userName, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(random);
        var name = Encoding.UTF8.GetString(userName);
        SrpVerifierEntry? entry;
        lock (gate)
        {
            entries.TryGetValue(name, out entry);
        }

        if (entry is not null && Encoding.UTF8.GetBytes(name).AsSpan().SequenceEqual(userName))
        {
            return new TlsSrpAccount(entry.UserName, entry.Group, entry.Salt, entry.Verifier, IsSimulated: false);
        }

        var group = SrpGroup.Rfc5054Group2048;
        var verifier = new byte[group.PrimeLength];
        random.GetBytes(verifier);
        var salt = HMACSHA256.HashData(simulationSecret, userName)[..SimulatedSaltLength];
        return new TlsSrpAccount(name, group, salt, verifier, IsSimulated: true);
    }
}
