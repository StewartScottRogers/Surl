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
        if (entries.TryGetValue(name, out var entry) && Encoding.UTF8.GetBytes(name).AsSpan().SequenceEqual(userName))
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
