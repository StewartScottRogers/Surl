using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The identifier source a server uses for a connection refusal when none is given: the bytes come
/// from <see cref="RandomNumberGenerator"/>, so unseeded servers answer different request IDs.
/// </summary>
internal sealed class AzureUnseededIdentifierRandom : IIdentifierRandom
{
    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
