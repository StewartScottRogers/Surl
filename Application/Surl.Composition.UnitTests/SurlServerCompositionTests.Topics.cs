using Surl.Cli;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Mqtt;

namespace Surl.Composition;

/// <summary>
/// <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> and <c>--topic-access</c> composed into the one message bus
/// every broker shares (ADR-0102 decisions 3 and 5, BL-991).
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public async Task ComposeAsync_TopicsQueuesMapsAndAccess_ReachTheOneBusTheMqttServerHolds()
    {
        var commandLine = StartTestSeams.Parse(
            "--user", "alice:secret", "--user", "bob:secret", "--group", "staff=alice", "mqtt://127.0.0.1:0") with
        {
            Topics = [new ConfiguredTopic("shop/orders", IsRetained: true, MaxQos: 1)],
            Queues = [new ConfiguredQueue("jobs", IsDurable: false)],
            TopicMaps = [new ConfiguredTopicMap("amqp", "orders", "mqtt", "shop/orders")],
            TopicAccess = [new ConfiguredTopicAccess("shop/#", [new ConfiguredGrant(new ConfiguredMember("staff", true), "pubsub")])],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var bus = serving.Stores.Bus;
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(serving.Servers.OfType<MqttProtocolServer>().First()));
        Assert.AreEqual("shop/orders", bus.Configuration.Topics.Single().Filter);
        Assert.AreEqual("jobs", bus.Configuration.Queues.Single().Name);
        Assert.AreEqual("shop/orders", bus.Names.ToCanonicalTopic("amqp", "orders").Name);
        Assert.AreEqual(TopicAccessRights.PublishAndSubscribe, bus.GetTopicAccess("alice", "shop/orders"));
        Assert.AreEqual(TopicAccessRights.None, bus.GetTopicAccess("bob", "shop/orders"));
    }

    [TestMethod]
    public async Task ComposeAsync_TopicAccessMemberNamingNothing_IsRefusedBeforeAnyListenerBinds()
    {
        var commandLine = StartTestSeams.Parse("mqtt://127.0.0.1:0") with
        {
            TopicAccess = [new ConfiguredTopicAccess("shop/#", [new ConfiguredGrant(new ConfiguredMember("dave", false), "sub")])],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(2) option --topic-access: member dave names no account or group" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TopicAccessGrantingEveryone_WritesItsWarningAndVerboseNotes()
    {
        var commandLine = StartTestSeams.Parse("-v", "mqtt://127.0.0.1:0") with
        {
            Topics = [new ConfiguredTopic("shop/#", IsRetained: true, MaxQos: null)],
            Queues = [new ConfiguredQueue("jobs", IsDurable: false)],
            TopicMaps = [new ConfiguredTopicMap("amqp", "orders", "mqtt", "shop/orders")],
            TopicAccess = [new ConfiguredTopicAccess("news/#", [new ConfiguredGrant(new ConfiguredMember("everyone", true), "sub")])],
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var lines = log.ToString().Split(Environment.NewLine);
        CollectionAssert.Contains(lines, "surl: warning: --topic-access: news/# grants sub to every caller, anonymous included");
        CollectionAssert.Contains(lines, "* Topic shop/#: retained");
        CollectionAssert.Contains(lines, "* Queue jobs");
        CollectionAssert.Contains(lines, "* Topic map amqp:orders = mqtt:shop/orders");
        CollectionAssert.Contains(lines, "* Topic access: 1 grants");
    }
}
