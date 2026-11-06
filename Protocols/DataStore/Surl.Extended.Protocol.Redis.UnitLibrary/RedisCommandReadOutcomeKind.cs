namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// What <see cref="RedisCommandReader.ReadCommandAsync"/> found.
/// </summary>
public enum RedisCommandReadOutcomeKind
{
    /// <summary>A command: its name and arguments, at least one.</summary>
    Command,

    /// <summary>
    /// Framing a server answers with <see cref="RedisCommandReadOutcome.ErrorReply"/> and then
    /// closes (ADR-0106 decision 6).
    /// </summary>
    ProtocolError,

    /// <summary>
    /// One command's bytes passed <c>--max-filesize</c> plus 64 KiB; the server closes with no
    /// bytes (ADR-0106 decision 6).
    /// </summary>
    CommandTooLarge,

    /// <summary>The client closed its side, between commands or inside one.</summary>
    ConnectionClosed,
}
