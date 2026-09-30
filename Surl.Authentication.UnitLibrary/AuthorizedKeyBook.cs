using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// The public keys each user may log in to the SSH server with (ADR-0051, section 6), checked as
/// ADR-0032 section 8 checks a password: each key is kept as the SHA-256 of its blob, and a key
/// sent is compared as the SHA-256 of its exact blob against every key of the user it names with
/// <see cref="CryptographicOperations.FixedTimeEquals"/>. An unknown user is compared against a
/// random dummy hash, so "no such user", "key not authorized" and "no keys configured" answer
/// the same.
/// </summary>
public sealed class AuthorizedKeyBook
{
    private readonly Dictionary<string, List<byte[]>> keyHashes = new(StringComparer.Ordinal);
    private readonly List<byte[]> dummyKeyHashes = [SHA256.HashData(RandomNumberGenerator.GetBytes(32))];
    private readonly ISecretComparer secretComparer;

    /// <summary>
    /// Keeps <paramref name="keys"/>, which <see cref="AuthorizedKeysParser"/> has already read.
    /// </summary>
    /// <param name="keys">Every key from every <c>--authorized-keys</c>; a user may have many.</param>
    public AuthorizedKeyBook(IEnumerable<AuthorizedKey> keys)
        : this(keys, CryptographicSecretComparer.Instance)
    {
    }

    internal AuthorizedKeyBook(IEnumerable<AuthorizedKey> keys, ISecretComparer secretComparer)
    {
        ArgumentNullException.ThrowIfNull(keys);

        this.secretComparer = secretComparer;
        foreach (var key in keys)
        {
            if (!keyHashes.TryGetValue(key.UserName, out var hashes))
            {
                hashes = [];
                keyHashes.Add(key.UserName, hashes);
            }

            hashes.Add(SHA256.HashData(key.Blob.Span));
        }
    }

    /// <summary>
    /// The book with no keys: every public-key login is refused.
    /// </summary>
    public static AuthorizedKeyBook Empty { get; } = new([]);

    /// <summary>
    /// Whether <paramref name="publicKeyBlob"/> is one of <paramref name="userName"/>'s keys. Every
    /// key of the user is compared, whichever matches.
    /// </summary>
    /// <param name="userName">The user name as sent, or <see langword="null"/> when it was not UTF-8.</param>
    /// <param name="publicKeyBlob">The public key blob as sent (RFC 4253 section 6.6).</param>
    /// <returns><see langword="true"/> only when the user has exactly that key.</returns>
    public bool IsAuthorized(string? userName, ReadOnlySpan<byte> publicKeyBlob)
    {
        List<byte[]>? hashes = null;
        var known = userName is not null && keyHashes.TryGetValue(userName, out hashes);
        var sentHash = SHA256.HashData(publicKeyBlob);
        var matches = false;
        foreach (var hash in hashes ?? dummyKeyHashes)
        {
            matches |= secretComparer.FixedTimeEquals(sentHash, hash);
        }

        return known && matches;
    }
}
