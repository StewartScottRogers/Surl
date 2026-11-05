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
        var server = new RedisTestServer(pubSub: new RedisPubSub(2, RedisPubSub.DefaultMaxUnsentMessageBytes));

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
        var server = new RedisTestServer(pubSub: new RedisPubSub(RedisPubSub.DefaultMaxSubscriptionsPerConnection, 30));
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
}
