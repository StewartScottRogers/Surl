using Surl.Dtls;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The <see cref="IDtlsLoginJudge"/> of one <c>coaps</c> exchange (ADR-0170 decisions 6 to 8): once the
/// client's <c>Finished</c> verified it asks the fault plan at <see cref="FaultStage.Login"/> whether a
/// <see cref="FaultKind.RefuseLogin"/> rule refuses the identity, and it publishes the
/// <see cref="LoginJudgedEvent"/> of mechanism <c>psk</c>. It never carries a key.
/// </summary>
/// <param name="context">The exchange the association belongs to.</param>
internal sealed class CoapPreSharedKeyLoginJudge(ExchangeContext context) : IDtlsLoginJudge
{
    /// <summary>The <see cref="LoginJudgedEvent.Mechanism"/> of a PSK login.</summary>
    internal const string Mechanism = "psk";

    private bool isRefusedByFault;

    /// <inheritdoc/>
    public async ValueTask<bool> ShouldRefuseAsync(string identity, CancellationToken cancellationToken)
    {
        var point = new FaultPoint(FaultStage.Login)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = identity,
        };

        if (await context.Faults.FindAsync(point, cancellationToken).ConfigureAwait(false) is not { Kind: FaultKind.RefuseLogin } rule)
        {
            return false;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Login, context.ExchangeId, identity));
        isRefusedByFault = true;

        return true;
    }

    /// <inheritdoc/>
    public void Judged(string identity, bool isAccepted)
    {
        context.Events.Publish(new LoginJudgedEvent(context.ExchangeId, context.Scheme, identity, Mechanism, isAccepted, isRefusedByFault));
        isRefusedByFault = false;
    }
}
