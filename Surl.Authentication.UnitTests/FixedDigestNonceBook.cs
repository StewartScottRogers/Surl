namespace Surl.Authentication;

/// <summary>
/// An <see cref="IDigestNonceBook"/> that knows one nonce, in the state a test sets: the
/// nonce the recordings in <c>Fixtures/</c> offered, so upstream curl's answers can be replayed.
/// </summary>
internal sealed class FixedDigestNonceBook(string nonce, DigestNonceState state = DigestNonceState.Fresh)
    : IDigestNonceBook
{
    public const string FixtureNonce = "fixturenonce";

    public List<uint> RecordedUses { get; } = [];

    public string Issue() => nonce;

    public DigestNonceState Check(string answered) => answered == nonce ? state : DigestNonceState.Unknown;

    public bool TryRecordUse(string answered, uint nonceCount)
    {
        RecordedUses.Add(nonceCount);

        return true;
    }
}
