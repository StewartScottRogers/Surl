using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class InMemoryDatagramFlowTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NoEndPoints_UsesTheLoopbackDefaults()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 1 });

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), flow.LocalEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), flow.RemoteEndPoint);
        CollectionAssert.AreEqual(new byte[] { 1 }, flow.FirstDatagram.ToArray());
        Assert.IsEmpty(flow.SentDatagrams);
        Assert.IsFalse(flow.Disposed);
    }

    [TestMethod]
    public void Constructor_GivenEndPoints_KeepsThem()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 8443);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 60000);

        var flow = new InMemoryDatagramFlow(new byte[] { 1 }, null, local, remote);

        Assert.AreSame(local, flow.LocalEndPoint);
        Assert.AreSame(remote, flow.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_ClientDatagrams_ReturnsThemInOrderOnePerReceive()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 }, [new byte[] { 1, 2 }, new byte[] { 3 }]);

        var first = await flow.ReceiveAsync(TestContext.CancellationToken);
        var second = await flow.ReceiveAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, first.ToArray());
        CollectionAssert.AreEqual(new byte[] { 3 }, second.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_DeliveredAfterTheConstructorsDatagrams_ComesAfterThem()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 }, [new byte[] { 1 }]);

        flow.DeliverFromClient(new byte[] { 2 });

        CollectionAssert.AreEqual(new byte[] { 1 }, (await flow.ReceiveAsync(TestContext.CancellationToken)).ToArray());
        CollectionAssert.AreEqual(new byte[] { 2 }, (await flow.ReceiveAsync(TestContext.CancellationToken)).ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_NothingWaiting_CompletesWhenADatagramIsDelivered()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        var receive = flow.ReceiveAsync(TestContext.CancellationToken).AsTask();
        Assert.IsFalse(receive.IsCompleted);
        flow.DeliverFromClient(new byte[] { 7 });

        CollectionAssert.AreEqual(new byte[] { 7 }, (await receive).ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_NothingWaitingAndCancelled_ThrowsOperationCanceled()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });
        using var cancellation = new CancellationTokenSource();

        var receive = flow.ReceiveAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => receive);
    }

    [TestMethod]
    public async Task ReceiveAsync_AlreadyCancelled_ThrowsOperationCanceled()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 }, [new byte[] { 1 }]);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await flow.ReceiveAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task ReceiveAsync_Waiting_ThrowsObjectDisposedWhenTheFlowIsDisposed()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        var receive = flow.ReceiveAsync(TestContext.CancellationToken).AsTask();
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => receive);
    }

    [TestMethod]
    public async Task ReceiveAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 }, [new byte[] { 1 }]);
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await flow.ReceiveAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SendAsync_TwoDatagrams_RecordsCopiesInOrder()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });
        var first = new byte[] { 1, 2 };

        await flow.SendAsync(first, TestContext.CancellationToken);
        first[0] = 9;
        await flow.SendAsync(new byte[] { 3 }, TestContext.CancellationToken);

        Assert.HasCount(2, flow.SentDatagrams);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, flow.SentDatagrams[0]);
        CollectionAssert.AreEqual(new byte[] { 3 }, flow.SentDatagrams[1]);
    }

    [TestMethod]
    public async Task SendAsync_Cancelled_ThrowsAndRecordsNothing()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await flow.SendAsync(new byte[] { 1 }, new CancellationToken(canceled: true)));
        Assert.IsEmpty(flow.SentDatagrams);
    }

    [TestMethod]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await flow.SendAsync(new byte[] { 1 }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_NoEndPointGiven_MovesToLoopbackPort50001()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50001), flow.LocalEndPoint);
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_EndPointGiven_MovesToIt()
    {
        var moved = new IPEndPoint(IPAddress.Loopback, 61000);
        var flow = new InMemoryDatagramFlow(new byte[] { 0 }, newLocalEndPoint: moved);

        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        Assert.AreSame(moved, flow.LocalEndPoint);
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_Cancelled_Throws()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await flow.MoveToNewLocalPortAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeliverFromClient_AfterDispose_ThrowsObjectDisposed()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });
        await flow.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => flow.DeliverFromClient(new byte[] { 1 }));
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_IsHarmless()
    {
        var flow = new InMemoryDatagramFlow(new byte[] { 0 });

        await flow.DisposeAsync();
        await flow.DisposeAsync();

        Assert.IsTrue(flow.Disposed);
    }
}
