namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// What waits to be sent to one session, and its limit (ADR-0120, decision 7).
/// </summary>
[TestClass]
public sealed class SocketIoOutboxTests
{
    [TestMethod]
    public async Task TryQueue_WithinTheLimit_WritesEachItemInOrder()
    {
        var outbox = new SocketIoOutbox(4);
        var written = new List<byte>();

        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Pong([1, 2])));
        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Pong([3, 4])));
        outbox.Complete();
        await outbox.WriteAllAsync(Collect(written), CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, written);
    }

    [TestMethod]
    public async Task TryQueue_PastTheLimitUntilWritten_IsRefusedThenTakenOnceWritten()
    {
        var outbox = new SocketIoOutbox(4);
        var written = new List<byte>();
        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Pong([1, 2, 3])));

        Assert.IsFalse(outbox.TryQueue(SocketIoOutboxItem.Pong([4, 5])));

        var writing = outbox.WriteAllAsync(Collect(written), CancellationToken.None);
        var giveUpAt = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!outbox.TryQueue(SocketIoOutboxItem.Pong([4, 5])))
        {
            Assert.IsLessThan(giveUpAt, DateTime.UtcNow, "The first item was never written.");
            await Task.Delay(1);
        }

        outbox.Complete();
        await writing;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, written);
    }

    [TestMethod]
    public async Task TryQueue_AfterComplete_IsDropped()
    {
        var outbox = new SocketIoOutbox(SocketIoOutbox.MaxUnsentBytes);
        var written = new List<byte>();
        outbox.Complete();

        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Pong([1])));

        await outbox.WriteAllAsync(Collect(written), CancellationToken.None);
        Assert.IsEmpty(written);
        Assert.IsFalse(await outbox.WaitToTakeAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task TakeAll_QueuedItems_AreEveryPacketInOrderAndFreeTheLimit()
    {
        var outbox = new SocketIoOutbox(4);
        var ping = EngineIoPacket.Text(EngineIoPacketType.Ping, string.Empty);
        var attachment = EngineIoPacket.Binary(new byte[] { 9 });
        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Of(ping, attachment)));

        Assert.IsTrue(await outbox.WaitToTakeAsync(CancellationToken.None));
        var packets = outbox.TakeAll();

        CollectionAssert.AreEqual(new[] { ping, attachment }, packets);
        Assert.IsTrue(outbox.TryQueue(SocketIoOutboxItem.Pong([1, 2, 3, 4])));
    }

    private static Func<SocketIoOutboxItem, CancellationToken, Task> Collect(List<byte> written) => (item, _) =>
    {
        written.AddRange(item.WebSocketPong!);

        return Task.CompletedTask;
    };
}
