using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The unseeded <see cref="IIdentifierRandom"/> (ADR-0124, decision 6): every identifier's bytes come
/// from <see cref="RandomNumberGenerator"/>, so they differ every run. Stateless, so one
/// <see cref="Instance"/> serves every exchange and every instance.
/// </summary>
public sealed class CryptographicIdentifierRandom : IIdentifierRandom
{
    private CryptographicIdentifierRandom()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static CryptographicIdentifierRandom Instance { get; } = new();

    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
