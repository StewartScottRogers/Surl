using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The engine's <see cref="ITlsLoginJudge"/> for one exchange (ADR-0165, decisions 2, 4 and 5): asks the
/// fault plan at <see cref="FaultStage.Login"/> whether a login whose credentials matched is refused,
/// and publishes the <see cref="LoginJudgedEvent"/> of mechanism <c>tls-srp</c>. It never carries a secret.
/// </summary>
/// <param name="context">The exchange the connection belongs to.</param>
internal sealed class ExchangeTlsLoginJudge(ExchangeContext context) : ITlsLoginJudge
{
    /// <summary>
    /// The <see cref="LoginJudgedEvent.Mechanism"/> of a TLS-SRP login.
    /// </summary>
    internal const string Mechanism = "tls-srp";

    private bool isRefusedByFault;

    /// <inheritdoc/>
    public async ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var point = new FaultPoint(FaultStage.Login)
        {
            Scheme = context.ListenUrl.Scheme,
            Account = userName,
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
        };

        if (await context.Faults.FindAsync(point, cancellationToken).ConfigureAwait(false) is not { Kind: FaultKind.RefuseLogin } rule)
        {
            return false;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Login, context.ExchangeId, userName));
        isRefusedByFault = true;
        return true;
    }

    /// <inheritdoc/>
    public void Judged(string userName, bool isAccepted)
    {
        ArgumentNullException.ThrowIfNull(userName);

        context.Events.Publish(new LoginJudgedEvent(
            context.ExchangeId, context.ListenUrl.Scheme, userName, Mechanism, isAccepted, isRefusedByFault));
        isRefusedByFault = false;
    }
}
