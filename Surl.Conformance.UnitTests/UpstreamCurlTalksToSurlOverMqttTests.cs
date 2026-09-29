namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build publishes to a live, in-process <c>surl</c> over MQTT
/// (<c>-d</c>) and then subscribes to the same topic (a plain fetch) against the same
/// <c>surl</c>. The publish exits with the exit code and stdout recorded for it in
/// <c>Surl.Protocol.Mqtt.UnitTests/Fixtures</c>, and the subscribe receives the published
/// message as that topic's retained message (BL-036's rule, ADR-0014), so it exits and prints
/// what the recording of a subscribe to a topic holding that message holds. Each expected
/// result is read from its fixture folder, so it is the recording, never a copy of it.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTalksToSurlOverMqttTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublishThenSubscribe_Hi_SubscriberReceivesWhatTheRecordingHolds()
    {
        await AssertPublishThenSubscribeMatchRecordingsAsync("hi", "publish-hi", "subscribe-t");
    }

    [TestMethod]
    public async Task PublishThenSubscribe_200Bytes_SubscriberReceivesWhatTheRecordingHolds()
    {
        await AssertPublishThenSubscribeMatchRecordingsAsync(
            new string('x', 200), "publish-200-bytes", "subscribe-200-bytes", ReplacePayload('y', 'x'));
    }

    private async Task AssertPublishThenSubscribeMatchRecordingsAsync(
        string message, string publishFixture, string subscribeFixture, Func<byte[], byte[]>? adaptSubscribeOutput = null)
    {
        var (publishExitCode, publishOutput) = await ReadRecordingAsync(publishFixture);
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync(subscribeFixture);
        await using var surl = await SurlOnLoopback.StartAsync(
            "mqtt", new Dictionary<string, byte[]>(), [], [], TestContext.CancellationToken);
        var topicUrl = $"mqtt://{surl.BaseUrl.Host}:{surl.BaseUrl.Port}/t";

        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", message, topicUrl);
        var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", topicUrl);

        Assert.AreEqual(publishExitCode, publish.ExitCode, publish.StandardError);
        CollectionAssert.AreEqual(publishOutput, publish.StandardOutput);
        Assert.AreEqual(subscribeExitCode, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual((adaptSubscribeOutput ?? (bytes => bytes))(subscribeOutput), subscribe.StandardOutput);
    }

    // subscribe-200-bytes was recorded with 200 'y's retained so its payload could not be
    // mistaken for publish-200-bytes' 200 'x's; the subscriber here receives the 'x's published.
    private static Func<byte[], byte[]> ReplacePayload(char recorded, char published) =>
        bytes => [.. bytes[..3], .. bytes[3..].Select(value => value == (byte)recorded ? (byte)published : value)];

    private async Task<(int ExitCode, byte[] StandardOutput)> ReadRecordingAsync(string fixtureFolder)
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Surl.Protocol.Mqtt.UnitTests", "Fixtures", fixtureFolder);
        var exitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var standardOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        return (exitCode, standardOutput);
    }
}
