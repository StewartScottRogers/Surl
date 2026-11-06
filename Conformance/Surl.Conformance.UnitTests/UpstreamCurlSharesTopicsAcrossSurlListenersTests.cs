using System.Buffers.Binary;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build publishes over MQTT (<c>-d</c>) on one listener of a live,
/// in-process <c>surl</c> and subscribes (a plain fetch) on another listener of the same
/// <c>surl</c>, and the subscriber receives the message through the one message bus every
/// listener shares (BL-622's ADR, decisions 1 and 3; BL-635). The publish exits with the exit
/// code and stdout recorded for it in <c>Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests/Fixtures</c>,
/// and the subscribe prints what the recording of a one-shot subscribe to a topic holding that
/// message holds (ADR-0014). Each expected result is read from its fixture folder, so it is the
/// recording, never a copy of it. Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSharesTopicsAcrossSurlListenersTests
{
    // curl publishes and subscribes without a user name, which surl's MQTT server refuses by
    // default (ADR-0032, section 5); these recordings are of an anonymous broker.
    private static readonly string[] AnonymousMqtt = ["--allow-anonymous"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublishOnOneMqttListener_SubscribeOnAnother_SubscriberReceivesWhatTheRecordingHolds()
    {
        var (publishExitCode, publishOutput) = await ReadRecordingAsync("publish-hi");
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync("subscribe-t");
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(["mqtt", "mqtt"], AnonymousMqtt, TestContext.CancellationToken);

        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", "hi", TopicUrl(surl.BaseUrls[0]));
        var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(surl.BaseUrls[1]));

        Assert.AreEqual(publishExitCode, publish.ExitCode, publish.StandardError);
        CollectionAssert.AreEqual(publishOutput, publish.StandardOutput);
        Assert.AreEqual(subscribeExitCode, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual(subscribeOutput, subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task PublishOnMqtt_SubscribeOnMqtts_SubscriberReceivesWhatTheMqttsRecordingHolds()
    {
        var (publishExitCode, publishOutput) = await ReadRecordingAsync("publish-hi");
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync("mqtts-subscribe-t");
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            ["mqtt", "mqtts"], [.. AnonymousMqtt, "--self-signed"], TestContext.CancellationToken);

        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", "hi", TopicUrl(surl.BaseUrlOf("mqtt")));
        var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", TopicUrl(surl.BaseUrlOf("mqtts")));

        Assert.AreEqual(publishExitCode, publish.ExitCode, publish.StandardError);
        CollectionAssert.AreEqual(publishOutput, publish.StandardOutput);
        Assert.AreEqual(subscribeExitCode, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual(subscribeOutput, subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task PublishOnOneListener_RestartOverTheDataDirectory_SubscribeOnAnother_SubscriberReceivesWhatTheRecordingHolds()
    {
        var (publishExitCode, publishOutput) = await ReadRecordingAsync("publish-hi");
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync("subscribe-t");
        var dataDirectory = Directory.CreateTempSubdirectory("surl-conformance-");
        try
        {
            UpstreamCurlRunResult publish;
            await using (var first = await StartTwoMqttListenersOverAsync(dataDirectory))
            {
                publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", "hi", TopicUrl(first.BaseUrls[0]));
            }

            await using var second = await StartTwoMqttListenersOverAsync(dataDirectory);
            var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(second.BaseUrls[1]));

            Assert.AreEqual(publishExitCode, publish.ExitCode, publish.StandardError);
            CollectionAssert.AreEqual(publishOutput, publish.StandardOutput);
            Assert.AreEqual(subscribeExitCode, subscribe.ExitCode, subscribe.StandardError);
            CollectionAssert.AreEqual(subscribeOutput, subscribe.StandardOutput);
        }
        finally
        {
            dataDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Subscribe_DataDirectoryWrittenBeforeTheBus_EachListenerServesTheOldRetainedMessage()
    {
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync("subscribe-t");
        var dataDirectory = Directory.CreateTempSubdirectory("surl-conformance-");
        try
        {
            WriteOldMqttRetainedMessagesFile(dataDirectory, "t", "hi"u8);
            await using var surl = await StartTwoMqttListenersOverAsync(dataDirectory);

            var first = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(surl.BaseUrls[0]));
            var second = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", TopicUrl(surl.BaseUrls[1]));

            Assert.AreEqual(subscribeExitCode, first.ExitCode, first.StandardError);
            CollectionAssert.AreEqual(subscribeOutput, first.StandardOutput);
            Assert.AreEqual(subscribeExitCode, second.ExitCode, second.StandardError);
            CollectionAssert.AreEqual(subscribeOutput, second.StandardOutput);
        }
        finally
        {
            dataDirectory.Delete(recursive: true);
        }
    }

    private Task<SurlOnLoopback> StartTwoMqttListenersOverAsync(DirectoryInfo dataDirectory) =>
        SurlOnLoopback.StartOverDirectoryAsync(["mqtt", "mqtt"], dataDirectory.FullName, AnonymousMqtt, TestContext.CancellationToken);

    private static string TopicUrl(Uri listener) => $"{listener.Scheme}://{listener.Host}:{listener.Port}/t";

    // ADR-0031 decision 6's retained-message file, as the MQTT server wrote it before BL-635:
    // the header line, then per topic a 2-byte big-endian length and its UTF-8 name, and a
    // 4-byte big-endian length and the payload.
    private static void WriteOldMqttRetainedMessagesFile(DirectoryInfo dataDirectory, string topic, ReadOnlySpan<byte> payload)
    {
        var mqttFolder = Directory.CreateDirectory(Path.Join(dataDirectory.FullName, ".surl", "mqtt"));
        var topicBytes = Encoding.UTF8.GetBytes(topic);
        var topicLength = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(topicLength, (ushort)topicBytes.Length);
        var payloadLength = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(payloadLength, (uint)payload.Length);
        File.WriteAllBytes(
            Path.Join(mqttFolder.FullName, "retained-messages"),
            [.. "SURL-MQTT-RETAINED-1\n"u8, .. topicLength, .. topicBytes, .. payloadLength, .. payload]);
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
