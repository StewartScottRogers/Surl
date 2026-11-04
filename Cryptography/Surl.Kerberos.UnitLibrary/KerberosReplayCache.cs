using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// The authenticators surl has accepted recently, so a replayed one is refused (ADR-0057
/// decision 7): the SHA-256 of each accepted authenticator's cipher text, held until its
/// <c>ctime</c> plus the clock skew has passed on the <see cref="TimeProvider" />, after which the
/// skew check refuses it anyway. One per process, shared by every listener, so it is safe to use
/// from several threads.
/// </summary>
/// <param name="timeProvider">The clock expiry is measured on.</param>
public sealed class KerberosReplayCache(TimeProvider timeProvider)
{
    /// <summary>
    /// The most authenticators held at once. When full, a new AP-REQ is refused rather than an
    /// entry evicted, since evicting one would let its replay through.
    /// </summary>
    public const int Capacity = 65536;

    private readonly Lock gate = new();
    private readonly HashSet<string> hashes = [];
    private readonly PriorityQueue<string, DateTimeOffset> hashesByDropTime = new();

    /// <summary>Gets how many authenticators are held, counting any whose time has passed but that no add has dropped yet.</summary>
    internal int Count
    {
        get
        {
            lock (gate)
            {
                return hashes.Count;
            }
        }
    }

    /// <summary>
    /// Drops every entry whose time has passed, then adds the authenticator unless it is held
    /// already or the cache is full.
    /// </summary>
    /// <param name="authenticatorCipherText">The authenticator's cipher text, as the AP-REQ carried it.</param>
    /// <param name="clientTime">The authenticator's <c>ctime</c> and <c>cusec</c>.</param>
    /// <returns>Whether it was added, was a replay, or found the cache full.</returns>
    internal KerberosReplayCacheOutcome TryAdd(ReadOnlySpan<byte> authenticatorCipherText, DateTimeOffset clientTime)
    {
        string hash = Convert.ToHexString(SHA256.HashData(authenticatorCipherText));
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (gate)
        {
            DropPassedEntries(now);
            if (hashes.Contains(hash))
            {
                return KerberosReplayCacheOutcome.Replayed;
            }

            if (hashes.Count >= Capacity)
            {
                return KerberosReplayCacheOutcome.Full;
            }

            hashes.Add(hash);
            hashesByDropTime.Enqueue(hash, clientTime + KerberosAcceptor.ClockSkew);
            return KerberosReplayCacheOutcome.Added;
        }
    }

    private void DropPassedEntries(DateTimeOffset now)
    {
        while (hashesByDropTime.TryPeek(out string? hash, out DateTimeOffset dropTime) && dropTime < now)
        {
            hashesByDropTime.Dequeue();
            hashes.Remove(hash);
        }
    }
}
