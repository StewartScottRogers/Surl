using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Networking;

/// <summary>
/// Hands <see cref="TlsSrpServer"/>, which references no Surl project, the connection's
/// <see cref="ITlsLoginJudge"/> as the <see cref="ITlsSrpLoginJudge"/> it asks (ADR-0165, decision 2).
/// </summary>
/// <param name="judge">The judge the engine gave the connection.</param>
internal sealed class TlsSrpLoginJudgeAdapter(ITlsLoginJudge judge) : ITlsSrpLoginJudge
{
    /// <inheritdoc/>
    public ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken) =>
        judge.ShouldRefuseAsync(userName, cancellationToken);

    /// <inheritdoc/>
    public void Judged(string userName, bool isAccepted) => judge.Judged(userName, isAccepted);
}
