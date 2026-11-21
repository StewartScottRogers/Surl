

    [TestMethod]
    public async Task WatchForPeerCloseAsync_ScriptEndsWithHalfClose_ReportsPeerClosed_AndKeepsEveryByte()
    {
        var connection = new InMemoryConnection([new byte[] { 1, 2 }, new byte[] { 3 }]);

        var outcome = await connection.WatchForPeerCloseAsync(16, TestContext.CancellationToken);
        var buffer = new byte[8];
        var first = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var second = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.PeerClosed, outcome);
        Assert.AreEqual((2, 1), (first, second));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_ScriptedToReset_ReportsPeerReset()
    {
        var connection = new InMemoryConnection([], peerResetsWhenExhausted: true);

        Assert.AreEqual(PeerWatchOutcome.PeerReset, await connection.WatchForPeerCloseAsync(16, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_MoreUnreadBytesThanTheLimit_ReportsBufferFull_CountingFromTheReadOffset()
    {
        var connection = new InMemoryConnection([new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }]);
        await connection.ReadAsync(new byte[1], TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.BufferFull, await connection.WatchForPeerCloseAsync(3, TestContext.CancellationToken));
        Assert.AreEqual(PeerWatchOutcome.PeerClosed, await connection.WatchForPeerCloseAsync(4, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_ClientStays_WaitsUntilCancelled()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        using var cancel = new CancellationTokenSource();

        var watching = connection.WatchForPeerCloseAsync(16, cancel.Token).AsTask();
        var stillWatching = !watching.IsCompleted;
        await cancel.CancelAsync();

        Assert.IsTrue(stillWatching);
        await Assert.ThrowsAsync<OperationCanceledException>(() => watching);
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_NonPositiveLimit_Throws()
    {
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await connection.WatchForPeerCloseAsync(0, TestContext.CancellationToken));
    }
}