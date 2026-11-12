using Surl.Cli;
using Surl.MessageBus;
using Surl.Output;

namespace Surl.Composition;

[TestClass]
public sealed class MessageBusCompositionTests
{
    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.Compose(null!, Groups()));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.Compose(new SurlCommandLine(), null!));
    }

    [TestMethod]
    public void Compose_NoMessagingOption_IsTheConfigurationOfNothing()
    {
        var (configuration, failure) = MessageBusComposition.Compose(new SurlCommandLine(), Groups());

        Assert.AreSame(MessageBusConfiguration.None, configuration);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Compose_TopicsQueuesMapsAndAccess_ReachTheConfigurationWithGroupsExpanded()
    {
        var commandLine = new SurlCommandLine
        {
            Topics = [new ConfiguredTopic("shop/#", IsRetained: true, MaxQos: 1), new ConfiguredTopic("shop/orders", false, null)],
            Queues = [new ConfiguredQueue("jobs", IsDurable: true), new ConfiguredQueue("mail", false)],
            TopicMaps = [new ConfiguredTopicMap("amqp", "orders", "mqtt", "shop/orders")],
            TopicAccess =
            [
                Access("shop/#", ("staff", true, "pub"), ("alice", false, "sub"), ("carol", false, "sub")),
                Access("$queue/jobs", ("everyone", true, "pubsub")),
            ],
        };

        var (configuration, failure) = MessageBusComposition.Compose(commandLine, Groups());

        Assert.IsNull(failure);
        Assert.HasCount(2, configuration!.Topics);
        Assert.AreEqual("shop/#", configuration.Topics[0].Filter);
        Assert.IsTrue(configuration.Topics[0].Retain);
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, configuration.Topics[0].MaximumQualityOfService);
        Assert.IsFalse(configuration.Topics[1].Retain);
        Assert.IsNull(configuration.Topics[1].MaximumQualityOfService);
        CollectionAssert.AreEqual(new[] { "jobs", "mail" }, configuration.Queues.Select(queue => queue.Name).ToArray());
        CollectionAssert.AreEqual(new[] { true, false }, configuration.Queues.Select(queue => queue.Durable).ToArray());
        Assert.AreEqual(new TopicMap("amqp", "orders", "mqtt", "shop/orders"), configuration.TopicMaps.Single());
        CollectionAssert.AreEqual(
            new[]
            {
                "shop/# alice PublishAndSubscribe",
                "shop/# bob Publish",
                "shop/# carol Subscribe",
                "$queue/jobs @everyone PublishAndSubscribe",
            },
            configuration.AccessGrants.Select(grant => $"{grant.Filter} {grant.Member} {grant.Rights}").ToArray());
    }

    [TestMethod]
    public void Compose_ComposedConfiguration_IsOneTheBusAccepts()
    {
        var commandLine = new SurlCommandLine
        {
            Topics = [new ConfiguredTopic("shop/orders", false, 0)],
            Queues = [new ConfiguredQueue("jobs", false)],
            TopicMaps = [new ConfiguredTopicMap("amqp", "orders", "mqtt", "shop/orders"), new ConfiguredTopicMap("mqtt5+ws", "o", "mqtt", "shop/orders")],
            TopicAccess = [Access("shop/#", ("staff", true, "pub"))],
        };
        var (configuration, _) = MessageBusComposition.Compose(commandLine, Groups());

        var bus = new MessageBus.MessageBus(TimeProvider.System, configuration: configuration);

        Assert.AreEqual("shop/orders", bus.Names.ToCanonicalTopic("amqp", "orders").Name);
        Assert.AreEqual(TopicAccessRights.Publish, bus.GetTopicAccess("alice", "shop/orders"));
        Assert.AreEqual(TopicAccessRights.None, bus.GetTopicAccess(null, "shop/orders"));
    }

    [TestMethod]
    public void Compose_TopicFilterThatIsNotCanonical_IsRefused() =>
        AssertRefused(
            new SurlCommandLine { Topics = [new ConfiguredTopic("shop/a+b", false, null)] },
            "(2) option --topic: topic shop/a+b is not a valid topic filter");

    [TestMethod]
    public void Compose_MoreQueuesThanTheBusHolds_IsRefused() =>
        AssertRefused(
            new SurlCommandLine { Queues = [.. Enumerable.Range(0, MessageBus.MessageBus.DefaultMaxQueues + 1).Select(n => new ConfiguredQueue($"q{n}", false))] },
            "(2) option --queue: more than 1000 queues");

    [TestMethod]
    public void Compose_ExactlyAsManyQueuesAsTheBusHolds_IsAccepted()
    {
        var commandLine = new SurlCommandLine
        {
            Queues = [.. Enumerable.Range(0, MessageBus.MessageBus.DefaultMaxQueues).Select(n => new ConfiguredQueue($"q{n}", false))],
        };

        var (configuration, failure) = MessageBusComposition.Compose(commandLine, Groups());

        Assert.IsNull(failure);
        Assert.HasCount(MessageBus.MessageBus.DefaultMaxQueues, configuration!.Queues);
    }

    [TestMethod]
    [DataRow("http", "x", "mqtt", "a", "(2) option --topic-map: http is not a messaging scheme")]
    [DataRow("mqtt", "a", "ftp", "x", "(2) option --topic-map: ftp is not a messaging scheme")]
    [DataRow("amqp", "a/b", "mqtt", "a", "(2) option --topic-map: not a valid name for amqp")]
    [DataRow("amqp", "orders", "mqtt", "shop/#", "(2) option --topic-map: not a valid name for mqtt")]
    public void Compose_TopicMapTheLookupRefuses_IsRefused(string fromScheme, string fromName, string toScheme, string toName, string expected) =>
        AssertRefused(new SurlCommandLine { TopicMaps = [new ConfiguredTopicMap(fromScheme, fromName, toScheme, toName)] }, expected);

    [TestMethod]
    [DataRow("mqtt", "mqtts", "(2) option --topic-map: topic map mqtts:a is given twice")]
    [DataRow("mqtt5", "mqtt5+ws", "(2) option --topic-map: topic map mqtt5+ws:a is given twice")]
    [DataRow("nats", "natss", "(2) option --topic-map: topic map natss:a is given twice")]
    [DataRow("natss", "nats", "(2) option --topic-map: topic map nats:a is given twice")]
    public void Compose_TopicMapAliasingANameItsTlsTwinAliases_IsRefused(string first, string second, string expected) =>
        AssertRefused(
            new SurlCommandLine { TopicMaps = [new ConfiguredTopicMap(first, "a", "mqtt", "x"), new ConfiguredTopicMap(second, "a", "mqtt", "y")] },
            expected);

    [TestMethod]
    public void Compose_SameNameOnTwoSchemeFamilies_IsAccepted()
    {
        var commandLine = new SurlCommandLine
        {
            TopicMaps = [new ConfiguredTopicMap("nats", "a", "mqtt", "x"), new ConfiguredTopicMap("kafka", "a", "mqtt", "y")],
        };

        var (configuration, failure) = MessageBusComposition.Compose(commandLine, Groups());

        Assert.IsNull(failure);
        Assert.HasCount(2, configuration!.TopicMaps);
    }

    [TestMethod]
    public void Compose_TopicAccessFilterThatIsNotCanonical_IsRefused() =>
        AssertRefused(
            new SurlCommandLine { TopicAccess = [Access("a+b", ("alice", false, "pub"))] },
            "(2) option --topic-access: topic a+b is not a valid topic filter");

    [TestMethod]
    [DataRow("dave", false, "(2) option --topic-access: member dave names no account or group")]
    [DataRow("nobody", true, "(2) option --topic-access: member @nobody names no account or group")]
    public void Compose_TopicAccessMemberNamingNothing_IsRefused(string member, bool isGroup, string expected) =>
        AssertRefused(new SurlCommandLine { TopicAccess = [Access("shop/#", (member, isGroup, "sub"))] }, expected);

    [TestMethod]
    public void WriteWarnings_EachFilterGrantingEveryone_IsWarnedFromInfoUp()
    {
        var configuration = new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("shop/#", TopicAccessGrant.Everyone, TopicAccessRights.Publish),
                new TopicAccessGrant("shop/#", "alice", TopicAccessRights.PublishAndSubscribe),
                new TopicAccessGrant("news/#", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                new TopicAccessGrant("$queue/jobs", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe),
            ],
        };

        var written = Write(log => MessageBusComposition.WriteWarnings(LogLevel.Info, configuration, log));

        Assert.AreEqual(
            "surl: warning: --topic-access: shop/# grants pub to every caller, anonymous included" + Environment.NewLine
            + "surl: warning: --topic-access: news/# grants sub to every caller, anonymous included" + Environment.NewLine
            + "surl: warning: --topic-access: $queue/jobs grants pubsub to every caller, anonymous included" + Environment.NewLine,
            written);
        Assert.AreEqual(string.Empty, Write(log => MessageBusComposition.WriteWarnings(LogLevel.Error, configuration, log)));
    }

    [TestMethod]
    public void WriteStartLines_EveryDeclarationMapAndTheGrantCount_AreNotedFromVerboseUp()
    {
        var configuration = new MessageBusConfiguration
        {
            Topics =
            [
                new DeclaredTopic("a/#", retain: true, BusQualityOfService.AtLeastOnce),
                new DeclaredTopic("b", retain: true),
                new DeclaredTopic("c", maximumQualityOfService: BusQualityOfService.AtMostOnce),
                new DeclaredTopic("d"),
            ],
            Queues = [new DeclaredQueue("jobs", durable: true), new DeclaredQueue("mail")],
            TopicMaps = [new TopicMap("amqp", "orders", "mqtt", "shop/orders")],
            AccessGrants = [new TopicAccessGrant("a/#", "alice", TopicAccessRights.Publish), new TopicAccessGrant("b", "bob", TopicAccessRights.Subscribe)],
        };

        var written = Write(log => MessageBusComposition.WriteStartLines(LogLevel.Verbose, configuration, log));

        CollectionAssert.AreEqual(
            new[]
            {
                "* Topic a/#: retained, QoS at most 1",
                "* Topic b: retained",
                "* Topic c: QoS at most 0",
                "* Topic d",
                "* Queue jobs: durable",
                "* Queue mail",
                "* Topic map amqp:orders = mqtt:shop/orders",
                "* Topic access: 2 grants",
                string.Empty,
            },
            written.Split(Environment.NewLine));
        Assert.AreEqual(string.Empty, Write(log => MessageBusComposition.WriteStartLines(LogLevel.Info, configuration, log)));
        Assert.AreEqual(string.Empty, Write(log => MessageBusComposition.WriteStartLines(LogLevel.Verbose, MessageBusConfiguration.None, log)));
    }

    [TestMethod]
    public void WritersNullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.WriteWarnings(LogLevel.Info, null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.WriteWarnings(LogLevel.Info, MessageBusConfiguration.None, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.WriteStartLines(LogLevel.Info, null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBusComposition.WriteStartLines(LogLevel.Info, MessageBusConfiguration.None, null!));
    }

    private static AccountGroupExpansion Groups() =>
        AccountGroupExpansion.Compose(
            [new ConfiguredGroup("staff", [new ConfiguredMember("alice", false), new ConfiguredMember("bob", false)])],
            ["alice", "bob", "carol"]).Expansion!;

    private static ConfiguredTopicAccess Access(string filter, params (string Name, bool IsGroup, string Access)[] grants) =>
        new(filter, [.. grants.Select(grant => new ConfiguredGrant(new ConfiguredMember(grant.Name, grant.IsGroup), grant.Access))]);

    private static void AssertRefused(SurlCommandLine commandLine, string expectedFailure)
    {
        var (configuration, failure) = MessageBusComposition.Compose(commandLine, Groups());

        Assert.IsNull(configuration);
        Assert.AreEqual(expectedFailure, failure);
    }

    private static string Write(Action<TextWriter> write)
    {
        using var log = new StringWriter();
        write(log);
        return log.ToString();
    }
}
