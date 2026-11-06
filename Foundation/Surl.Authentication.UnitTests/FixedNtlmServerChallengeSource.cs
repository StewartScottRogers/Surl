namespace Surl.Authentication;

/// <summary>
/// An <see cref="INtlmServerChallengeSource"/> that always gives the same challenge: by default
/// <c>0123456789abcdef</c>, the one the recordings in <c>Fixtures/</c> carried, so upstream
/// curl's answers can be checked.
/// </summary>
internal sealed class FixedNtlmServerChallengeSource(byte[]? challenge = null) : INtlmServerChallengeSource
{
    public static readonly byte[] FixtureChallenge = Convert.FromHexString("0123456789ABCDEF");

    public int Created { get; private set; }

    public byte[] CreateServerChallenge()
    {
        Created++;

        return [.. challenge ?? FixtureChallenge];
    }
}
