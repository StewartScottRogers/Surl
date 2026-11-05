using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>How a connection's replies and pub/sub messages are written (BL-491; ADR-0106 decision 6).</summary>
[TestClass]
public sealed class RedisReplyWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Deliver_WithinTheBound_WritesTheMessageAfterTheReplies()
    {
        var connection = Connection([], peerHalfCloses: false);
        var writer = new RedisReplyWriter(connection, 8, TestContext.CancellationToken);

        await writer.WriteAsync("+OK\r\n"u8.ToArray());
        writer.Deliver("12345678"u8.ToArray());
        writer.Deliver("ab"u8.ToArray());

        Assert.AreEqual("+OK\r\n12345678ab", Written(connection));
        Assert.IsFalse(writer.IsClosedByWriter);
    }

    [TestMethod]
    public async Task Deliver_AfterWritesAreComplete_ClosesTheConnection()
    {
        var connection = Connection([], peerHalfCloses: false);
        var writer = new RedisReplyWriter(connection, 100, TestContext.CancellationToken);
        await writer.CompleteWritesAsync();

        writer.Deliver("x"u8.ToArray());

        Assert.IsTrue(writer.IsClosedByWriter);
        Assert.IsTrue(writer.Token.IsCancellationRequested);
        Assert.IsEmpty(Written(connection));
    }

    [TestMethod]
    public void Deliver_PastTheBound_ClosesTheConnectionWithoutWriting()
    {
        var connection = Connection([], peerHalfCloses: false);
        var writer = new RedisReplyWriter(connection, 4, TestContext.CancellationToken);

        writer.Deliver("12345"u8.ToArray());

        Assert.IsTrue(writer.IsClosedByWriter);
        Assert.IsEmpty(Written(connection));
    }
}
