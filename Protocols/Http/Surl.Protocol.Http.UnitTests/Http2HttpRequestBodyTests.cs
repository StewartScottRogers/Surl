using System.Security.Cryptography;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2HttpRequestBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CopyToAsync_WholeBody_CopiesItReplenishesEachPieceAndLeavesNoneAnnounced()
    {
        var test = new Http2TestStream(Http2TestStream.Get(contentLength: 5), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 0);
        test.Receive("abc");
        test.Receive("de");
        test.End();
        using var destination = new MemoryStream();

        var outcome = await body.CopyToAsync(destination, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, outcome);
        Assert.AreEqual("abcde", HttpServerHarness.Latin1(destination.ToArray()));
        CollectionAssert.AreEqual(new[] { 3, 2 }, test.Replenished);
        Assert.IsFalse(body.IsAnnounced);
        Assert.IsTrue(body.IsReadable);
        Assert.AreEqual(5, body.AnnouncedLength);
        Assert.IsFalse(test.Stream.HasContentLengthMismatch);
        Assert.IsEmpty(test.Connection.WrittenBytes, "No 100 is sent to a request that did not ask for one.");
    }

    [TestMethod]
    public async Task CopyToAsync_BodyAbandoned_IsIncompleteWithoutAMismatch()
    {
        var test = new Http2TestStream(Http2TestStream.Get(contentLength: 5), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 0);
        test.Receive("abc");
        test.Abandon();
        using var destination = new MemoryStream();

        var outcome = await body.CopyToAsync(destination, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Incomplete, outcome);
        Assert.IsFalse(test.Stream.HasContentLengthMismatch);
        Assert.IsTrue(body.IsAnnounced);
    }

    [TestMethod]
    public async Task DiscardAsync_ShorterThanItsContentLength_IsIncompleteWithAMismatch()
    {
        var test = new Http2TestStream(Http2TestStream.Get(contentLength: 5), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 0);
        test.Receive("abc");
        test.End();

        var outcome = await body.DiscardAsync(null, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Incomplete, outcome);
        Assert.IsTrue(test.Stream.HasContentLengthMismatch);
    }

    [TestMethod]
    public async Task DiscardAsync_PastTheUploadLimit_IsTooLargeAndReplenishesNothing()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 2);
        test.Receive("abc");
        test.End();

        var outcome = await body.DiscardAsync(null, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.TooLarge, outcome);
        Assert.IsEmpty(test.Replenished);
    }

    [TestMethod]
    public async Task DiscardAsync_WithAHash_HashesEveryPiece()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 0);
        test.Receive("abc");
        test.End();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var outcome = await body.DiscardAsync(hash, TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, outcome);
        CollectionAssert.AreEqual(SHA256.HashData(HttpServerHarness.Ascii("abc")), hash.GetHashAndReset());
    }

    [TestMethod]
    public async Task DiscardAsync_ExpectsContinue_SendsAnInterim100FirstAndOnlyWhileTheBodyIsAnnounced()
    {
        var test = new Http2TestStream(Http2TestStream.Get(expectsContinue: true), isBodyAnnounced: true, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: true, maxUploadBytes: 0);
        test.End();

        await body.DiscardAsync(null, TestContext.CancellationToken);
        await body.DiscardAsync(null, TestContext.CancellationToken);

        var response = Http2TestClient.Responses(test.Connection.WrittenBytes)[1];
        Assert.HasCount(1, response.HeaderBlocks);
        Assert.AreEqual("100", response.Status);
        Assert.IsFalse(response.EndedOnHeaders);
    }

    [TestMethod]
    public async Task CopyToAsync_NoDestination_Throws()
    {
        var test = new Http2TestStream(Http2TestStream.Get(), isBodyAnnounced: false, TestContext.CancellationToken);
        var body = new Http2HttpRequestBody(test.Stream, expectsContinue: false, maxUploadBytes: 0);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => body.CopyToAsync(null!, TestContext.CancellationToken));
    }
}
