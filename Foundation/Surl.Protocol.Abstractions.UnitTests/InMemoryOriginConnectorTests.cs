using System.Text;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// <see cref="InMemoryOriginConnector"/>, the <see cref="IOriginConnector"/> proxy tests use
/// (ADR-0088, decision 2).
/// </summary>
[TestClass]
public sealed class InMemoryOriginConnectorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_ScriptedOrigin_HandsOutItsConnectionAndRecordsTheRequest()
    {
        var origin = new InMemoryConnection([Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n")]);
        IOriginConnector connector = new InMemoryOriginConnector().ScriptConnection("origin.example", 8080, origin);

        var connection = await connector.ConnectAsync("origin.example", 8080, Timeout, TestContext.CancellationToken);
        await connection.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"), TestContext.CancellationToken);

        Assert.AreSame(origin, connection);
        Assert.AreEqual("GET / HTTP/1.1\r\n\r\n", Encoding.ASCII.GetString(origin.WrittenBytes));
        CollectionAssert.AreEqual(
            new[] { new OriginConnectionRequest("origin.example", 8080, Timeout) },
            ((InMemoryOriginConnector)connector).Requests.ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_HostDiffersOnlyInCase_IsTheSameOrigin()
    {
        var origin = new InMemoryConnection([]);
        var connector = new InMemoryOriginConnector().ScriptConnection("Origin.Example", 80, origin);

        var connection = await connector.ConnectAsync("origin.example", 80, Timeout, TestContext.CancellationToken);

        Assert.AreSame(origin, connection);
    }

    [TestMethod]
    public async Task ConnectAsync_RefusedOrigin_ThrowsNotAllowed()
    {
        var connector = new InMemoryOriginConnector().ScriptFailure("192.0.2.1", 80, OriginConnectionFailure.NotAllowed);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("192.0.2.1", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.NotAllowed, exception.Failure);
        StringAssert.Contains(exception.Message, "192.0.2.1:80");
    }

    [TestMethod]
    public async Task ConnectAsync_UnreachableOrigin_ThrowsUnreachable()
    {
        var connector = new InMemoryOriginConnector().ScriptFailure("localhost", 9, OriginConnectionFailure.Unreachable);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("localhost", 9, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.Unreachable, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_ScriptsOfOneOrigin_AreTakenInOrderThenRefused()
    {
        var origin = new InMemoryConnection([]);
        var connector = new InMemoryOriginConnector()
            .ScriptFailure("localhost", 80, OriginConnectionFailure.TimedOut)
            .ScriptConnection("localhost", 80, origin);

        var first = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, TestContext.CancellationToken).AsTask());
        var second = await connector.ConnectAsync("localhost", 80, Timeout, TestContext.CancellationToken);
        var third = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.TimedOut, first.Failure);
        Assert.AreSame(origin, second);
        Assert.AreEqual(OriginConnectionFailure.Refused, third.Failure);
        Assert.HasCount(3, connector.Requests);
    }

    [TestMethod]
    public async Task ConnectAsync_UnscriptedOrigin_ThrowsRefused()
    {
        var connector = new InMemoryOriginConnector().ScriptConnection("localhost", 80, new InMemoryConnection([]));

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("localhost", 81, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.Refused, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_ThrowsWithoutRecording()
    {
        var connector = new InMemoryOriginConnector();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, new CancellationToken(canceled: true)).AsTask());

        Assert.IsEmpty(connector.Requests);
    }

    [TestMethod]
    public void Script_EmptyHostOrNullConnection_Throws()
    {
        var connector = new InMemoryOriginConnector();

        Assert.ThrowsExactly<ArgumentException>(() => connector.ScriptFailure("", 80, OriginConnectionFailure.Refused));
        Assert.ThrowsExactly<ArgumentNullException>(() => connector.ScriptConnection("localhost", 80, null!));
    }

    [TestMethod]
    public async Task ConnectAsync_EmptyHost_Throws()
    {
        var connector = new InMemoryOriginConnector();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => connector.ConnectAsync("", 80, Timeout, TestContext.CancellationToken).AsTask());
    }
}
