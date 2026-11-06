namespace Surl.Extended.Protocol.WebTransport;

/// <summary>Requests that are not a WebTransport CONNECT (ADR-0117 decision 1), over the in-memory QUIC test connection.</summary>
[TestClass]
public sealed class WebTransportRequestStreamHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeRequestStreamAsync_Get_IsAnswered405WithAllowConnectAndFin()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        client.Send(0, client.Request("GET", "/echo"), fin: true);
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        Assert.AreEqual("405", client.Response(0).Status);
        CollectionAssert.AreEqual(new[] { "allow: CONNECT" }, client.Response(0).RegularFields);
        Assert.IsEmpty(client.Response(0).Body);
    }
}
