using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The hand-written <c>multipart/related</c> reader (RFC 2387; ADR-0148, decision 7): fed a byte at a time, so every split of a
/// delimiter across two writes is met.
/// </summary>
[TestClass]
public sealed class GcsMultipartRelatedSinkTests
{
    private const string Body = "--b\r\nContent-Type: application/json\r\n\r\n{\"name\":\"x\"}\r\n--b\r\nContent-Type: text/plain\r\n\r\nhello\r\n-- world\r\n-\r\n--b--";

    [TestMethod]
    public async Task WriteAsync_TheBodyOneByteAtATime_ReadsBothPartsWhole()
    {
        var content = new MemoryStream();
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(content));

        foreach (var value in Encoding.ASCII.GetBytes(Body))
        {
            await sink.WriteAsync(new[] { value }, 0, 1, CancellationToken.None);
        }

        Assert.IsTrue(sink.IsComplete);
        Assert.IsNull(sink.Problem);
        Assert.AreEqual("{\"name\":\"x\"}", Encoding.ASCII.GetString(sink.MetadataPart));
        Assert.AreEqual("hello\r\n-- world\r\n-", Encoding.ASCII.GetString(content.ToArray()));
        Assert.AreEqual("text/plain", sink.ContentPartType);
    }

    [TestMethod]
    public async Task WriteAsync_TheWholeBodyAtOnce_ReadsBothPartsAndIgnoresWhatFollowsTheClose()
    {
        var content = new MemoryStream();
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(content));

        await sink.WriteAsync(Encoding.ASCII.GetBytes(Body + "\r\nepilogue"));
        await sink.WriteAsync(Encoding.ASCII.GetBytes("more epilogue"));

        Assert.IsTrue(sink.IsComplete);
        Assert.AreEqual("hello\r\n-- world\r\n-", Encoding.ASCII.GetString(content.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_AfterAProblem_DropsTheRest()
    {
        var content = new MemoryStream();
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(content));

        await sink.WriteAsync(Encoding.ASCII.GetBytes("junk"));
        await sink.WriteAsync(Encoding.ASCII.GetBytes(Body));

        Assert.AreEqual(GcsMultipartRelatedProblem.Malformed, sink.Problem);
        Assert.IsFalse(sink.IsComplete);
        Assert.AreEqual(0, content.Length);
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheContentIsRefused_StopsAsRefused()
    {
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(null));

        await sink.WriteAsync(Encoding.ASCII.GetBytes(Body));

        Assert.AreEqual(GcsMultipartRelatedProblem.Refused, sink.Problem);
    }

    [TestMethod]
    public async Task WriteAsync_AMetadataPartPast1MiB_IsMetadataTooLarge()
    {
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(new MemoryStream()));

        await sink.WriteAsync(Encoding.ASCII.GetBytes("--b\r\n\r\n"));
        await sink.WriteAsync(new byte[GcsMultipartRelatedSink.MaxMetadataBytes + 100]);

        Assert.AreEqual(GcsMultipartRelatedProblem.MetadataTooLarge, sink.Problem);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("application/json; boundary=b", null)]
    [DataRow("multipart/related", null)]
    [DataRow("multipart/related; charset=x", null)]
    [DataRow("multipart/related; boundary=", null)]
    [DataRow("multipart/related; boundary=\"\"", null)]
    [DataRow("multipart/related; boundary=a b", null)]
    [DataRow("multipart/related; boundary=é", null)]
    [DataRow("multipart/related; boundary=b", "b")]
    [DataRow("Multipart/Related ; Boundary=\"b-1\" ; charset=x", "b-1")]
    public void BoundaryOf_ContentType_IsTheBoundaryOrNull(string? contentType, string? boundary)
    {
        Assert.AreEqual(boundary, GcsMultipartRelatedSink.BoundaryOf(contentType));
    }

    [TestMethod]
    public async Task Members_OfAWriteOnlyStream_SayWhatTheyAre()
    {
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(null));

        Assert.IsFalse(sink.CanRead);
        Assert.IsFalse(sink.CanSeek);
        Assert.IsTrue(sink.CanWrite);
        sink.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => _ = sink.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => _ = sink.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => sink.Position = 1);
        Assert.ThrowsExactly<NotSupportedException>(() => sink.Read([], 0, 0));
        Assert.ThrowsExactly<NotSupportedException>(() => sink.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => sink.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => sink.Write([], 0, 0));
    }
}
