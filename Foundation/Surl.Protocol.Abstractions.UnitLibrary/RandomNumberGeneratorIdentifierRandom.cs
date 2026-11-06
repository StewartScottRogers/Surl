using System.Security.Cryptography;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.Identifiers"/>: draws from
/// <see cref="RandomNumberGenerator"/>, so a server given no seeded source generates identifiers as
/// it did before the seam existed. Stateless; <c>Surl.Core</c>'s <c>CryptographicIdentifierRandom</c>
/// is the public source of the same bytes the engine sets.
/// </summary>
internal sealed class RandomNumberGeneratorIdentifierRandom : IIdentifierRandom
{
    private RandomNumberGeneratorIdentifierRandom()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static RandomNumberGeneratorIdentifierRandom Instance { get; } = new();

    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
