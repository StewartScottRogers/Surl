using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The 64 datagrams a session holds while they wait for their echo (ADR-0117 decision 8).</summary>
[TestClass]
public sealed class WebTransportDatagramEchoQueueTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task TryEnqueue_SixtyFifthWaitingDatagram_IsDroppedWhileTheFirstSixtyFourAreEchoed()
    {
        var queue = new WebTransportDatagramEchoQueue();

        for (byte number = 0; number < WebTransportDatagramEchoQueue.Capacity; number++)
        {
            Assert.IsTrue(queue.TryEnqueue(new[] { number }), $"Datagram {number + 1} waits.");
        }

        Assert.IsFalse(queue.TryEnqueue(new byte[] { 200 }), "The 65th is dropped.");
        queue.Complete();
        var echoed = new List<byte>();
        await foreach (var payload in queue.TakeAsync(Token))
        {
            echoed.Add(payload.Span[0]);
        }

        Assert.AreEqual(64, WebTransportDatagramEchoQueue.Capacity);
        CollectionAssert.AreEqual(Enumerable.Range(0, 64).Select(number => (byte)number).ToArray(), echoed);
    }

    [TestMethod]
    public async Task TryEnqueue_AfterAnEchoFreesASlot_TakesTheNextDatagram()
    {
        var queue = new WebTransportDatagramEchoQueue();
        for (byte number = 0; number < WebTransportDatagramEchoQueue.Capacity; number++)
        {
            _ = queue.TryEnqueue(new[] { number });
        }

        await using var taking = queue.TakeAsync(Token).GetAsyncEnumerator(Token);
        Assert.IsTrue(await taking.MoveNextAsync());

        Assert.IsTrue(queue.TryEnqueue(new byte[] { 200 }));
        Assert.IsFalse(queue.TryEnqueue(new byte[] { 201 }));
    }

    [TestMethod]
    public void Enqueue_SixtyFifthWaitingDatagram_IsDroppedAndNotedInTheLog()
    {
        var queue = new WebTransportDatagramEchoQueue();
        var log = new RecordingExchangeLog();

        for (var number = 0; number <= WebTransportDatagramEchoQueue.Capacity; number++)
        {
            queue.Enqueue(new byte[] { 1, 2 }, log, 0);
        }

        Assert.HasCount(1, log.Notes);
        Assert.Contains("was dropped: 64 datagrams already wait for their echo", log.Notes[0]);
    }
}