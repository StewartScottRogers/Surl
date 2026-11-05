namespace Surl.Http2;

[TestClass]
public sealed class Http2RequestBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_WholeBody_ReadsEachPieceAndReplenishesEachWhenTheNextIsAskedFor()
    {
        var replenished = new List<int>();
        var body = Body(replenished, contentLength: 5);
        body.Receive(Http2TestBytes.Ascii("abc"));
        body.Receive(Http2TestBytes.Ascii("de"));
        body.End();

        var first = await body.ReadAsync(TestContext.CancellationToken);
        var replenishedAfterFirst = replenished.ToArray();
        var second = await body.ReadAsync(TestContext.CancellationToken);
        var end = await body.ReadAsync(TestContext.CancellationToken);

        Assert.AreEqual("abc", Http2TestBytes.Latin1(first!));
        Assert.AreEqual("de", Http2TestBytes.Latin1(second!));
        Assert.IsNull(end);
        Assert.IsEmpty(replenishedAfterFirst, "A piece's window comes back only once the handler asks for the next.");
        CollectionAssert.AreEqual(new[] { 3, 2 }, replenished);
        Assert.IsTrue(body.IsAnnounced);
        Assert.AreEqual(5, body.AnnouncedLength);
        Assert.IsFalse(body.IsStillArriving);
        Assert.IsFalse(body.HasEndedEarly);
        Assert.IsFalse(body.HasContentLengthMismatch);
    }

    [TestMethod]
    public async Task ReadAsync_BodyShorterThanItsContentLength_EndsWithAMismatch()
    {
        var body = Body([], contentLength: 5);
        body.Receive(Http2TestBytes.Ascii("abc"));
        body.End();

        _ = await body.ReadAsync(TestContext.CancellationToken);
        var end = await body.ReadAsync(TestContext.CancellationToken);

        Assert.IsNull(end);
        Assert.IsTrue(body.HasContentLengthMismatch);
    }

    [TestMethod]
    public async Task ReadAsync_BodyAbandonedTwice_EndsEarlyWithoutAMismatch()
    {
        var body = Body([], contentLength: 5);
        body.Receive(Http2TestBytes.Ascii("abc"));
        body.Abandon();
        body.Abandon();

        _ = await body.ReadAsync(TestContext.CancellationToken);
        var end = await body.ReadAsync(TestContext.CancellationToken);

        Assert.IsNull(end);
        Assert.IsTrue(body.HasEndedEarly);
        Assert.IsFalse(body.HasContentLengthMismatch);
        Assert.IsFalse(body.IsStillArriving);
    }

    [TestMethod]
    public async Task ReadAsync_EndedThenAbandoned_IsWhole()
    {
        var body = Body([], contentLength: null);
        body.End();
        body.Abandon();

        var end = await body.ReadAsync(TestContext.CancellationToken);

        Assert.IsNull(end);
        Assert.IsFalse(body.HasEndedEarly);
        Assert.IsFalse(body.HasContentLengthMismatch);
    }

    [TestMethod]
    public void Constructor_NotAnnounced_HasEndedAlready()
    {
        var body = new Http2RequestBody(isAnnounced: false, null, _ => Task.CompletedTask);

        Assert.IsFalse(body.IsAnnounced);
        Assert.IsFalse(body.IsStillArriving);
        Assert.IsNull(body.AnnouncedLength);
    }

    private static Http2RequestBody Body(List<int> replenished, long? contentLength) => new(
        isAnnounced: true,
        contentLength,
        increment =>
        {
            replenished.Add(increment);

            return Task.CompletedTask;
        });
}
