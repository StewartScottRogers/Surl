namespace Surl.TlsSrp;

/// <summary>A hand-written judge that records every call in order and refuses when told to.</summary>
internal sealed class UnitTestRecordingLoginJudge(bool refuse = false) : ITlsSrpLoginJudge
{
    public List<string> Calls { get; } = [];

    public ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken)
    {
        Calls.Add($"ask:{userName}");
        return ValueTask.FromResult(refuse);
    }

    public void Judged(string userName, bool isAccepted) => Calls.Add($"judged:{userName}:{isAccepted}");
}
