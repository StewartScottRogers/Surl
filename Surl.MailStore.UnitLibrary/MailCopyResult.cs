namespace Surl.MailStore;

/// <summary>
/// What <see cref="MailboxStore.Copy"/> copied, as IMAP's <c>COPYUID</c> reports it (RFC 4315).
/// </summary>
/// <param name="DestinationUidValidity">The destination mailbox's <c>UIDVALIDITY</c>.</param>
/// <param name="SourceUids">The UIDs copied, in ascending order.</param>
/// <param name="CopyUids">The UID each copy got in the destination, in the same order.</param>
public sealed record MailCopyResult(uint DestinationUidValidity, IReadOnlyList<uint> SourceUids, IReadOnlyList<uint> CopyUids);
