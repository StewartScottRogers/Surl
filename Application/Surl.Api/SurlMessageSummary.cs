namespace Surl.Api;

/// <summary>One mail message's metadata, as <see cref="SurlServerMail.ListMessagesAsync"/> reads it.</summary>
/// <param name="Uid">The message's UID.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Flags">Its IMAP system flags, such as <c>\Seen</c>, in a fixed order.</param>
/// <param name="InternalDate">When the store took it in.</param>
public sealed record SurlMessageSummary(uint Uid, long Size, IReadOnlyList<string> Flags, DateTimeOffset InternalDate);
