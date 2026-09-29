namespace Surl.Authentication;

/// <summary>
/// Where NTLM's server challenges come from: random in production
/// (<see cref="RandomNtlmServerChallengeSource"/>), fixed in tests so an answer recorded from
/// upstream curl can be checked.
/// </summary>
internal interface INtlmServerChallengeSource
{
    /// <summary>
    /// A new server challenge.
    /// </summary>
    /// <returns><see cref="NtlmChallengeMessage.ServerChallengeLength"/> bytes.</returns>
    byte[] CreateServerChallenge();
}
