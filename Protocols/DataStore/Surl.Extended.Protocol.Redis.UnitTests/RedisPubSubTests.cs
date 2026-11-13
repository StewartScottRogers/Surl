using System.Text;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Pub/sub through the server (BL-491; ADR-0106 decisions 1, 2 and 6), every byte written by hand
/// from the RESP page, the "Pub/Sub" page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisPubSubTests
{
    private const string Hello3 = "*2\r\n$5\r\nHELLO\r\n$1\r\n3\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task Publish_ToASubscriberOnAnotherConnection_ArrivesAsAnArrayInRespTwo()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "news", "sport"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "news", "hi"), Command("PUBLISH", "weather", "rain"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":1\r\n:0\r\n", published);
        Assert.AreEqual(
            "*3\r\n$9\r\nsubscribe\r\n$4\r\nnews\r\n:1\r\n*3\r\n$9\r\nsubscribe\r\n$5\r\nsport\r\n:2\r\n"
            + "*3\r\n$7\r\nmessage\r\n$4\r\nnews\r\n$2\r\nhi\r\n",
            received);
    }

    [TestMethod]
    public async Task Publish_ToASubscriberOnAnotherConnection_ArrivesAsAPushInRespThree()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Hello3, Command("SUBSCRIBE", "news"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "news", "hi"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":1\r\n", published);
        Assert.EndsWith(">3\r\n$9\r\nsubscribe\r\n$4\r\nnews\r\n:1\r\n>3\r\n$7\r\nmessage\r\n$4\r\nnews\r\n$2\r\nhi\r\n", received);
    }

    [TestMethod]
    public async Task Publish_ToAChannelAndAMatchingPattern_ReachesBothSubscriptions()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("PSUBSCRIBE", "news.*"), Command("SUBSCRIBE", "news.art"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "news.art", "x"), Command("PUBLISH", "sport", "y"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":2\r\n:0\r\n", published);
        Assert.AreEqual(
            "*3\r\n$10\r\npsubscribe\r\n$6\r\nnews.*\r\n:1\r\n*3\r\n$9\r\nsubscribe\r\n$8\r\nnews.art\r\n:2\r\n"
            + "*3\r\n$7\r\nmessage\r\n$8\r\nnews.art\r\n$1\r\nx\r\n"
            + "*4\r\n$8\r\npmessage\r\n$6\r\nnews.*\r\n$8\r\nnews.art\r\n$1\r\nx\r\n",
            received);
    }

    [TestMethod]
    public async Task Publish_ToTwoSubscribersOfOneChannel_ReachesEachUntilOneLeaves()
    {
        var server = new RedisTestServer();
        var first = await server.OpenAsync(Token, Command("SUBSCRIBE", "news"));
        var second = await server.OpenAsync(Token, Command("SUBSCRIBE", "news"), Command("UNSUBSCRIBE", "news"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "news", "hi"));
        await first.CloseAsync();
        var afterClose = await server.SendAsync(Token, Command("PUBLISH", "news", "hi"));
        await second.CloseAsync();

        Assert.AreEqual(":1\r\n", published);
        Assert.AreEqual(":0\r\n", afterClose);
    }

    [TestMethod]
    public async Task Subscribe_AChannelAlreadySubscribed_ConfirmsItWithTheSameCount()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(Token, Command("SUBSCRIBE", "a", "a"));

        Assert.AreEqual("*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n", written);
    }

    [TestMethod]
    public async Task Unsubscribe_WithNoArgument_LeavesEveryChannelOrConfirmsANullName()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("UNSUBSCRIBE"),
            Command("SUBSCRIBE", "a", "b"),
            Command("PSUBSCRIBE", "p*"),
            Command("UNSUBSCRIBE"),
            Command("UNSUBSCRIBE", "never"),
            Command("PUNSUBSCRIBE"),
            Command("PUNSUBSCRIBE"));

        Assert.AreEqual(
            "*3\r\n$11\r\nunsubscribe\r\n$-1\r\n:0\r\n"
            + "*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n*3\r\n$9\r\nsubscribe\r\n$1\r\nb\r\n:2\r\n"
            + "*3\r\n$10\r\npsubscribe\r\n$2\r\np*\r\n:3\r\n"
            + "*3\r\n$11\r\nunsubscribe\r\n$1\r\na\r\n:2\r\n*3\r\n$11\r\nunsubscribe\r\n$1\r\nb\r\n:1\r\n"
            + "*3\r\n$11\r\nunsubscribe\r\n$5\r\nnever\r\n:1\r\n"
            + "*3\r\n$12\r\npunsubscribe\r\n$2\r\np*\r\n:0\r\n"
            + "*3\r\n$12\r\npunsubscribe\r\n$-1\r\n:0\r\n",
            written);
    }

    [TestMethod]
    public async Task Unsubscribe_WithNothingSubscribedInRespThree_ConfirmsANullAsAPush()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(Token, Hello3, Command("UNSUBSCRIBE"));

        Assert.EndsWith(">3\r\n$11\r\nunsubscribe\r\n_\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task SubscribedInRespTwo_OtherCommandsAreRefusedAndPingIsAnArray()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SUBSCRIBE", "a"),
            Command("GET", "k"),
            Command("PING"),
            Command("PING", "hi"),
            Command("PING", "a", "b"),
            Command("UNSUBSCRIBE"),
            Command("PING"));

        Assert.AreEqual(
            "*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n"
            + "-ERR Can't execute 'get': only (P|S)SUBSCRIBE / (P|S)UNSUBSCRIBE / PING / QUIT / RESET are allowed in this context\r\n"
            + "*2\r\n$4\r\npong\r\n$0\r\n\r\n*2\r\n$4\r\npong\r\n$2\r\nhi\r\n"
            + "-ERR wrong number of arguments for 'ping' command\r\n"
            + "*3\r\n$11\r\nunsubscribe\r\n$1\r\na\r\n:0\r\n+PONG\r\n",
            written);
    }

    [TestMethod]
    public async Task SubscribedInRespThree_EveryCommandRunsAndOwnMessagesArriveBeforeTheReply()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Hello3,
            Command("SUBSCRIBE", "a"),
            Command("GET", "k"),
            Command("PING"),
            Command("PUBLISH", "a", "me"));

        Assert.EndsWith(
            ">3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n_\r\n+PONG\r\n>3\r\n$7\r\nmessage\r\n$1\r\na\r\n$2\r\nme\r\n:1\r\n",
            written);
    }

    [TestMethod]
    public async Task Reset_LeavesEveryChannelAndPattern()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "a"), Command("PSUBSCRIBE", "*"), Command("RESET"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "a", "x"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":0\r\n", published);
        Assert.EndsWith(":2\r\n+RESET\r\n", received);
    }

    [TestMethod]
    public async Task Subscribe_PastTheBound_IsRefusedWithNothingSubscribed()
    {
        var server = new RedisTestServer(pubSub: new RedisPubSub(new MessageBus.MessageBus(TimeProvider.System), 2, RedisPubSub.DefaultMaxUnsentMessageBytes));

        var written = await server.SendAsync(
            Token,
            Command("SUBSCRIBE", "a", "a"),
            Command("PSUBSCRIBE", "b", "c"),
            Command("SUBSCRIBE", "b"),
            Command("UNSUBSCRIBE"));

        Assert.AreEqual(
            "*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n"
            + "-ERR max number of subscriptions reached\r\n"
            + "*3\r\n$9\r\nsubscribe\r\n$1\r\nb\r\n:2\r\n"
            + "*3\r\n$11\r\nunsubscribe\r\n$1\r\na\r\n:1\r\n*3\r\n$11\r\nunsubscribe\r\n$1\r\nb\r\n:0\r\n",
            written);
    }

    [TestMethod]
    public async Task Publish_PastTheSubscribersUnsentBound_ClosesTheSubscriberWithNoMoreBytes()
    {
        var server = new RedisTestServer(pubSub: new RedisPubSub(new MessageBus.MessageBus(TimeProvider.System), RedisPubSub.DefaultMaxSubscriptionsPerConnection, 30));
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "a"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "a", "a message too long to queue"));
        await subscriber.Serving;
        var afterClose = await server.SendAsync(Token, Command("PUBLISH", "a", "x"));

        Assert.AreEqual(":1\r\n", published);
        Assert.AreEqual(":0\r\n", afterClose);
        Assert.AreEqual("*3\r\n$9\r\nsubscribe\r\n$1\r\na\r\n:1\r\n", subscriber.Written);
        Assert.Contains("closed with no more bytes", subscriber.Log.Notes.Single());
    }

    [TestMethod]
    public async Task PubSubCommands_BeforeLogin_AreRefused()
    {
        var written = await ServeAsync(new UnitTestRedisAuthenticationPolicy(false), Token, Command("SUBSCRIBE", "a"), Command("PUBLISH", "a", "x"));

        Assert.AreEqual("-NOAUTH Authentication required.\r\n-NOAUTH Authentication required.\r\n", written);
    }

    [TestMethod]
    public async Task BusPublish_OnATopicARedisConnectionSubscribes_ArrivesAsAMessageWithThePayloadExact()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "a/b"));

        var published = server.Bus.Publish(new BusMessage("a/b", new byte[] { 0x00, 0xFF, (byte)'x' }));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(1, published.DeliveredSubscriptionCount);
        Assert.AreEqual(
            "*3\r\n$9\r\nsubscribe\r\n$3\r\na/b\r\n:1\r\n*3\r\n$7\r\nmessage\r\n$3\r\na/b\r\n$3\r\n\0ÿx\r\n",
            received);
    }

    [TestMethod]
    public async Task Publish_OnATopic_ReachesABusSubscriptionOfAnyProtocolAndCountsIt()
    {
        var server = new RedisTestServer();
        var received = new List<BusMessage>();
        using var otherProtocol = server.Bus.Connect();
        otherProtocol.Subscribe("a/+", received.Add);

        var published = await server.SendAsync(Token, Command("PUBLISH", "a/b", "x"));

        Assert.AreEqual(":1\r\n", published);
        Assert.AreEqual("a/b", received.Single().Topic);
        Assert.AreEqual("x", Encoding.UTF8.GetString(received.Single().Payload.Span));
    }

    [TestMethod]
    public async Task BusPublish_MatchingAPattern_ArrivesAsAPmessageAndADollarTopicDoesNot()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("PSUBSCRIBE", "a*"));

        var matching = server.Bus.Publish(new BusMessage("a/b", "x"u8.ToArray()));
        var dollar = server.Bus.Publish(new BusMessage("$SYS/x", "y"u8.ToArray()));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(1, matching.DeliveredSubscriptionCount);
        Assert.AreEqual(0, dollar.DeliveredSubscriptionCount);
        Assert.AreEqual(
            "*3\r\n$10\r\npsubscribe\r\n$2\r\na*\r\n:1\r\n*4\r\n$8\r\npmessage\r\n$2\r\na*\r\n$3\r\na/b\r\n$1\r\nx\r\n",
            received);
    }

    [TestMethod]
    public async Task Publish_OnATopic_CountsEachMatchingPatternOnceAndAConnectionWithNoneMatchingNotAtAll()
    {
        var server = new RedisTestServer();
        var both = await server.OpenAsync(Token, Command("PSUBSCRIBE", "a*", "*b"));
        var neither = await server.OpenAsync(Token, Command("PSUBSCRIBE", "z*"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "a/b", "x"));
        var bothReceived = await both.CloseAsync();
        var neitherReceived = await neither.CloseAsync();

        Assert.AreEqual(":2\r\n", published);
        Assert.EndsWith(
            "*4\r\n$8\r\npmessage\r\n$2\r\na*\r\n$3\r\na/b\r\n$1\r\nx\r\n*4\r\n$8\r\npmessage\r\n$2\r\n*b\r\n$3\r\na/b\r\n$1\r\nx\r\n",
            bothReceived);
        Assert.EndsWith(":1\r\n", neitherReceived);
    }

    [TestMethod]
    public async Task Publish_OnADollarTopic_ReachesTheChannelButNoPattern()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "$SYS/x"), Command("PSUBSCRIBE", "*"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "$SYS/x", "y"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":1\r\n", published);
        Assert.EndsWith(":2\r\n*3\r\n$7\r\nmessage\r\n$6\r\n$SYS/x\r\n$1\r\ny\r\n", received);
    }

    [TestMethod]
    public async Task Publish_OnAChannelThatIsNotATopic_ReachesRedisConnectionsAndNeverTheBus()
    {
        var server = new RedisTestServer();
        var onBus = new List<BusMessage>();
        using var otherProtocol = server.Bus.Connect();
        otherProtocol.Subscribe("#", onBus.Add);
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "ÿ", "a/+"), Command("PSUBSCRIBE", "*"));

        var published = await server.SendAsync(Token, Command("PUBLISH", "ÿ", "x"), Command("PUBLISH", "a/+", "y"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":2\r\n:2\r\n", published);
        Assert.IsEmpty(onBus);
        Assert.EndsWith(
            "*3\r\n$7\r\nmessage\r\n$1\r\nÿ\r\n$1\r\nx\r\n*4\r\n$8\r\npmessage\r\n$1\r\n*\r\n$1\r\nÿ\r\n$1\r\nx\r\n"
            + "*3\r\n$7\r\nmessage\r\n$3\r\na/+\r\n$1\r\ny\r\n*4\r\n$8\r\npmessage\r\n$1\r\n*\r\n$3\r\na/+\r\n$1\r\ny\r\n",
            received);
    }

    [TestMethod]
    public async Task Unsubscribe_FromATopicAndAPattern_LeavesTheBus()
    {
        var server = new RedisTestServer();
        var subscriber = await server.OpenAsync(
            Token,
            Command("SUBSCRIBE", "a/b", "c"),
            Command("PSUBSCRIBE", "a*", "b*"),
            Command("PUNSUBSCRIBE", "a*"),
            Command("UNSUBSCRIBE", "a/b"));

        var stillPattern = server.Bus.Publish(new BusMessage("b/x", "x"u8.ToArray()));
        var leaving = await server.SendAsync(Token, Command("PUBLISH", "a/b", "x"));
        await subscriber.CloseAsync();
        var gone = server.Bus.Publish(new BusMessage("b/x", "x"u8.ToArray()));

        Assert.AreEqual(1, stillPattern.DeliveredSubscriptionCount);
        Assert.AreEqual(":0\r\n", leaving);
        Assert.AreEqual(0, gone.DeliveredSubscriptionCount);
    }

    [TestMethod]
    public async Task PublishAndSubscribe_TheBusGrantsRefuse_AreNopermWithNothingSubscribed()
    {
        var grants = new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("a/b", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)] };
        var server = new RedisTestServer(bus: new MessageBus.MessageBus(TimeProvider.System, configuration: grants));
        const string NoPermission = "-NOPERM this user has no permissions to access one of the channels used as arguments\r\n";

        var written = await server.SendAsync(
            Token,
            Command("PUBLISH", "a/b", "x"),
            Command("SUBSCRIBE", "a/b", "c"),
            Command("PSUBSCRIBE", "a*"),
            Command("UNSUBSCRIBE"),
            Command("PUNSUBSCRIBE"));

        Assert.AreEqual(
            NoPermission + NoPermission + NoPermission
            + "*3\r\n$11\r\nunsubscribe\r\n$-1\r\n:0\r\n*3\r\n$12\r\npunsubscribe\r\n$-1\r\n:0\r\n",
            written);
    }

    [TestMethod]
    public async Task Subscribe_PastTheBusBound_IsRefusedWithNothingSubscribed()
    {
        var server = new RedisTestServer(bus: new MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1));

        var written = await server.SendAsync(Token, Command("SUBSCRIBE", "a", "b"), Command("UNSUBSCRIBE"));

        Assert.AreEqual(
            "-ERR max number of subscriptions reached\r\n*3\r\n$11\r\nunsubscribe\r\n$-1\r\n:0\r\n",
            written);
    }

    [TestMethod]
    public async Task Publish_PastTheBusMessageBound_IsOutOfMemory()
    {
        var server = new RedisTestServer(bus: new MessageBus.MessageBus(TimeProvider.System, maxMessageBytes: 1));

        var written = await server.SendAsync(Token, Command("PUBLISH", "a", "xy"));

        Assert.AreEqual("-OOM command not allowed when used memory > 'maxmemory'.\r\n", written);
    }

    [TestMethod]
    public async Task TopicMap_ARedisChannelAlias_ReachesTheMappedTopicAndComesBackAsTheChannel()
    {
        var maps = new MessageBusConfiguration { TopicMaps = [new TopicMap("redis", "orders", "mqtt", "shop/orders")] };
        var server = new RedisTestServer(bus: new MessageBus.MessageBus(TimeProvider.System, configuration: maps));
        var subscriber = await server.OpenAsync(Token, Command("SUBSCRIBE", "orders"), Command("PSUBSCRIBE", "ord*"));

        var fromMqtt = server.Bus.Publish(new BusMessage("shop/orders", "x"u8.ToArray()));
        var unreachable = server.Bus.Publish(new BusMessage("orders", "y"u8.ToArray()));
        var fromRedis = await server.SendAsync(Token, Command("PUBLISH", "orders", "z"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(2, fromMqtt.DeliveredSubscriptionCount);
        Assert.AreEqual(1, unreachable.DeliveredSubscriptionCount);
        Assert.AreEqual(":2\r\n", fromRedis);
        Assert.EndsWith(
            "*3\r\n$7\r\nmessage\r\n$6\r\norders\r\n$1\r\nx\r\n*4\r\n$8\r\npmessage\r\n$4\r\nord*\r\n$6\r\norders\r\n$1\r\nx\r\n"
            + "*3\r\n$7\r\nmessage\r\n$6\r\norders\r\n$1\r\nz\r\n*4\r\n$8\r\npmessage\r\n$4\r\nord*\r\n$6\r\norders\r\n$1\r\nz\r\n",
            received);
    }

    [TestMethod]
    public async Task Publish_ThroughTwoServersOnOneBus_ReachesTheOtherServersSubscriber()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System);
        var first = new RedisTestServer(bus: bus);
        var second = new RedisTestServer(bus: bus);
        var subscriber = await first.OpenAsync(Token, Command("SUBSCRIBE", "news"));

        var published = await second.SendAsync(Token, Command("PUBLISH", "news", "hi"));
        var received = await subscriber.CloseAsync();

        Assert.AreEqual(":1\r\n", published);
        Assert.EndsWith("*3\r\n$7\r\nmessage\r\n$4\r\nnews\r\n$2\r\nhi\r\n", received);
    }
}
