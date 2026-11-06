namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The bytes waiting for one session's connection, and their limit (ADR-0120, decision 7).
/// </summary>
[TestClass]
public sealed class SocketIoOutboxTests
{
    [TestMethod]
    public async Task TryQueue_WithinTheLimit_WritesEachItemInOrder()
    {
        var outbox = new SocketIoOutbox(4);
        var connection = new UnitTestDuplexConnection();

        Assert.IsTrue(outbox.TryQueue([1, 2]));
        Assert.IsTrue(outbox.TryQueue([3, 4]));
        outbox.Complete();
        await outbox.WriteAllAsync(connection, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task TryQueue_PastTheLimitUntilWritten_IsRefusedThenTakenOnceWritten()
    {
        var outbox = new SocketIoOutbox(4);
        var connection = new UnitTestDuplexConnection();
        Assert.IsTrue(outbox.TryQueue([1, 2, 3]));

        Assert.IsFalse(outbox.TryQueue([4, 5]));

        var writing = outbox.WriteAllAsync(connection, CancellationToken.None);
        await connection.WaitForWrittenAsync(bytes => bytes.Length == 3);
        Assert.IsTrue(outbox.TryQueue([4, 5]));
        outbox.Complete();
        await writing;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task TryQueue_AfterComplete_IsDropped()
    {
        var outbox = new SocketIoOutbox(SocketIoOutbox.MaxUnsentBytes);
        var connection = new UnitTestDuplexConnection();
        outbox.Complete();

        Assert.IsTrue(outbox.TryQueue([1]));

        await outbox.WriteAllAsync(connection, CancellationToken.None);
        Assert.IsEmpty(connection.WrittenBytes);
    }
}
