using Surl.MailStore;

namespace Surl.Composition;

/// <summary>One stored message without its bytes, as a test reads it back.</summary>
/// <param name="Uid">The message's UID.</param>
/// <param name="Size">The message's size in bytes.</param>
/// <param name="Flags">The message's flags.</param>
/// <param name="InternalDate">When the message was stored.</param>
public sealed record ComposedMailMessageSummary(uint Uid, long Size, MailFlags Flags, DateTimeOffset InternalDate);
