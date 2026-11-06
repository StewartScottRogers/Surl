using Surl.Protocol.Abstractions;

namespace Surl.Api;

public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task FindExchangesAndWaitForExchangeAsync_AServerOnHttpAndFtp_FindTheFtpExchangeByScheme()
    {
        var (seams, connections) = HttpAndFtpSeams();
        var options = HttpAndFtpOptions();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await WaitUntilDisposedAsync(connections);
        var ftp = new SurlEventQuery { Scheme = "ftp" };
        var found = server.FindExchanges(ftp);
        var waited = await server.WaitForExchangeAsync(ftp, TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(server.BoundUrls[1], found.Single().ListenUrl);
        Assert.AreEqual(found.Single().ExchangeId, waited.ExchangeId);
    }

    [TestMethod]
    public async Task FindEventsAndWaitForEventAsync_AServerOnHttpAndFtp_FindTheEndOfTheHttpExchange()
    {
        var (seams, connections) = HttpAndFtpSeams();
        var options = HttpAndFtpOptions();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await WaitUntilDisposedAsync(connections);
        var httpEnded = new SurlEventQuery { Scheme = "http", Kind = SurlEventKind.ExchangeEnded };
        var waited = await server.WaitForEventAsync(httpEnded, TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var found = server.FindEvents(httpEnded);

        Assert.IsInstanceOfType<ExchangeEndedEvent>(waited);
        Assert.AreEqual(waited, found.Single());
        Assert.AreEqual(0, server.DroppedEventCount);
    }

    [TestMethod]
    public async Task Queries_RecordingOff_ThrowAsExchangesDoes()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        var query = new SurlEventQuery();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => server.FindExchanges(query)).Message, "RecordExchanges is off");
        Assert.ThrowsExactly<InvalidOperationException>(() => server.FindEvents(query));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.DroppedEventCount);
        Assert.ThrowsExactly<InvalidOperationException>(() => server.WaitForExchangeAsync(query, TimeSpan.FromSeconds(1), TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.WaitForEventAsync(query, TimeSpan.FromSeconds(1), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Queries_ANullQuery_ThrowArgumentNullException()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.RecordExchanges = true;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.FindExchanges(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.FindEvents(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.WaitForExchangeAsync(null!, TimeSpan.FromSeconds(1), TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.WaitForEventAsync(null!, TimeSpan.FromSeconds(1), TestContext.CancellationToken));
    }
}
