using System.Collections.Frozen;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The connection commands (ADR-0106 decisions 1, 2 and 4, each command's reference page):
/// <c>HELLO</c>, <c>AUTH</c>, <c>SELECT</c>, <c>PING</c>, <c>ECHO</c>, <c>QUIT</c>, <c>RESET</c>
/// and <c>CLIENT SETNAME</c>, <c>GETNAME</c>, <c>ID</c> and <c>SETINFO</c>.
/// </summary>
internal static class RedisConnectionCommands
{
    /// <summary>What <c>HELLO</c> reports as <c>version</c>: the command semantics followed (ADR-0106 decision 0).</summary>
    public const string ReportedVersion = "7.4.0";

    private static readonly RespSimpleString Ok = new("OK");
    private static readonly RespSimpleString Pong = new("PONG");
    private static readonly RespSimpleString ResetDone = new("RESET");

    /// <summary>The connection commands but <c>CLIENT</c>'s subcommands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        new("hello", -1, true, HelloAsync),
        new("auth", -2, true, AuthAsync),
        new("select", 2, false, call => ValueTask.FromResult(Select(call))),
        new("ping", -1, false, call => ValueTask.FromResult(Ping(call))),
        new("echo", 2, false, call => ValueTask.FromResult<RespValue>(new RespBulkString(call.Arguments[1]))),
        new("quit", -1, true, call => ValueTask.FromResult(Quit(call))),
        new("reset", 1, true, call => ValueTask.FromResult(Reset(call))),
    ];

    /// <summary><c>CLIENT</c> and the subcommands served.</summary>
    public static RedisCommandContainer Client { get; } = new(
        "client",
        -2,
        new RedisCommand[]
        {
            new("client|setname", 3, false, call => ValueTask.FromResult(ClientSetName(call))),
            new("client|getname", 2, false, call => ValueTask.FromResult(ClientGetName(call))),
            new("client|id", 2, false, call => ValueTask.FromResult<RespValue>(new RespInteger(call.Session.ClientId))),
            new("client|setinfo", 4, false, call => ValueTask.FromResult(ClientSetInfo(call))),
        }.ToFrozenDictionary(command => command.Name["client|".Length..].ToUpperInvariant()));

    /// <summary>
    /// Whether <paramref name="name"/> may name a connection: printable ASCII without a space,
    /// as the <c>CLIENT SETNAME</c> page says; an empty name clears it.
    /// </summary>
    public static bool IsValidClientName(byte[] name) => name.AsSpan().IndexOfAnyExceptInRange((byte)'!', (byte)'~') < 0;

    // HELLO [protover [AUTH username password] [SETNAME clientname]], checked in Redis's order:
    // the version, the options, the login, then whether the connection is logged in.
    private static async ValueTask<RespValue> HelloAsync(RedisCommandCall call)
    {
        var session = call.Session;
        if (!TryReadProtocolVersion(call, out var version))
        {
            return RedisErrorReplies.UnsupportedProtocolVersion;
        }

        var options = HelloOptions.Read(call.Arguments);
        var refusal = options.Refusal ?? await LogInForHelloAsync(call, options);
        if (refusal is not null)
        {
            return refusal;
        }

        session.ClientName = options.ClientName is null ? session.ClientName : NameOrNone(options.ClientName);
        session.ProtocolVersion = version;
        return HelloReply(session);
    }

    // The version HELLO names, or the connection's own when it names none.
    private static bool TryReadProtocolVersion(RedisCommandCall call, out int version)
    {
        version = call.Session.ProtocolVersion;
        if (call.Arguments.Count < 2)
        {
            return true;
        }

        var isInteger = RedisIntegers.TryParse(call.Arguments[1], out var value);
        version = (int)value;
        return isInteger && value is 2 or 3;
    }

    // The login HELLO carries, if any, then whether the connection may go on.
    private static async ValueTask<RespSimpleError?> LogInForHelloAsync(RedisCommandCall call, HelloOptions options)
    {
        if (options.Password is not null && !await call.Logins.TryLogInAsync(call.Session, options.UserName!, options.Password))
        {
            return RedisErrorReplies.WrongPassword;
        }

        return call.Session.MayRunEveryCommand ? null : RedisErrorReplies.HelloAuthenticationRequired;
    }

    // server, version, proto, id, mode, role and modules: a map in RESP3, a flat array in RESP2.
    private static RespValue HelloReply(RedisSession session)
    {
        KeyValuePair<RespValue, RespValue>[] fields =
        [
            new(new RespBulkString("server"), new RespBulkString("redis")),
            new(new RespBulkString("version"), new RespBulkString(ReportedVersion)),
            new(new RespBulkString("proto"), new RespInteger(session.ProtocolVersion)),
            new(new RespBulkString("id"), new RespInteger(session.ClientId)),
            new(new RespBulkString("mode"), new RespBulkString("standalone")),
            new(new RespBulkString("role"), new RespBulkString("master")),
            new(new RespBulkString("modules"), new RespArray()),
        ];

        return session.ProtocolVersion == 3
            ? new RespMap(fields)
            : new RespArray(fields.SelectMany(field => new[] { field.Key, field.Value }).ToArray());
    }

    // AUTH [username] password: the user is "default" when only a password is given.
    private static async ValueTask<RespValue> AuthAsync(RedisCommandCall call)
    {
        var arguments = call.Arguments;
        if (arguments.Count > 3)
        {
            return RedisErrorReplies.SyntaxError;
        }

        var userName = arguments.Count == 3 ? arguments[1] : "default"u8.ToArray();
        return await call.Logins.TryLogInAsync(call.Session, userName, arguments[^1]) ? Ok : RedisErrorReplies.WrongPassword;
    }

    private static RespValue Select(RedisCommandCall call)
    {
        // As Redis reads it: a 32-bit integer, then a database number.
        if (!RedisIntegers.TryParse(call.Arguments[1], out var index) || index != (int)index)
        {
            return RedisErrorReplies.NotAnInteger;
        }

        if ((ulong)index >= RedisSession.DatabaseCount)
        {
            return RedisErrorReplies.DatabaseIndexOutOfRange;
        }

        call.Session.DatabaseIndex = (int)index;
        return Ok;
    }

    // A subscribed RESP2 connection is answered the array pong and the message, empty when none
    // (the "Pub/Sub" page).
    private static RespValue Ping(RedisCommandCall call)
    {
        if (call.Arguments.Count > 2)
        {
            return RedisErrorReplies.WrongNumberOfArguments("ping");
        }

        if (call.Subscriber.IsSubscribed && call.Session.ProtocolVersion == 2)
        {
            return new RespArray(new RespBulkString("pong"), new RespBulkString(call.Arguments.Count == 2 ? call.Arguments[1] : []));
        }

        return call.Arguments.Count == 2 ? new RespBulkString(call.Arguments[1]) : Pong;
    }

    private static RespValue Quit(RedisCommandCall call)
    {
        call.Session.IsQuitting = true;
        return Ok;
    }

    private static RespValue Reset(RedisCommandCall call)
    {
        call.PubSub.UnsubscribeAll(call.Subscriber);
        call.Session.Reset();
        return ResetDone;
    }

    private static RespValue ClientSetName(RedisCommandCall call)
    {
        var name = call.Arguments[2];
        if (!IsValidClientName(name))
        {
            return RedisErrorReplies.InvalidClientName;
        }

        call.Session.ClientName = NameOrNone(name);
        return Ok;
    }

    private static RespValue ClientGetName(RedisCommandCall call) =>
        call.Session.ClientName is { } name ? new RespBulkString(name) : call.Session.NullReply;

    // CLIENT SETINFO LIB-NAME|LIB-VER value: accepted and not kept, as nothing reads it.
    private static RespValue ClientSetInfo(RedisCommandCall call) =>
        RedisCommandTable.UpperCaseName(call.Arguments[2]) is "LIB-NAME" or "LIB-VER" ? Ok : RedisErrorReplies.SyntaxError;

    private static byte[]? NameOrNone(byte[] name) => name.Length == 0 ? null : name;

    /// <summary><c>HELLO</c>'s options after the version, or the error refusing them.</summary>
    private sealed record HelloOptions(byte[]? UserName, byte[]? Password, byte[]? ClientName, RespSimpleError? Refusal)
    {
        public static HelloOptions Read(IReadOnlyList<byte[]> arguments)
        {
            var options = new HelloOptions(null, null, null, null);
            for (var index = 2; index < arguments.Count && options.Refusal is null; index++)
            {
                options = options.WithOption(arguments, ref index);
            }

            return options;
        }

        private HelloOptions WithOption(IReadOnlyList<byte[]> arguments, ref int index)
        {
            var more = arguments.Count - 1 - index;
            var option = RedisCommandTable.UpperCaseName(arguments[index]);
            if (option == "AUTH" && more >= 2)
            {
                index += 2;
                return this with { UserName = arguments[index - 1], Password = arguments[index] };
            }

            if (option == "SETNAME" && more >= 1)
            {
                index++;
                return IsValidClientName(arguments[index])
                    ? this with { ClientName = arguments[index] }
                    : this with { Refusal = RedisErrorReplies.InvalidClientName };
            }

            return this with { Refusal = RedisErrorReplies.SyntaxError };
        }
    }
}
