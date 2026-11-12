using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The <see cref="IIdentifierRandom"/> a <see cref="NatsServer"/> draws its server ID from when it is given
/// none: bytes from <see cref="RandomNumberGenerator"/>, different every run. A protocol server may not
/// reference <c>Surl.Core</c>, so it cannot use that library's equivalent.
/// </summary>
internal sealed class NatsUnseededIdentifierRandom : IIdentifierRandom
{
    private NatsUnseededIdentifierRandom()
    {
    }

    /// <summary>The one instance; it keeps no state.</summary>
    public static NatsUnseededIdentifierRandom Instance { get; } = new();

    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
