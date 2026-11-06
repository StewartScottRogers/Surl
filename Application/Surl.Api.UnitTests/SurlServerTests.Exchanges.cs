using System.Text;
using Surl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task Exchanges_AServerOnAnHttpAndAnFtpListenUrl_RecordsEachExchangeWithItsOwnBoundListenUrlAndScheme()
    {
        var (seams, connections) = HttpAndFtpSeams();
        var options = HttpAndFtpOptions();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await WaitUntilDisposedAsync(connections);
        var records = server.Exchanges;

        Assert.HasCount(2, records);
        var http = records.Single(record => record.Scheme == "http");
        var ftp = records.Single(record => record.Scheme == "ftp");
        Assert.AreEqual(server.BoundUrls[0], http.ListenUrl);
        Assert.AreEqual(new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/"), http.ListenUrl);
        CollectionAssert.AreEqual(CurlGetHello, http.Received);
        Assert.AreEqual(server.BoundUrls[1], ftp.ListenUrl);
        Assert.AreEqual(new Uri($"ftp://127.0.0.1:{FakeListenerFactory.BoundPort}/"), ftp.ListenUrl);
        CollectionAssert.AreEqual(FtpQuit, ftp.Received);
        StringAssert.StartsWith(Encoding.ASCII.GetString(ftp.Sent), "220");
        Assert.AreEqual(0, server.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Exchanges_OneExchangeKeptAndTwoServed_KeepsOneAndCountsTheDroppedOne()
    {
        var (seams, connections) = HttpAndFtpSeams();
        var options = HttpAndFtpOptions();
        options.MaxRecordedExchanges = 1;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await WaitUntilDisposedAsync(connections);
        var records = server.Exchanges;

        Assert.HasCount(1, records);
        Assert.AreEqual(1, server.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Exchanges_BytesPastTheBytesPerExchangeCap_KeepsTheFirstBytesBothDirectionsTogetherAndMarksEachTruncated()
    {
        var (seams, connections) = HttpAndFtpSeams();
        var options = HttpAndFtpOptions();
        options.MaxRecordedBytesPerExchange = 4;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await WaitUntilDisposedAsync(connections);
        var records = server.Exchanges;

        Assert.HasCount(2, records);
        Assert.IsTrue(records.All(record => record.IsTruncated));
        Assert.IsTrue(records.All(record => record.Chunks.Sum(chunk => chunk.Bytes.Length) == 4));
        CollectionAssert.AreEqual(CurlGetHello[..4], records.Single(record => record.Scheme == "http").Received);
        CollectionAssert.AreEqual("220 "u8.ToArray(), records.Single(record => record.Scheme == "ftp").Sent);
    }

    [TestMethod]
    public async Task Exchanges_AfterTheServerStopped_StillHoldsEveryExchange()
    {
        var connection = new FakeConnection(CurlGetHello);
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.RecordExchanges = true;

        var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await server.StopAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(CurlGetHello, server.Exchanges.Single().Received);
    }

    [TestMethod]
    public async Task StartAsync_RecordingOnAndTheBindFails_ThrowsTheBindFailure()
    {
        var seams = new ApiTestSeams(new FakeListenerFactory { BindFails = true });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.RecordExchanges = true;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.BindFailed, exception.ExitCode);
    }

    // What an FTP client sends to end the session at once (RFC 959, section 4.1.1).
    private static readonly byte[] FtpQuit = "QUIT\r\n"u8.ToArray();

    private static (ApiTestSeams Seams, FakeConnection[] Connections) HttpAndFtpSeams()
    {
        var http = new FakeConnection(CurlGetHello);
        var ftp = new FakeConnection(FtpQuit);
        var listenerFactory = new FakeListenerFactory
        {
            ConnectionsToAcceptByScheme = new Dictionary<string, IConnection>
            {
                ["http"] = http,
                ["ftp"] = ftp,
            },
        };
        return (new ApiTestSeams(listenerFactory), [http, ftp]);
    }

    private static SurlServerOptions HttpAndFtpOptions()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0", "ftp://127.0.0.1:0");
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.RecordExchanges = true;
        return options;
    }

    private async Task WaitUntilDisposedAsync(FakeConnection[] connections)
    {
        foreach (var connection in connections)
        {
            await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        }
    }
}
