using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An Engine.IO HTTP long-polling client written by hand from EIO "HTTP long-polling" and "Packet
/// encoding", driving a <see cref="SocketIoServerHarness"/>'s server one request per connection.
/// </summary>
internal sealed class SocketIoPollingClient
{
    public const string PollingTarget = "/socket.io/?EIO=4&transport=polling";

    public const string TokenPayload = "{\"token\":\"" + UnitTestPasswordPolicy.Token + "\"}";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly SocketIoServerHarness harness;

    private SocketIoPollingClient(SocketIoServerHarness harness, string openResponse)
    {
        this.harness = harness;
        OpenResponse = openResponse;
        SessionId = Body(openResponse)[9..29];
    }

    public string OpenResponse { get; }

    public string SessionId { get; }

    public string Target => PollingTarget + "&sid=" + SessionId;

    /// <summary>
    /// Sends the handshake <c>GET</c> and keeps the session it opens.
    /// </summary>
    public static async Task<SocketIoPollingClient> OpenAsync(SocketIoServerHarness harness, ExchangeContext? context = null) =>
        new(harness, await harness.SendRawAsync($"GET {PollingTarget} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\nConnection: close\r\n\r\n", context));

    /// <summary>
    /// The body of an HTTP response.
    /// </summary>
    public static string Body(string response) => response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

    public Task<string> GetAsync() => harness.SendAsync("GET", Target);

    /// <summary>
    /// Posts <paramref name="body"/>, each character one byte, as <paramref name="contentType"/>.
    /// </summary>
    public Task<string> PostAsync(string body, string contentType = "text/plain;charset=UTF-8", ExchangeContext? context = null) =>
        harness.SendRawAsync(
            $"POST {Target} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\nContent-Type: {contentType}\r\nContent-Length: {Encoding.Latin1.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            context);

    /// <summary>
    /// Connects the main namespace with the token and returns the poll that answers it.
    /// </summary>
    public async Task<string> ConnectAsync()
    {
        await PostAsync("40" + TokenPayload);

        return Body(await GetAsync());
    }

    /// <summary>
    /// Waits, in real time, until the listener holds <paramref name="count"/> sessions.
    /// </summary>
    public static async Task WaitForSessionCountAsync(SocketIoServerHarness harness, int count)
    {
        var giveUpAt = DateTime.UtcNow + WaitLimit;
        while (harness.Sessions.Count != count)
        {
            Assert.IsLessThan(giveUpAt, DateTime.UtcNow, "The session count never became " + count + ".");
            await Task.Delay(1);
        }
    }
}
