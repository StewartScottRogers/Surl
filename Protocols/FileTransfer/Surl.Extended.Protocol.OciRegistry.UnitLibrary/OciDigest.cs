using System.Security.Cryptography;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A content digest, <c>&lt;algorithm&gt;:&lt;encoded&gt;</c>, of one of the image
/// specification's two registered algorithms: <c>sha256</c> with 64 lower-case hexadecimal
/// digits, or <c>sha512</c> with 128 (Image "Descriptors"; ADR-0115, decision 3).
/// </summary>
internal sealed class OciDigest
{
    private OciDigest(string algorithm, string encoded)
    {
        Algorithm = algorithm;
        Encoded = encoded;
    }

    /// <summary>
    /// The algorithm, <c>sha256</c> or <c>sha512</c>.
    /// </summary>
    public string Algorithm { get; }

    /// <summary>
    /// The hash as lower-case hexadecimal digits.
    /// </summary>
    public string Encoded { get; }

    /// <summary>
    /// Reads a digest.
    /// </summary>
    /// <param name="text">The digest as sent.</param>
    /// <returns>The digest, or <see langword="null"/> when it does not parse or names another algorithm.</returns>
    public static OciDigest? Parse(string text)
    {
        var colon = text.IndexOf(':');
        if (colon < 0)
        {
            return null;
        }

        var algorithm = text[..colon];
        var encoded = text[(colon + 1)..];

        return encoded.Length == EncodedLength(algorithm) && IsLowerHex(encoded) ? new OciDigest(algorithm, encoded) : null;
    }

    /// <summary>
    /// Starts a hash of this digest's algorithm.
    /// </summary>
    /// <returns>The hash.</returns>
    public IncrementalHash CreateHash() =>
        IncrementalHash.CreateHash(Algorithm == "sha256" ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512);

    /// <summary>
    /// Whether a finished hash is this digest.
    /// </summary>
    /// <param name="hash">The hash of the bytes read.</param>
    /// <returns><see langword="true"/> when the hashes are equal.</returns>
    public bool IsHashOf(ReadOnlySpan<byte> hash) =>
        string.Equals(Convert.ToHexStringLower(hash), Encoded, StringComparison.Ordinal);

    /// <summary>
    /// The path, under the content store, of the blob file of this digest in a repository's
    /// OCI Image Layout: <c>/&lt;name&gt;/blobs/&lt;algorithm&gt;/&lt;encoded&gt;</c>.
    /// </summary>
    /// <param name="repositoryName">The repository's name.</param>
    /// <returns>The request path.</returns>
    public string BlobPathIn(string repositoryName) => $"/{repositoryName}/blobs/{Algorithm}/{Encoded}";

    /// <inheritdoc/>
    public override string ToString() => $"{Algorithm}:{Encoded}";

    private static int EncodedLength(string algorithm) => algorithm switch
    {
        "sha256" => 64,
        "sha512" => 128,
        _ => -1,
    };

    private static bool IsLowerHex(string encoded) =>
        encoded.AsSpan().IndexOfAnyExcept("0123456789abcdef") < 0;
}
