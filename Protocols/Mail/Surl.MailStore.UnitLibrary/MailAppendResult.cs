namespace Surl.MailStore;

/// <summary>
/// What <see cref="MailboxStore.AppendAsync"/> gives back: the outcome, and where the message was
/// stored when the outcome is <see cref="MailStoreOutcome.Succeeded"/>.
/// </summary>
/// <param name="Outcome">How the append ended.</param>
/// <param name="Stored">Where the message was stored; <see langword="default"/> unless it was.</param>
public readonly record struct MailAppendResult(MailStoreOutcome Outcome, MailStoredUid Stored);
