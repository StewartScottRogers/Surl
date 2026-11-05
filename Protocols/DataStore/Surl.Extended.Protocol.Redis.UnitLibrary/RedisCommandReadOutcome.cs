namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One result of <see cref="RedisCommandReader.ReadCommandAsync"/>: a command, a protocol error
/// to answer before closing, a command too large to read, or the client's close.
/// </summary>
public sealed class RedisCommandReadOutcome
{
    private RedisCommandReadOutcome(
        RedisCommandReadOutcomeKind kind,
        IReadOnlyList<byte[]> arguments,
        RespSimpleError? errorReply)
    {
        Kind = kind;
        Arguments = arguments;
        ErrorReply = errorReply;
    }

    /// <summary>The outcome when one command's bytes passed the reader's bound.</summary>
    public static RedisCommandReadOutcome CommandTooLarge { get; } =
        new(RedisCommandReadOutcomeKind.CommandTooLarge, [], null);

    /// <summary>The outcome when the client closed its side.</summary>
    public static RedisCommandReadOutcome ConnectionClosed { get; } =
        new(RedisCommandReadOutcomeKind.ConnectionClosed, [], null);

    /// <summary>What was found.</summary>
    public RedisCommandReadOutcomeKind Kind { get; }

    /// <summary>
    /// For <see cref="RedisCommandReadOutcomeKind.Command"/>, the command's name then its
    /// arguments, each the exact bytes sent; otherwise empty.
    /// </summary>
    public IReadOnlyList<byte[]> Arguments { get; }

    /// <summary>
    /// For <see cref="RedisCommandReadOutcomeKind.ProtocolError"/>, the <c>-ERR Protocol error: …</c>
    /// reply to write before closing; otherwise <see langword="null"/>.
    /// </summary>
    public RespSimpleError? ErrorReply { get; }

    /// <summary>A command of <paramref name="arguments"/>.</summary>
    /// <param name="arguments">The command's name then its arguments; at least one.</param>
    /// <returns>The outcome.</returns>
    public static RedisCommandReadOutcome Command(IReadOnlyList<byte[]> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfZero(arguments.Count, nameof(arguments));
        return new(RedisCommandReadOutcomeKind.Command, arguments, null);
    }

    /// <summary>A protocol error answered <c>-ERR Protocol error: <paramref name="detail"/></c>.</summary>
    /// <param name="detail">The bytes after <c>Protocol error: </c>, without CR or LF.</param>
    /// <returns>The outcome.</returns>
    public static RedisCommandReadOutcome ProtocolError(ReadOnlySpan<byte> detail) =>
        new(RedisCommandReadOutcomeKind.ProtocolError, [], new RespSimpleError([.. "ERR Protocol error: "u8, .. detail]));
}
