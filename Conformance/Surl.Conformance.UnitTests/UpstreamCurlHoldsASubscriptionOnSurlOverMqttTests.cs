using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build subscribes (<c>curl mqtt://</c>) to a live, in-process <c>surl</c> started with
/// <c>--mqtt-hold-subscription</c> and receives a publish made after it subscribed (ADR-0014 Amendment 1, BL-1722).
/// The publisher is a test-local <see cref="TcpClient"/> writing the NATS protocol's text frames, since upstream curl
/// speaks no NATS and the Curl port is never the oracle (ADR-0003). A test learns the subscription is live from the
/// <c>SUBACK</c> in the exchange's sent bytes, which the control endpoint's <c>GET /v1/events/wait</c> hands it; nothing
/// sleeps. What curl prints is compared with the recording of a subscribe to topic <c>t</c> holding <c>hi</c> in
/// <c>Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests/Fixtures</c>. A last test shows that without the option a fetch of a
/// retained message still ends as that recording does. Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlHoldsASubscriptionOnSurlOverMqttTests
{
    // CURLE_OPERATION_TIMEDOUT: --max-time ended the transfer.
    private const int OperationTimedOut = 28;

    // The MQTT 3.1.1 SUBACK fixed header's first byte (section 3.9.1).
    private const byte SubscribeAcknowledgementHeader = 0x90;

    // The control token: 32 to 1024 visible ASCII bytes on the first line of the token file.
    private const string Token = "held-subscription-control-token-0123456789";

    private static readonly TimeSpan PublishLimit = TimeSpan.FromSeconds(30);

    private string temporaryDirectory = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateTemporaryDirectory() =>
        temporaryDirectory = Directory.CreateTempSubdirectory("surl-conformance-mqtt-hold-").FullName;

    [TestCleanup]
    public void DeleteTemporaryDirectory() => Directory.Delete(temporaryDirectory, recursive: true);

    [TestMethod]
    public async Task HeldSubscriptionWithCountOne_PublishOverNatsAfterSubAck_ExitsZeroWithTheTopicAndPayload()
    {
        var (recordedExitCode, recordedOutput) = await ReadRecordingAsync("subscribe-t");
        await using var surl = await StartHoldingAsync(publishCount: 1);

        var subscribing = PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(surl.BaseUrlOf("mqtt")));
        await WaitUntilSubscribedAsync(surl, subscribing);
        await PublishHiToTopicOverNatsAsync(surl.BaseUrlOf("nats"));
        var subscribe = await subscribing;

        Assert.AreEqual(recordedExitCode, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual(recordedOutput, subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task HeldSubscriptionWithCountZero_CurlMaxTimeEnds_ExitsTwentyEightWithTheLiveMessage()
    {
        var (_, recordedOutput) = await ReadRecordingAsync("subscribe-t");
        await using var surl = await StartHoldingAsync(publishCount: 0);

        // The test lasts this long: room for the SUBACK wait and the publish on a loaded machine, under the
        // runner's 30 second limit.
        var subscribing = PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--max-time", "20", TopicUrl(surl.BaseUrlOf("mqtt")));
        await WaitUntilSubscribedAsync(surl, subscribing);
        await PublishHiToTopicOverNatsAsync(surl.BaseUrlOf("nats"));
        var subscribe = await subscribing;

        Assert.AreEqual(OperationTimedOut, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual(recordedOutput, subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task SubscribeWithoutTheHoldOption_RetainedMessage_ExitsZeroWithThatMessage()
    {
        var (recordedExitCode, recordedOutput) = await ReadRecordingAsync("subscribe-t");
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "mqtt", ["--allow-anonymous", "--topic", "t;retain"], TestContext.CancellationToken);

        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", "hi", TopicUrl(surl.BaseUrl));
        var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(surl.BaseUrl));

        Assert.AreEqual(0, publish.ExitCode, publish.StandardError);
        Assert.AreEqual(recordedExitCode, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual(recordedOutput, subscribe.StandardOutput);
    }

    private static string TopicUrl(Uri listener) => $"{listener.Scheme}://{listener.Host}:{listener.Port}/t";

    private async Task<SurlOnLoopback> StartHoldingAsync(int publishCount)
    {
        var tokenFile = Path.Combine(temporaryDirectory, "control.token");
        await File.WriteAllTextAsync(tokenFile, Token + "\n", TestContext.CancellationToken);

        return await SurlOnLoopback.StartInMemoryWithControlAsync(
            ["mqtt", "nats"],
            ["--allow-anonymous", "--mqtt-hold-subscription", publishCount.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            tokenFile,
            TestContext.CancellationToken);
    }

    // Waits for the SUBACK. Nothing is published yet, so a subscriber curl that ends first has failed: report its result.
    private async Task WaitUntilSubscribedAsync(SurlOnLoopback surl, Task<UpstreamCurlRunResult> subscribing)
    {
        var acknowledged = WaitForSubscribeAcknowledgementAsync(surl);
        if (await Task.WhenAny(acknowledged, subscribing) == subscribing)
        {
            var ended = await subscribing;
            Assert.Fail($"curl ended before its SUBACK was seen: {ended}; stderr: {ended.StandardError}. {surl.Diagnosis}");
        }

        await acknowledged;
    }

    // Walks the mqtt listener's byte events, each wait starting after the last one seen, until the sent SUBACK.
    private async Task WaitForSubscribeAcknowledgementAsync(SurlOnLoopback surl)
    {
        var lastSequence = 0L;
        while (true)
        {
            var waited = await PinnedUpstreamCurl.RunAsync(
                TestContext,
                "-sS",
                "--fail-with-body",
                "-H",
                $"Authorization: Bearer {Token}",
                $"{surl.ControlUrl!.AbsoluteUri.TrimEnd('/')}/v1/events/wait?kind=exchange_bytes&scheme=mqtt&after={lastSequence}&timeout_ms=10000");
            Assert.AreEqual(0, waited.ExitCode, waited.StandardError);

            using var answer = JsonDocument.Parse(waited.StandardOutput);
            Assert.IsTrue(
                answer.RootElement.TryGetProperty("event", out var bytesEvent),
                $"surl sent no SUBACK to the mqtt subscriber. {surl.Diagnosis}");
            if (IsSentSubscribeAcknowledgement(bytesEvent))
            {
                return;
            }

            lastSequence = bytesEvent.GetProperty("sequence").GetInt64();
        }
    }

    // A SUBACK is not UTF-8 text, so the control endpoint shows it as base64.
    private static bool IsSentSubscribeAcknowledgement(JsonElement bytesEvent) =>
        bytesEvent.GetProperty("direction").GetString() == "sent"
        && bytesEvent.GetProperty("encoding").GetString() == "base64"
        && bytesEvent.GetProperty("data").GetBytesFromBase64()[0] == SubscribeAcknowledgementHeader;

    // Publishes "hi" to subject t, which is bus topic t. The PING after the PUB is answered only once the PUB was handled.
    private async Task PublishHiToTopicOverNatsAsync(Uri listener)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        limit.CancelAfter(PublishLimit);
        using var publisher = new TcpClient();
        await publisher.ConnectAsync(listener.Host, listener.Port, limit.Token);
        var stream = publisher.GetStream();
        await stream.WriteAsync("CONNECT {\"verbose\":false}\r\nPUB t 2\r\nhi\r\nPING\r\n"u8.ToArray(), limit.Token);

        var received = new StringBuilder();
        var buffer = new byte[4096];
        while (!received.ToString().Contains("PONG\r\n", StringComparison.Ordinal))
        {
            var count = await stream.ReadAsync(buffer, limit.Token);
            Assert.AreNotEqual(0, count, $"the nats listener closed the connection before PONG, after: {received}");
            received.Append(Encoding.UTF8.GetString(buffer, 0, count));
        }

        Assert.DoesNotContain("-ERR", received.ToString());
    }

    private async Task<(int ExitCode, byte[] StandardOutput)> ReadRecordingAsync(string fixtureFolder)
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Protocols", "Messaging", "Surl.Protocol.Mqtt.UnitTests", "Fixtures", fixtureFolder);
        var exitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var standardOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        return (exitCode, standardOutput);
    }
}
