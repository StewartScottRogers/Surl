namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2RequestBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CopyToAsync_WholeBody_CopiesItReplenishesEachPieceAndLeavesNoneAnnounced()
    {
        var replenished = new List<int>();
        var body = Body(replenished, contentLength: 5);
        body.Receive(HttpServerHarness.Ascii("abc"));
        body.Receive(HttpServerHarness.Ascii("de"));
        body.End();
        using var destination = new MemoryStream();

        var outcome = await body.CopyToAsync(destination, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, outcome);
        Assert.AreEqual("abcde", HttpServerHarness.Latin1(destination.ToArray()));
        CollectionAssert.AreEqual(new[] { 3, 2 }, replenished);
        Assert.IsFalse(body.IsAnnounced);
        Assert.IsTrue(body.IsReadable);
        Assert.IsFalse(body.IsStillArriving);
        Assert.IsFalse(body.HasContentLengthMismatch);
    }

    [TestMethod]
    public async Task CopyToAsync_BodyAbandoned_IsIncompleteWithoutAMismatch()
    {
        var body = Body([], contentLength: 5);
        body.Receive(HttpServerHarness.Ascii("abc"));
        body.Abandon();
        body.Abandon();
        using var destination = new MemoryStream();

        var outcome = await body.CopyToAsync(destination, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Incomplete, outcome);
        Assert.IsFalse(body.HasContentLengthMismatch);
        Assert.IsTrue(body.IsAnnounced);
    }

    [TestMethod]
    public async Task DiscardAsync_EndedThenAbandoned_IsWhole()
    {
        var body = Body([], contentLength: null);
        body.End();
        body.Abandon();

        var outcome = await body.DiscardAsync(null, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, outcome);
    }

    [TestMethod]
    public async Task DiscardAsync_WithAHash_HashesEveryPiece()
    {
        var body = Body([], contentLength: null);
        body.Receive(HttpServerHarness.Ascii("abc"));
        body.End();
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

        var outcome = await body.DiscardAsync(hash, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, outcome);
        CollectionAssert.AreEqual(System.Security.Cryptography.SHA256.HashData(HttpServerHarness.Ascii("abc")), hash.GetHashAndReset());
    }

    [TestMethod]
    public async Task CopyToAsync_NoDestination_Throws()
    {
        var body = Body([], contentLength: null);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => body.CopyToAsync(null!, TestContext.CancellationToken));
    }

    private static Http2RequestBody Body(List<int> replenished, long? contentLength) => new(
        isAnnounced: true,
        contentLength,
        expectsContinue: false,
        maxUploadBytes: 0,
        increment =>
        {
            replenished.Add(increment);

            return Task.CompletedTask;
        },
        () => Task.CompletedTask);
}
