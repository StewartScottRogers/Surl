using System.Security.Cryptography;

namespace Surl.Protocol.Smb;

/// <summary>
/// The challenge source a served SMB connection uses: the operating system's cryptographic
/// generator, <see cref="RandomNumberGenerator"/> (ADR-0073, decision 1).
/// </summary>
public sealed class SmbSystemChallengeSource : ISmbChallengeSource
{
    /// <inheritdoc/>
    public void Fill(Span<byte> challenge) => RandomNumberGenerator.Fill(challenge);
}
