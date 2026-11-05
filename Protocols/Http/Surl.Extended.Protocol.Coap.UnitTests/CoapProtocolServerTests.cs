using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server's message layer through the datagram seam (ADR-0004; ADR-0097 decisions 0 and
/// 2), from datagrams written by hand from RFC 7252 sections 3, 4.2 and 4.3.
/// </summary>
[TestClass]
public sealed class CoapProtocolServerTests
{
    [TestMethod]
    public void Schemes_IsCoap() =>
        CollectionAssert.AreEqual(new[] { "coap" }, new CoapProtocolServer().Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new CoapProtocolServer();
        await using var flow = new InMemoryDatagramFlow(new byte[] { 0x40, 0x00, 0x00, 0x01 });

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog(), default)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(log, [0x40, 0x01, 0xBE, 0xEF, 0xFF]);

        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0xBE, 0xEF }, sent.Single());
        Assert.AreEqual("Rejected with RST: a CON with Message ID 48879 and a message format error: A payload marker with no payload.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_MalformedNon_IsIgnored()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(log, [0x50, 0x01, 0xBE, 0xEF, 0xF0]);

        Assert.IsEmpty(sent);
        Assert.AreEqual("Ignored: a NonConfirmable with Message ID 48879 and a message format error: Option delta 15 is reserved outside the payload marker.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyCon_IsAnsweredWithRst()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(log, [0x40, 0x00, 0x12, 0x34]);

        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0x12, 0x34 }, sent.Single());
        Assert.AreEqual("Rejected with RST: a CON with Message ID 4660 and code 0.00, which is no request.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow((byte)0x45)]
    [DataRow((byte)0x20)]
    [DataRow((byte)0xE0)]
    public async Task ServeAsync_ConWithAResponseOrReservedCode_IsAnsweredWithRst(byte code) =>
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0x12, 0x34 }, (await ServeAsync(null, [0x41, code, 0x12, 0x34, 0x99])).Single());

    [TestMethod]
    public async Task ServeAsync_NonWithAResponseCode_IsIgnored() =>
        Assert.IsEmpty(await ServeAsync(null, [0x51, 0x45, 0x12, 0x34, 0x99]));

    [TestMethod]
    [DataRow(new byte[] { 0x60, 0x00, 0x12, 0x34 }, "Ignored: an unmatched Acknowledgement with Message ID 4660.")]
    [DataRow(new byte[] { 0x70, 0x00, 0x12, 0x34 }, "Ignored: an unmatched Reset with Message ID 4660.")]
    [DataRow(new byte[] { 0x40, 0x01, 0x12 }, "Ignored: A datagram shorter than the 4-byte CoAP header.")]
    [DataRow(new byte[] { 0x80, 0x01, 0x12, 0x34 }, "Ignored: A datagram of CoAP version 2, not 1.")]
    public async Task ServeAsync_AckRstAndNonCoapDatagrams_AreIgnored(byte[] datagram, string note)
    {
        var log = new RecordingExchangeLog();

        Assert.IsEmpty(await ServeAsync(log, datagram));
        Assert.AreEqual(note, log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ConRequest_IsAnsweredWithAPiggybacked404CarryingItsMessageIdAndToken()
    {
        var sent = await ServeAsync(null, [0x42, 0x01, 0x7D, 0x34, 0xA1, 0xA2, 0xBB, .. "temperature"u8]);

        CollectionAssert.AreEqual(new byte[] { 0x62, 0x84, 0x7D, 0x34, 0xA1, 0xA2 }, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NonRequests_AreAnsweredWithNon404sNumberedFromTheDrawnMessageId()
    {
        var sent = await ServeAsync(null, [0x51, 0x01, 0x7D, 0x40, 0x75], [0x51, 0x01, 0x7D, 0x41, 0x76]);

        Assert.HasCount(2, sent);
        CollectionAssert.AreEqual(new byte[] { 0x51, 0x84, 0xFF, 0xFF, 0x75 }, sent[0]);
        CollectionAssert.AreEqual(new byte[] { 0x51, 0x84, 0x00, 0x00, 0x76 }, sent[1]);
    }

    [TestMethod]
    public async Task ServeAsync_WithTheDefaultMessageIdSource_AnswersANonRequestWithANonOfItsToken()
    {
        await using var flow = new InMemoryDatagramFlow(new byte[] { 0x51, 0x01, 0x7D, 0x40, 0x75 });
        using var exchange = new CancellationTokenSource();

        var serving = new CoapProtocolServer().ServeAsync(flow, Context(new RecordingExchangeLog(), exchange.Token));
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        var answer = flow.SentDatagrams.Single();
        Assert.AreEqual((byte)0x51, answer[0]);
        Assert.AreEqual((byte)0x84, answer[1]);
        Assert.AreEqual((byte)0x75, answer[4]);
    }

    private static async Task<IReadOnlyList<byte[]>> ServeAsync(RecordingExchangeLog? log, byte[] firstDatagram, params byte[][] later)
    {
        var server = new CoapProtocolServer(() => 0xFFFF);
        await using var flow = new InMemoryDatagramFlow(firstDatagram, later.Select(datagram => (ReadOnlyMemory<byte>)datagram));
        using var exchange = new CancellationTokenSource();

        var serving = server.ServeAsync(flow, Context(log ?? new RecordingExchangeLog(), exchange.Token));
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), flow.LocalEndPoint);
        return flow.SentDatagrams;
    }

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("coap", "127.0.0.1", 5683).WithBoundPort(5683),
        new IPEndPoint(IPAddress.Loopback, 5683),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken);
}
