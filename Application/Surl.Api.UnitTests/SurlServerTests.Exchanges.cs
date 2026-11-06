using System.Text;

namespace Surl.Api;

public sealed partial class SurlServerTests
{
    // The request head upstream curl 8.21.0 sends for a plain GET (Host, User-Agent, Accept, as
    // HttpConnectionReaderTests pins from the recorded exchange).
    private static readonly byte[] CurlGetHello = Encoding.ASCII.GetBytes(
        $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{FakeListenerFactory.BoundPort}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n");

    [TestMethod]
    public async Task Exchanges_RecordingOnAndAFakeClientsGet_HoldsTheExactBytesTheClientWroteAndWasSent()
    {
        var connection = new FakeConnection(CurlGetHello);
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.RecordExchanges = true;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var record = server.Exchanges.Single();

        CollectionAssert.AreEqual(CurlGetHello, record.Received);
        CollectionAssert.AreEqual(connection.WrittenToClient, record.Sent);
        StringAssert.StartsWith(Encoding.ASCII.GetString(record.Sent), "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(FakeConnection.Client, record.Client);
        Assert.IsFalse(record.IsTruncated);
        Assert.AreEqual(0, server.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Exchanges_RecordingOff_ThrowsSayingRecordExchangesIsOff()
    {
        var connection = new FakeConnection(CurlGetHello);
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Files["hello.txt"] = "hello"u8.ToArray();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => server.Exchanges);
        StringAssert.Contains(exception.Message, "RecordExchanges is off");
        Assert.ThrowsExactly<InvalidOperationException>(() => server.DroppedExchangeCount);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    [DataRow(-1, 1)]
    public async Task StartAsync_NonPositiveRecordBound_ThrowsArgumentException(int maxExchanges, int maxBytesPerExchange)
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.MaxRecordedExchanges = maxExchanges;
        options.MaxRecordedBytesPerExchange = maxBytesPerExchange;

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public void Options_RecordBoundsByDefault_AreTheAdrsAndRecordingIsOffAndAddsNoWord()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.RecordExchanges = true;

        var arguments = SurlServerArguments.Compose(options);

        Assert.AreEqual(1000, options.MaxRecordedExchanges);
        Assert.AreEqual(1_048_576, options.MaxRecordedBytesPerExchange);
        Assert.IsFalse(new SurlServerOptions().RecordExchanges);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:0" }, arguments.ToArray());
    }
}
