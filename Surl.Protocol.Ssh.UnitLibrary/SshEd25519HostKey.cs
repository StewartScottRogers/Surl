using System.Security.Cryptography;
using Surl.Cryptography.Ed25519;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An Ed25519 host key: an <c>ssh-ed25519</c> blob (<c>string "ssh-ed25519"</c>,
/// <c>string</c> the 32-byte public key) that signs as <c>ssh-ed25519</c>, the signature the
/// 64 bytes of RFC 8032 (RFC 8709, sections 4 and 6), with the hand-built
/// <see cref="Ed25519"/> (ADR-0048).
/// </summary>
internal sealed class SshEd25519HostKey : SshHostKey
{
    /// <summary>The key type, which is also its one host-key algorithm.</summary>
    public const string Ed25519KeyType = "ssh-ed25519";

    private readonly byte[] seed;

    /// <summary>
    /// Creates the host key from its private key, which is copied.
    /// </summary>
    /// <param name="seed">The <see cref="Ed25519.SeedSize"/>-byte private key.</param>
    /// <param name="publicKey">Its public key, computed by <see cref="Ed25519.ComputePublicKey"/>.</param>
    private SshEd25519HostKey(byte[] seed, byte[] publicKey)
        : base(Ed25519KeyType, Blob(publicKey), [Ed25519KeyType])
    {
        this.seed = seed;
    }

    /// <summary>
    /// The host key of <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">The private key.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="CryptographicException">The seed is not <see cref="Ed25519.SeedSize"/> bytes.</exception>
    public static SshEd25519HostKey FromSeed(ReadOnlySpan<byte> seed) =>
        seed.Length != Ed25519.SeedSize
            ? throw new CryptographicException($"An Ed25519 private key is {Ed25519.SeedSize} bytes.")
            : new SshEd25519HostKey(seed.ToArray(), Ed25519.ComputePublicKey(seed));

    /// <inheritdoc/>
    private protected override byte[] SignRaw(string algorithm, byte[] data) => Ed25519.Sign(seed, data);

    private static byte[] Blob(byte[] publicKey)
    {
        var blob = new SshWireWriter();
        blob.WriteString(Ed25519KeyType);
        blob.WriteString(publicKey);

        return blob.ToArray();
    }
}
