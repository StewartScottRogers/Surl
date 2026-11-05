using System.Net;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// One client connected to a <see cref="StompServer"/> on a shared bus while the test drives it:
/// CONNECT is sent and CONNECTED awaited on start, <see cref="Send"/> writes frames, and
/// <see cref="Answers"/> is what the server wrote after CONNECTED.
/// </summary>
internal sealed partial class StompTestSession
{
    /// <summary>CONNECT as alice, accepting 1.2 only, without heart-beating.</summary>
    public const string AliceConnect12 = "CONNECT\naccept-version:1.2\nlogin:alice\npasscode:secret\n\n\0";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private StompTestSession(UnitTestStompClientConnection connection, Task serving, RecordingExchangeLog log)
    {
        Connection = connection;
        Serving = serving;
        Log = log;
    }

    public UnitTestStompClientConnection Connection { get; }

    public Task Serving { get; }

    public RecordingExchangeLog Log { get; }

    /// <summary>What the server wrote after CONNECTED, the frame's random session masked.</summary>
    public string Answers
    {
        get
        {
            var written = Connection.Written;
            var connectedEnd = written.IndexOf('\0', StringComparison.Ordinal);
            return written[(connectedEnd + 1)..];
        }
    }

    /// <summary>Everything the server wrote, CONNECTED's session masked.</summary>
    public string Written => SessionHeader().Replace(Connection.Written, "session:*\n");

    public static async Task<StompTestSession> StartAsync(
        Surl.MessageBus.MessageBus bus,
        TimeProvider? clock = null,
        StompServerOptions? options = null,
        ExchangeLimits? limits = null,
        string connect = AliceConnect12,
        int failWritesAfter = int.MaxValue,
        CancellationToken cancellation = default)
    {
        var log = new RecordingExchangeLog();
        var connection = new UnitTestStompClientConnection { FailWritesAfter = failWritesAfter };
        var context = Context(clock ?? TimeProvider.System, log, cancellation) with { Limits = limits ?? ExchangeLimits.Default };
        var serving = new StompServer(new UnitTestStompAuthenticationPolicy(), bus, options).ServeAsync(connection, context);
        var session = new StompTestSession(connection, serving, log);
        connection.Send(connect);
        await session.WaitUntilAsync(() => connection.Written.Contains("CONNECTED\n", StringComparison.Ordinal) && connection.Written.Contains('\0', StringComparison.Ordinal));
        return session;
    }

    public void Send(string frames) => Connection.Send(frames);

    /// <summary>Sends <paramref name="frames"/> and waits for the RECEIPT the last of them asks for.</summary>
    public Task SendAndWaitForReceiptAsync(string frames, string receipt)
    {
        Send(frames);
        return WaitForAsync($"RECEIPT\nreceipt-id:{receipt}\n\n\0");
    }

    /// <summary>Waits until the server has written <paramref name="text"/> after CONNECTED.</summary>
    public Task WaitForAsync(string text) => WaitUntilAsync(() => Answers.Contains(text, StringComparison.Ordinal));

    /// <summary>Closes the client's side and waits for the server to finish.</summary>
    public async Task CloseAsync()
    {
        Connection.Close();
        await Serving.WaitAsync(WaitLimit);
    }

    /// <summary>Waits for the server to finish without closing the client's side.</summary>
    public Task EndAsync() => Serving.WaitAsync(WaitLimit);

    public async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"The server did not reach the expected state in time; it wrote: {Written}");
            }

            if (Serving.IsFaulted)
            {
                await Serving;
            }

            await Task.Delay(5);
        }
    }

    private static ExchangeContext Context(TimeProvider clock, IExchangeLog log, CancellationToken cancellation) => new(
        1,
        new ListenUrl("stomp", "127.0.0.1", 61613).WithBoundPort(61613),
        new IPEndPoint(IPAddress.Loopback, 61613),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellation);

    [GeneratedRegex("session:[0-9a-f]{32}\n")]
    private static partial Regex SessionHeader();
}
