using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Commands as the RESP page frames them, and exchanges with the server, for the server's tests.
/// </summary>
internal static class RedisTestExchanges
{
    /// <summary>A command as an array of bulk strings (the RESP page, "Sending commands").</summary>
    public static string Command(params string[] arguments) =>
        $"*{arguments.Length}\r\n" + string.Concat(arguments.Select(argument => $"${Encoding.Latin1.GetByteCount(argument)}\r\n{argument}\r\n"));

    /// <summary>
    /// Serves <paramref name="inbound"/> on one connection, the client half-closing after it, and
    /// returns what the server wrote, as Latin-1.
    /// </summary>
    public static async Task<string> ServeAsync(
        IAuthenticationPolicy policy,
        CancellationToken cancellationToken,
        params string[] inbound)
    {
        var connection = Connection(inbound);
        await new RedisServer(policy).ServeAsync(connection, Context(cancellationToken));
        return Written(connection);
    }

    /// <summary>A connection replaying <paramref name="inbound"/>, one chunk each.</summary>
    public static InMemoryConnection Connection(string[] inbound, bool peerHalfCloses = true, TlsSession? tlsSession = null) =>
        new(inbound.Select(chunk => (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes(chunk)), peerHalfClosesWhenExhausted: peerHalfCloses, initialTlsSession: tlsSession);

    public static string Written(InMemoryConnection connection) => Encoding.Latin1.GetString(connection.WrittenBytes);

    public static ExchangeContext Context(
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("redis", "127.0.0.1", 6379).WithBoundPort(6379),
            new IPEndPoint(IPAddress.Loopback, 6379),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider ?? new ManualTimeProvider(),
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };
}
