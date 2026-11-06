namespace Surl.MailStore;

/// <summary>
/// What <see cref="MailboxStore.Seed"/> did with one message: how the recipient resolved, as SMTP's
/// <c>RCPT TO</c> would resolve it, and how the delivery went when it resolved to owners.
/// </summary>
/// <param name="Recipient">What <see cref="MailboxStore.LookUpRecipient"/> answers for the
/// recipient: <see cref="MailRecipientLookup.Deliverable"/> when it names owners;
/// <see cref="MailRecipientLookup.NoSuchAccount"/>, which SMTP accepts and discards;
/// <see cref="MailRecipientLookup.InvalidAddress"/> or
/// <see cref="MailRecipientLookup.RelayingDenied"/>, which SMTP refuses at <c>RCPT</c>.</param>
/// <param name="Delivery">How the delivery went - <see cref="MailStoreOutcome.Succeeded"/>,
/// <see cref="MailStoreOutcome.MessageTooLarge"/>, <see cref="MailStoreOutcome.QuotaExceeded"/>,
/// <see cref="MailStoreOutcome.StorageFailed"/> or <see cref="MailStoreOutcome.StoreFull"/> - when
/// <paramref name="Recipient"/> is <see cref="MailRecipientLookup.Deliverable"/>; otherwise
/// <see langword="null"/>, and nothing was stored.</param>
public sealed record MailSeedResult(MailRecipientLookup Recipient, MailStoreOutcome? Delivery);
