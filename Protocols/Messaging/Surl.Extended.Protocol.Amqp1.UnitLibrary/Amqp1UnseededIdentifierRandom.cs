using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The identifier source a container uses when none is given: the bytes come from
/// <see cref="RandomNumberGenerator"/>, so unseeded containers get different identifiers.
/// </summary>
internal sealed class Amqp1UnseededIdentifierRandom : IIdentifierRandom
{
    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
