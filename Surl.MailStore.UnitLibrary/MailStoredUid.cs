namespace Surl.MailStore;

/// <summary>
/// Where a message was stored: its mailbox's <c>UIDVALIDITY</c> and the UID it got there, as
/// IMAP's <c>APPENDUID</c> reports them (RFC 4315).
/// </summary>
/// <param name="UidValidity">The mailbox's <c>UIDVALIDITY</c>.</param>
/// <param name="Uid">The message's UID.</param>
public readonly record struct MailStoredUid(uint UidValidity, uint Uid);
