namespace Surl.Control;

/// <summary>One message of a mailbox without its bytes.</summary>
/// <param name="Uid">Its UID.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Flags">Its flags as IMAP writes them, such as <c>\Seen</c>.</param>
/// <param name="InternalDate">When it was delivered.</param>
public sealed record ControlMailMessageSummary(uint Uid, long Size, IReadOnlyList<string> Flags, DateTimeOffset InternalDate);
