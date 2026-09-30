using System.Diagnostics.CodeAnalysis;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The host keys the SSH server serves, at most one of each key type; the client's host-key
/// list picks one (ADR-0051, decision 4).
/// </summary>
public sealed class SshHostKeySet
{
    private readonly List<SshHostKey> keys = [];

    /// <summary>
    /// The keys held, in the order added.
    /// </summary>
    public IReadOnlyList<SshHostKey> Keys => keys;

    /// <summary>
    /// The host-key algorithms the keys held sign with, for <see cref="SshAlgorithmOffer.Default"/>.
    /// </summary>
    public IEnumerable<string> SignatureAlgorithms => keys.SelectMany(key => key.SignatureAlgorithms);

    /// <summary>
    /// Adds <paramref name="key"/> unless a key of its type is already held.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="heldKey">The key of the same type already held, when it is not added; otherwise <see langword="null"/>.</param>
    /// <returns>Whether the key was added.</returns>
    public bool TryAdd(SshHostKey key, [NotNullWhen(false)] out SshHostKey? heldKey)
    {
        ArgumentNullException.ThrowIfNull(key);

        heldKey = keys.Find(held => held.KeyType == key.KeyType);
        if (heldKey is not null)
        {
            return false;
        }

        keys.Add(key);

        return true;
    }

    /// <summary>
    /// The key that signs with <paramref name="algorithm"/>, the host-key algorithm the
    /// negotiation agreed.
    /// </summary>
    /// <param name="algorithm">The host-key algorithm.</param>
    /// <returns>The key.</returns>
    /// <exception cref="InvalidOperationException">No key held signs with it: the offer named an algorithm of a key the server does not hold.</exception>
    internal SshHostKey ForSignatureAlgorithm(string algorithm) =>
        keys.Find(key => key.SignatureAlgorithms.Contains(algorithm))
            ?? throw new InvalidOperationException($"The SSH server offered the host-key algorithm {algorithm}, but holds no key that signs with it.");
}
