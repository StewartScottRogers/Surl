using Surl.Protocol.Abstractions;

namespace Surl.MailStore;

/// <summary>
/// What the store publishes on the event stream and how it answers an out-of-space fault rule
/// (ADR-0124 decisions 1 and 4; ADR-0126).
/// </summary>
public sealed partial class MailboxStore
{
    /// <summary>
    /// Publishes one change to one message, named by <see cref="MailStoreChangeName.ForMessage"/>,
    /// with the message's byte count. Called under the store's lock, so the events keep the order
    /// in which the changes happened.
    /// </summary>
    private void PublishMessage(StoreChange change, MailboxPlace place, uint uid) =>
        events.Publish(new StoreChangedEvent(
            StoreKind.Mail,
            change,
            MailStoreChangeName.ForMessage(place.Owner.Name, place.Mailbox.Name, uid),
            place.Mailbox.Messages[uid].Body.Length,
            ExchangeId: null));

    /// <summary>
    /// Publishes one change to one mailbox, named by <see cref="MailStoreChangeName.ForMailbox"/>,
    /// with no byte count.
    /// </summary>
    private void PublishMailbox(StoreChange change, OwnerMailboxes owner, StoredMailbox mailbox) =>
        events.Publish(new StoreChangedEvent(
            StoreKind.Mail,
            change,
            MailStoreChangeName.ForMailbox(owner.Name, mailbox.Name),
            ByteCount: null,
            ExchangeId: null));

    /// <summary>
    /// Publishes <see cref="StoreChange.Deleted"/> for each of <paramref name="uids"/>, all held by
    /// the mailbox at <paramref name="place"/>, before they are removed.
    /// </summary>
    private void PublishRemoving(MailboxPlace place, IReadOnlyList<uint> uids)
    {
        foreach (var uid in uids)
        {
            PublishMessage(StoreChange.Deleted, place, uid);
        }
    }

    /// <summary>
    /// Removes <paramref name="uids"/>, all held by the mailbox at <paramref name="place"/>,
    /// publishing each, and counts one change when any was removed.
    /// </summary>
    /// <returns>How many were removed.</returns>
    private int RemovePublished(MailboxPlace place, IReadOnlyList<uint> uids)
    {
        PublishRemoving(place, uids);
        var removed = RemoveMessages(place.Mailbox, uids);
        changeCount += removed == 0 ? 0 : 1;
        return removed;
    }

    /// <summary>
    /// The owner and mailbox a write to <paramref name="mailboxName"/> in the view would land in,
    /// or none when the view has no such mailbox.
    /// </summary>
    private IEnumerable<(string OwnerName, string MailboxName)> TargetOf(MailView view, string mailboxName)
    {
        lock (storeLock)
        {
            var place = FindPlace(view, mailboxName);
            return place is null ? [] : [(place.Value.Owner.Name, place.Value.Mailbox.Name)];
        }
    }

    /// <summary>
    /// Whether an out-of-space rule of the fault plan matches a write to any of
    /// <paramref name="targets"/> (ADR-0124 decision 4): each is asked as a
    /// <see cref="FaultStage.StoreWrite"/> point whose account is the owner and whose path is the
    /// mailbox, in turn until one matches, and the rule that matches is published as a
    /// <see cref="FaultAppliedEvent"/> naming the mailbox. Called outside the store's lock, since
    /// the plan may run a test's code.
    /// </summary>
    private bool IsOutOfSpace(IEnumerable<(string OwnerName, string MailboxName)> targets)
    {
        foreach (var (ownerName, mailboxName) in targets)
        {
            var rule = FindOutOfSpaceRule(new FaultPoint(FaultStage.StoreWrite) { Account = ownerName, Path = mailboxName });
            if (rule is not null)
            {
                events.Publish(new FaultAppliedEvent(
                    rule.Id,
                    FaultKind.OutOfSpace,
                    FaultStage.StoreWrite,
                    ExchangeId: null,
                    MailStoreChangeName.ForMailbox(ownerName, mailboxName)));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The plan's out-of-space rule for <paramref name="point"/>, or <see langword="null"/> when it
    /// names none or a rule of another kind. The store's writes are synchronous, so it takes only an
    /// answer the plan gives at once - every plan does when no hook is registered - and serves a
    /// point whose answer is still pending, or that failed, as if no rule matched, as ADR-0124
    /// decision 5 serves a hook that times out or throws (ADR-0126).
    /// </summary>
    private FaultRule? FindOutOfSpaceRule(FaultPoint point)
    {
        var answer = faults.FindAsync(point, CancellationToken.None);
        if (!answer.IsCompletedSuccessfully)
        {
            // Observed, so a later failure is never an unobserved task exception.
            _ = answer.AsTask().ContinueWith(
                static pending => pending.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return null;
        }

        // Completed already, so reading the result does not block.
        var rule = answer.Result;
        return rule?.Kind == FaultKind.OutOfSpace ? rule : null;
    }
}
