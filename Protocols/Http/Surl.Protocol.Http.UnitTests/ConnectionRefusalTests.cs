using Surl.Hpack;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Http2.Http2TestClient;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class ConnectionRefusalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_WritesTheRecorded503AndCompletesWritesWithoutAbort(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("refusal-503")], peerHalfClosesWhenExhausted: false);

        await Server().WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("refusal-503"), connection.WrittenBytes);
        Assert.AreEqual("HTTP/1.1 503 Service Unavailable\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_ClientHalfClosesWithNoBytes_WritesTheHttp11503()
    {
        var connection = new InMemoryConnection([]);

        await Server().WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("refusal-503"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_RecordedPriorKnowledgeGetOfAFile_AnswersSettingsThen503OnStream1ThenGoAwayNoError()
    {
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("h2-prior-knowledge-get-file")], peerHalfClosesWhenExhausted: false);

        await Server().WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        var frames = Frames(connection.WrittenBytes);
        Assert.HasCount(3, frames);
        Assert.IsTrue(frames[0] is Http2SettingsFrame { Ack: false, Settings.Count: 0 }, "An empty SETTINGS comes first.");
        Assert.IsTrue(frames[1] is Http2HeadersFrame { StreamId: 1, EndStream: true, EndHeaders: true });
        var response = Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("503", response.Status);
        CollectionAssert.AreEqual(
            new HpackHeaderField[] { new(":status", "503"), new("server", "surl"), new("content-length", "0") },
            response.Fields.Select(field => new HpackHeaderField(field.Name, field.Value)).ToArray());
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.IsTrue(frames[2] is Http2GoAwayFrame { LastStreamId: 1, ErrorCode: Http2ErrorCode.NoError });
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_PrefaceWithNoHeadersWithinTheDeadline_ClosesWithNoBytes()
    {
        var connection = new InMemoryConnection([Start()], peerHalfClosesWhenExhausted: false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var refusing = Server().WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, deadline.Token).AsTask();

        await deadline.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => refusing);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NoBytesWithinTheDeadline_ClosesWithNoBytes()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var refusing = Server().WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, deadline.Token).AsTask();

        await deadline.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => refusing);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Server().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Server_IsAConnectionRefusalWriter()
    {
        Assert.IsInstanceOfType<IConnectionRefusalWriter>(Server());
    }
}
