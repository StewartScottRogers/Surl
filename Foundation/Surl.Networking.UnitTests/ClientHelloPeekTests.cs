namespace Surl.Networking;

/// <summary>Reading the first record and deciding whether only TLS 1.3 can answer it (ADR-0157, decision 1).</summary>
[TestClass]
public sealed class ClientHelloPeekTests
{
    private const ushort Tls13 = 0x0304;
    private const ushort Tls12 = 0x0303;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_Tls13Only_OffersOnlyTls13AndKeepsEveryByte()
    {
        var record = ClientHelloBytes.Record(22, ClientHelloBytes.Message([Tls13]));

        var peek = await ReadAsync([.. record, 99]);

        Assert.IsTrue(peek.OffersOnlyTls13);
        Assert.AreEqual(Tls13, peek.ClientHello!.SupportedVersions![0]);
        CollectionAssert.AreEqual(record, peek.BytesRead);
    }

    [TestMethod]
    public async Task ReadAsync_GreaseBesideTls13_StillOffersOnlyTls13() =>
        Assert.IsTrue((await ReadAsync(ClientHelloBytes.Record(22, ClientHelloBytes.Message([0x0a0a, Tls13])))).OffersOnlyTls13);

    [TestMethod]
    public async Task ReadAsync_Tls12AndTls13_DoesNotOfferOnlyTls13() =>
        Assert.IsFalse((await ReadAsync(ClientHelloBytes.Record(22, ClientHelloBytes.Message([Tls13, Tls12])))).OffersOnlyTls13);

    [TestMethod]
    public async Task ReadAsync_Tls12Only_DoesNotOfferOnlyTls13() =>
        Assert.IsFalse((await ReadAsync(ClientHelloBytes.Record(22, ClientHelloBytes.Message([Tls12])))).OffersOnlyTls13);

    [TestMethod]
    public async Task ReadAsync_NoSupportedVersions_DoesNotOfferOnlyTls13() =>
        Assert.IsFalse((await ReadAsync(ClientHelloBytes.Record(22, ClientHelloBytes.Message(null)))).OffersOnlyTls13);

    [TestMethod]
    public async Task ReadAsync_NotAHandshakeRecord_KeepsTheHeaderAndParsesNothing()
    {
        var peek = await ReadAsync([23, 3, 3, 0, 4, 1, 2, 3, 4]);

        Assert.IsNull(peek.ClientHello);
        CollectionAssert.AreEqual(new byte[] { 23, 3, 3, 0, 4 }, peek.BytesRead);
    }

    [TestMethod]
    public async Task ReadAsync_OversizedRecord_KeepsTheHeaderAndParsesNothing()
    {
        var peek = await ReadAsync([22, 3, 3, 0x7f, 0xff, 1]);

        Assert.IsNull(peek.ClientHello);
        Assert.HasCount(5, peek.BytesRead);
    }

    [TestMethod]
    public async Task ReadAsync_ShortHeader_KeepsWhatCame()
    {
        var peek = await ReadAsync([22, 3]);

        Assert.IsNull(peek.ClientHello);
        CollectionAssert.AreEqual(new byte[] { 22, 3 }, peek.BytesRead);
        Assert.IsFalse(peek.OffersOnlyTls13);
    }

    [TestMethod]
    public async Task ReadAsync_TruncatedFragment_KeepsWhatCame()
    {
        var record = ClientHelloBytes.Record(22, ClientHelloBytes.Message([Tls13]));

        var peek = await ReadAsync(record[..^3]);

        Assert.IsNull(peek.ClientHello);
        CollectionAssert.AreEqual(record[..^3], peek.BytesRead);
    }

    [TestMethod]
    public async Task ReadAsync_FragmentThatIsNotAClientHello_ParsesNothing()
    {
        var peek = await ReadAsync(ClientHelloBytes.Record(22, [2, 0, 0, 0]));

        Assert.IsNull(peek.ClientHello);
        Assert.HasCount(9, peek.BytesRead);
    }

    private Task<ClientHelloPeek> ReadAsync(byte[] bytes) =>
        ClientHelloPeek.ReadAsync(new MemoryStream(bytes), TestContext.CancellationToken);
}
