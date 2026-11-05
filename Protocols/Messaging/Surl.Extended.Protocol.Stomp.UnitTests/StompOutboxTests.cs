using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The one ordered writer of a connection and its slow-consumer bound (ADR-0118 decision 8).</summary>
[TestClass]
public sealed class StompOutboxTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAllAsync_QueuedWrites_AreWrittenInOrderAndCounted()
    {
        using var outbox = new StompOutbox(1024);
        var connection = new InMemoryConnection([]);
        outbox.Enqueue("a"u8.ToArray());
        outbox.Enqueue("b"u8.ToArray());
        outbox.Complete();

        await outbox.WriteAllAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual("ab", Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.AreEqual(2, outbox.EnqueuedCount);
        Assert.IsFalse(outbox.HasOverflowed);
    }

    [TestMethod]
    public async Task Enqueue_AfterComplete_IsDroppedWithoutOverflowing()
    {
        using var outbox = new StompOutbox(1024);
        var connection = new InMemoryConnection([]);
        outbox.Complete();

        outbox.Enqueue("late"u8.ToArray());
        await outbox.WriteAllAsync(connection, TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual(0, outbox.EnqueuedCount);
        Assert.IsFalse(outbox.HasOverflowed);
    }

    [TestMethod]
    public async Task Enqueue_PastTheBoundWhileTheWriterWaits_OverflowsAndEndsTheWriting()
    {
        using var outbox = new StompOutbox(4);
        var connection = new UnitTestStompClientConnection { HoldsWrites = true };
        var writing = outbox.WriteAllAsync(connection, TestContext.CancellationToken);
        outbox.Enqueue("ab"u8.ToArray());

        outbox.Enqueue("cde"u8.ToArray());
        outbox.Enqueue("x"u8.ToArray());
        await writing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(outbox.HasOverflowed);
        Assert.AreEqual(1, outbox.EnqueuedCount);
    }
}
