using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// SASL nonces from <see cref="RandomNumberGenerator"/>.
/// </summary>
internal sealed class RandomSaslNonceSource : ISaslNonceSource
{
    /// <summary>
    /// The one instance; it holds no state.
    /// </summary>
    public static readonly RandomSaslNonceSource Instance = new();

    private RandomSaslNonceSource()
    {
    }

    /// <inheritdoc/>
    public byte[] CreateNonce(int length) => RandomNumberGenerator.GetBytes(length);
}
