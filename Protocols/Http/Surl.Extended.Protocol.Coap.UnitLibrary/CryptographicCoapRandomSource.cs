using System.Security.Cryptography;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The production <see cref="ICoapRandomSource"/>: every draw from <see cref="RandomNumberGenerator"/>.
/// </summary>
internal sealed class CryptographicCoapRandomSource : ICoapRandomSource
{
    private const int FractionSteps = 1 << 30;

    /// <summary>The one instance; it holds no state.</summary>
    public static CryptographicCoapRandomSource Instance { get; } = new();

    /// <inheritdoc/>
    public ushort DrawMessageId() => (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);

    /// <inheritdoc/>
    public double DrawFraction() => RandomNumberGenerator.GetInt32(FractionSteps) / (double)FractionSteps;

    /// <inheritdoc/>
    public void Fill(Span<byte> bytes) => RandomNumberGenerator.Fill(bytes);
}
