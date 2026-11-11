namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsOutboxTests
{
    [TestMethod]
    public void Enqueue_AfterComplete_IsDroppedWithoutOverflowing()
    {
        using var outbox = new NatsOutbox(maxPendingBytes: 1000);
        outbox.Complete();

        outbox.Enqueue(new byte[10]);

        Assert.IsFalse(outbox.HasOverflowed);
    }

    [TestMethod]
    public void Enqueue_PastTheBound_Overflows()
    {
        using var outbox = new NatsOutbox(maxPendingBytes: 10);

        outbox.Enqueue(new byte[11]);

        Assert.IsTrue(outbox.HasOverflowed);
    }

    [TestMethod]
    public void Enqueue_PastTheBoundTwice_StaysOverflowed()
    {
        using var outbox = new NatsOutbox(maxPendingBytes: 10);

        outbox.Enqueue(new byte[11]);
        outbox.Enqueue(new byte[11]);

        Assert.IsTrue(outbox.HasOverflowed);
    }
}
