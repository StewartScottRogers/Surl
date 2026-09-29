using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// Server challenges from <see cref="RandomNumberGenerator"/>.
/// </summary>
internal sealed class RandomNtlmServerChallengeSource : INtlmServerChallengeSource
{
    /// <summary>
    /// The one instance; it holds no state.
    /// </summary>
    public static readonly RandomNtlmServerChallengeSource Instance = new();

    private RandomNtlmServerChallengeSource()
    {
    }

    /// <inheritdoc/>
    public byte[] CreateServerChallenge() => RandomNumberGenerator.GetBytes(NtlmChallengeMessage.ServerChallengeLength);
}
