namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// HTTP Post's side of the hub: one payload at a time, answered once the session has processed it
/// (TransportProtocols.md "HTTP Post"; ADR-0119, decision 2, amendment 1).
/// </summary>
[TestClass]
public sealed class SignalRPostedPayloadsTests
{
    private static readonly SignalRSessionEnd Ended = SignalRSessionEnd.Silent("a test ended it");

    [TestMethod]
    public async Task Post_PayloadTheSessionReads_CompletesOnceTheSessionAsksAgain()
    {
        var posts = new SignalRPostedPayloads();

        var processed = posts.Post([1, 2]);
        var read = await posts.ReadAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, read.Payload);
        Assert.IsFalse(processed!.IsCompleted);
        var next = posts.ReadAsync(CancellationToken.None);
        Assert.IsTrue(await processed);
        posts.Close(Ended);
        Assert.AreSame(Ended, (await next).End);
    }

    [TestMethod]
    public async Task Post_WhileTheSessionWaits_HandsItThePayload()
    {
        var posts = new SignalRPostedPayloads();
        var waiting = posts.ReadAsync(CancellationToken.None);

        var processed = posts.Post([7]);

        CollectionAssert.AreEqual(new byte[] { 7 }, (await waiting).Payload);
        Assert.IsFalse(processed!.IsCompleted);
    }

    [TestMethod]
    public void Post_AnEarlierPostStillOutstanding_IsRefused()
    {
        var posts = new SignalRPostedPayloads();
        posts.Post([1]);

        Assert.IsNull(posts.Post([2]));
    }

    [TestMethod]
    public async Task Post_AfterTheConnectionEnded_CompletesFalse()
    {
        var posts = new SignalRPostedPayloads();
        posts.Close(Ended);

        Assert.IsFalse(await posts.Post([1])!);
    }

    [TestMethod]
    public async Task Close_WithAPostOutstanding_CompletesItFalse()
    {
        var posts = new SignalRPostedPayloads();
        var processed = posts.Post([1]);

        posts.Close(Ended);
        posts.Close(SignalRSessionEnd.Silent("a second end"));

        Assert.IsFalse(await processed!);
        Assert.AreSame(Ended, (await posts.ReadAsync(CancellationToken.None)).End);
    }

    [TestMethod]
    public async Task Close_WhileTheSessionWaits_EndsItsRead()
    {
        var posts = new SignalRPostedPayloads();
        var waiting = posts.ReadAsync(CancellationToken.None);

        posts.Close(Ended);

        Assert.AreSame(Ended, (await waiting).End);
    }

    [TestMethod]
    public async Task ReadAsync_Cancelled_Throws()
    {
        var posts = new SignalRPostedPayloads();
        using var cancellation = new CancellationTokenSource();
        var waiting = posts.ReadAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }
}
