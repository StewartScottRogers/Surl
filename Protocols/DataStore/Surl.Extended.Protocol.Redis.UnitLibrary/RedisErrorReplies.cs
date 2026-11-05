using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The fixed error replies of ADR-0106 decision 5, and the ones that echo the client's own bytes.
/// </summary>
internal static class RedisErrorReplies
{
    /// <summary>How many bytes of a command name, and of the argument text, an error echoes.</summary>
    public const int EchoLimit = 128;

    public static RespSimpleError AuthenticationRequired { get; } = new("NOAUTH Authentication required.");

    public static RespSimpleError HelloAuthenticationRequired { get; } = new(
        "NOAUTH HELLO must be called with the client already authenticated, otherwise the HELLO <proto> AUTH <user> <pass> option can be used to authenticate the client and select the RESP protocol version at the same time");

    public static RespSimpleError WrongPassword { get; } = new("WRONGPASS invalid username-password pair or user is disabled.");

    public static RespSimpleError UnsupportedProtocolVersion { get; } = new("NOPROTO unsupported protocol version");

    public static RespSimpleError SyntaxError { get; } = new("ERR syntax error");

    public static RespSimpleError NotAnInteger { get; } = new("ERR value is not an integer or out of range");

    public static RespSimpleError DatabaseIndexOutOfRange { get; } = new("ERR DB index is out of range");

    public static RespSimpleError InvalidClientName { get; } = new("ERR Client names cannot contain spaces, newlines or special characters.");

    public static RespSimpleError MaxNumberOfClientsReached { get; } = new("ERR max number of clients reached");

    public static RespSimpleError IncrementOverflow { get; } = new("ERR increment or decrement would overflow");

    public static RespSimpleError NoSuchKey { get; } = new("ERR no such key");

    public static RespSimpleError InvalidCursor { get; } = new("ERR invalid cursor");

    public static RespSimpleError WrongType { get; } = new("WRONGTYPE Operation against a key holding the wrong kind of value");

    public static RespSimpleError OutOfMemory { get; } = new("OOM command not allowed when used memory > 'maxmemory'.");

    public static RespSimpleError IndexOutOfRange { get; } = new("ERR index out of range");

    /// <summary><c>HINCRBY</c> on a field whose value is not an integer, in Redis's words.</summary>
    public static RespSimpleError HashValueNotAnInteger { get; } = new("ERR hash value is not an integer");

    /// <summary>A negative count to <c>LPOP</c>, <c>RPOP</c> or <c>SPOP</c>, in Redis's words.</summary>
    public static RespSimpleError NotPositive { get; } = new("ERR value is out of range, must be positive");

    public static RespSimpleError MaxNumberOfSubscriptionsReached { get; } = new("ERR max number of subscriptions reached");

    /// <summary>A command a RESP2 connection may not run while subscribed (ADR-0106 decision 1).</summary>
    /// <param name="commandName">The command's name in lower case.</param>
    /// <returns>The error.</returns>
    public static RespSimpleError NotAllowedWhileSubscribed(string commandName) =>
        new($"ERR Can't execute '{commandName}': only (P|S)SUBSCRIBE / (P|S)UNSUBSCRIBE / PING / QUIT / RESET are allowed in this context");

    /// <summary><c>-ERR invalid expire time in '<paramref name="commandName"/>' command</c>.</summary>
    /// <param name="commandName">The command's name in lower case.</param>
    /// <returns>The error.</returns>
    public static RespSimpleError InvalidExpireTime(string commandName) =>
        new($"ERR invalid expire time in '{commandName}' command");

    /// <summary><c>-ERR wrong number of arguments for '<paramref name="commandName"/>' command</c>.</summary>
    /// <param name="commandName">The command's name in lower case, <c>client|setname</c> for a subcommand.</param>
    /// <returns>The error.</returns>
    public static RespSimpleError WrongNumberOfArguments(string commandName) =>
        new($"ERR wrong number of arguments for '{commandName}' command");

    /// <summary>
    /// <c>-ERR unknown command '&lt;name&gt;', with args beginning with: </c> then <c>'&lt;arg&gt;' </c>
    /// per argument, as Redis writes it: the name cut to 128 bytes, and arguments added while the
    /// argument text is under 128 bytes, each cut to what is left of them.
    /// </summary>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <returns>The error.</returns>
    public static RespSimpleError UnknownCommand(IReadOnlyList<byte[]> arguments)
    {
        var text = new List<byte>(EchoLimit * 2);
        text.AddRange("ERR unknown command '"u8);
        text.AddRange(Cut(arguments[0], EchoLimit));
        text.AddRange("', with args beginning with: "u8);
        var argumentText = 0;
        for (var index = 1; index < arguments.Count && argumentText < EchoLimit; index++)
        {
            var argument = Cut(arguments[index], EchoLimit - argumentText);
            text.Add((byte)'\'');
            text.AddRange(argument);
            text.AddRange("' "u8);
            argumentText += argument.Length + 3;
        }

        return Echoing(text);
    }

    /// <summary><c>-ERR unknown subcommand '&lt;sub&gt;'. Try &lt;COMMAND&gt; HELP.</c>, the subcommand cut to 128 bytes.</summary>
    /// <param name="subcommand">The subcommand as sent.</param>
    /// <param name="commandName">The command's name, written in upper case.</param>
    /// <returns>The error.</returns>
    public static RespSimpleError UnknownSubcommand(byte[] subcommand, string commandName)
    {
        var text = new List<byte>(EchoLimit * 2);
        text.AddRange("ERR unknown subcommand '"u8);
        text.AddRange(Cut(subcommand, EchoLimit));
        text.AddRange(Encoding.ASCII.GetBytes($"'. Try {commandName.ToUpperInvariant()} HELP."));
        return Echoing(text);
    }

    private static ReadOnlySpan<byte> Cut(byte[] bytes, int limit) => bytes.AsSpan(0, Math.Min(bytes.Length, limit));

    // A simple error cannot hold CR or LF (the RESP page): each becomes a space.
    private static RespSimpleError Echoing(List<byte> text)
    {
        var bytes = text.ToArray();
        bytes.AsSpan().Replace((byte)'\r', (byte)' ');
        bytes.AsSpan().Replace((byte)'\n', (byte)' ');
        return new RespSimpleError(bytes);
    }
}
