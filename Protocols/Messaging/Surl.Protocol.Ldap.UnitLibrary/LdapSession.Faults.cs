using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The command-stage faults (ADR-0124 decision 4, ADR-0139, ADR-0141): each search, compare and
/// write is asked about at a <see cref="FaultStage.Command"/> point once the bind check has let it
/// through and before the directory is read. A matching <see cref="FaultKind.Delay"/> rule holds the
/// operation's response for the rule's <c>ms</c> with the idle clock paused, then answers it as
/// usual; a <see cref="FaultKind.Drop"/> rule aborts the connection with no response and ends the
/// session (<see cref="CommandStageFaults"/>). A matching <see cref="FaultKind.Error"/> rule's reply
/// - a resultCode 1 to 80 other than 10 and 14, a space and the diagnostic message - is the
/// operation's response, with no entry before it and an empty <c>matchedDN</c>.
/// </summary>
internal sealed partial class LdapSession
{
    private const string SearchOperationName = "search";
    private const string CompareOperationName = "compare";
    private const int LowestResultCode = 1;
    private const int HighestResultCode = 80;

    // The account the last bind proved, for a rule's account scope; null when unbound,
    // anonymous, or accepted unchecked.
    private string? boundAccount;

    // Set once a drop rule has aborted the connection: nothing more is answered.
    private bool isDroppedByAFault;

    // A search whose base DN an error rule matches gets the rule's result and no entry; null
    // after a drop, which sends nothing.
    private async ValueTask<LdapSearchOutcome?> SearchUnlessAFaultAnswersAsync(LdapSearchRequest search, LdapRootDseFacts rootDseFacts)
    {
        var chosen = await FindChosenResultAsync(SearchOperationName, search.BaseObject);
        if (isDroppedByAFault)
        {
            return null;
        }

        return chosen is not null ? new LdapSearchOutcome([], chosen) : directory.Search(search, rootDseFacts);
    }

    // A compare whose entry an error rule matches gets the rule's result; null after a drop.
    private async ValueTask<LdapResult?> CompareUnlessAFaultAnswersAsync(LdapCompareRequest compare)
    {
        var chosen = await FindChosenResultAsync(CompareOperationName, compare.Entry);
        return isDroppedByAFault ? null : chosen ?? directory.Compare(compare);
    }

    private LdapResult ReadOnlyRefusal(string operationName)
    {
        context.Log.Note($"LDAP {operationName} refused: the directory is read-only");
        return new LdapResult(LdapResultCode.UnwillingToPerform, string.Empty, "the directory is read-only over LDAP");
    }

    // Applies the operation's delay or drop, if any, and returns the result a matching error rule
    // chooses for it, or null to answer it as usual (or not at all after a drop).
    private async ValueTask<LdapResult?> FindChosenResultAsync(string operationName, string? distinguishedName)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = boundAccount,
            Path = distinguishedName,
            Command = operationName,
        };
        var target = distinguishedName is null ? operationName : $"{operationName} {LdapLogText.Render(distinguishedName)}";

        return await CommandStageFaults.FindAndApplyAsync(context, connection, point, target) switch
        {
            null or { Kind: FaultKind.Delay } => null,
            { Kind: FaultKind.Drop } => Dropped(),
            var rule => ChosenResult(rule, target),
        };
    }

    private LdapResult? Dropped()
    {
        isDroppedByAFault = true;
        return null;
    }

    private LdapResult? ChosenResult(FaultRule rule, string target)
    {
        if (rule.Kind != FaultKind.Error || ChosenResultOf(rule.Reply) is not { } result)
        {
            context.Log.Note($"LDAP {target}: fault rule {rule.Id} is not an error with an LDAP resultCode and diagnostic text; not applied.");
            return null;
        }

        context.Log.Note($"LDAP {target} answered by fault rule {rule.Id}: {LdapLogText.NameOf(result.ResultCode)}");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return result;
    }

    // "<resultCode> <diagnostic text>", as Surl.Cli checks an ldap rule's reply (ADR-0124, decision 4):
    // referral (10) and saslBindInProgress (14) are left out because each needs more than a code.
    private static LdapResult? ChosenResultOf(string? reply)
    {
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        if (space < 1 || space == reply!.Length - 1
            || !int.TryParse(reply.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            || code is < LowestResultCode or > HighestResultCode or 10 or 14)
        {
            return null;
        }

        return new LdapResult((LdapResultCode)code, string.Empty, reply[(space + 1)..]);
    }
}
