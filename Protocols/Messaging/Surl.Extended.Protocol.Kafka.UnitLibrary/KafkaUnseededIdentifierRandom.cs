using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The <see cref="IIdentifierRandom"/> a <see cref="KafkaMetadataAnswerer"/> draws its cluster id from when it
/// is given none: bytes from <see cref="RandomNumberGenerator"/>, different every run. A protocol server may
/// not reference <c>Surl.Core</c>, so it cannot use that library's equivalent.
/// </summary>
internal sealed class KafkaUnseededIdentifierRandom : IIdentifierRandom
{
    private KafkaUnseededIdentifierRandom()
    {
    }

    /// <summary>The one instance; it keeps no state.</summary>
    public static KafkaUnseededIdentifierRandom Instance { get; } = new();

    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
