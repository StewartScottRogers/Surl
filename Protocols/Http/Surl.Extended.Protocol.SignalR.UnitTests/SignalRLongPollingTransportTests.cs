namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The Long Polling transport's polls (TransportProtocols.md "Long Polling"; ADR-0119, decision 2,
/// amendment 1).
/// </summary>
[TestClass]
public sealed class SignalRLongPollingTransportTests
{
    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    public async Task PollAsync_RecordsWaiting_AnswersThemTogether()
    {
        using var transport = NewTransport();
        await transport.WriteRecordAsync([1, 2], false, CancellationToken.None);
        await transport.WriteRecordAsync([3], false, CancellationToken.None);

        var result = await transport.PollAsync(CancellationToken.None);

        Assert.AreEqual(200, result.Status);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.Body);
    }

    [TestMethod]
    public async Task PollAsync_RecordSentWhileWaiting_AnswersIt()
    {
        using var transport = NewTransport();
        var poll = transport.PollAsync(CancellationToken.None);

        await transport.WriteRecordAsync([9], false, CancellationToken.None);

        var result = await poll;
        Assert.AreEqual(200, result.Status);
        CollectionAssert.AreEqual(new byte[] { 9 }, result.Body);
    }

    [TestMethod]
    public async Task PollAsync_NothingSentWithinThePollTimeout_AnswersOkWithNoContent()
    {
        using var transport = NewTransport();
        var poll = transport.PollAsync(CancellationToken.None);

        await clock.AdvanceWhenWaitingAsync(SignalRLongPollingTransport.PollTimeout);

        var result = await poll;
        Assert.AreEqual(200, result.Status);
        Assert.IsEmpty(result.Body);
    }

    [TestMethod]
    public async Task PollAsync_ANewerPollArrives_CompletesTheOlderOneWithNoContent()
    {
        using var transport = NewTransport();
        var older = transport.PollAsync(CancellationToken.None);

        var newer = transport.PollAsync(CancellationToken.None);

        var superseded = await older;
        Assert.AreEqual(204, superseded.Status);
        Assert.IsEmpty(superseded.Body);
        Assert.IsFalse(newer.IsCompleted);
        await transport.WriteRecordAsync([5], false, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 5 }, (await newer).Body);
    }

    [TestMethod]
    public async Task PollAsync_RequestGoesAway_ThrowsAndLeavesNoPollWaiting()
    {
        using var transport = NewTransport();
        using var cancellation = new CancellationTokenSource();
        var poll = transport.PollAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => poll);
        await transport.WriteRecordAsync([4], false, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 4 }, (await transport.PollAsync(CancellationToken.None)).Body);
    }

    [TestMethod]
    public async Task Delete_WhileAPollWaits_CompletesItWithNoContentAndEndsThePosts()
    {
        var posts = new SignalRPostedPayloads();
        using var transport = new SignalRLongPollingTransport(posts, clock);
        await transport.WriteRecordAsync([1], false, CancellationToken.None);
        await transport.PollAsync(CancellationToken.None);
        var poll = transport.PollAsync(CancellationToken.None);

        transport.Delete();

        Assert.AreEqual(204, (await poll).Status);
        Assert.AreEqual(204, (await transport.PollAsync(CancellationToken.None)).Status);
        Assert.IsNotNull((await posts.ReadAsync(CancellationToken.None)).End);
    }

    [TestMethod]
    public async Task Delete_RecordsStillWaiting_DropsThem()
    {
        using var transport = NewTransport();
        await transport.WriteRecordAsync([1], false, CancellationToken.None);

        transport.Delete();

        Assert.AreEqual(204, (await transport.PollAsync(CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task WriteEndAsync_WithAHubClose_AnswersItThenNoContent()
    {
        var posts = new SignalRPostedPayloads();
        using var transport = new SignalRLongPollingTransport(posts, clock);
        var poll = transport.PollAsync(CancellationToken.None);

        await transport.WriteEndAsync(SignalRSessionEnd.HubProtocolError("a test"), [7, 7], false, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 7, 7 }, (await poll).Body);
        Assert.AreEqual(204, (await transport.PollAsync(CancellationToken.None)).Status);
        Assert.IsNotNull((await posts.ReadAsync(CancellationToken.None)).End);
    }

    [TestMethod]
    public async Task WriteEndAsync_WithoutAHubClose_AnswersNoContent()
    {
        using var transport = NewTransport();

        await transport.WriteEndAsync(SignalRSessionEnd.Silent("a test"), null, false, CancellationToken.None);

        Assert.AreEqual(204, (await transport.PollAsync(CancellationToken.None)).Status);
    }

    [TestMethod]
    public void CarriesBinary_IsTrue()
    {
        using var transport = NewTransport();

        Assert.IsTrue(transport.CarriesBinary);
    }

    private SignalRLongPollingTransport NewTransport() => new(new SignalRPostedPayloads(), clock);
}
