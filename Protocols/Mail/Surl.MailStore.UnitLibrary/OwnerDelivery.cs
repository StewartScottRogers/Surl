namespace Surl.MailStore;

/// <summary>
/// One copy of a message on its way into one owner's <c>INBOX</c>, with the envelope addresses
/// its delivery event names (ADR-0053 Amendment 1, decision 7).
/// </summary>
internal readonly record struct OwnerDelivery(OwnerMailboxes Owner, IReadOnlyList<string>? DeliveredFor);
