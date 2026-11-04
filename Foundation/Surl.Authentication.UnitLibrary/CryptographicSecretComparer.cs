using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// The <see cref="ISecretComparer"/> every account check uses outside tests:
/// <see cref="CryptographicOperations.FixedTimeEquals"/>.
/// </summary>
internal sealed class CryptographicSecretComparer : ISecretComparer
{
    /// <summary>
    /// The one instance; it holds no state.
    /// </summary>
    public static readonly CryptographicSecretComparer Instance = new();

    private CryptographicSecretComparer()
    {
    }

    /// <inheritdoc/>
    public bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
        CryptographicOperations.FixedTimeEquals(left, right);
}
