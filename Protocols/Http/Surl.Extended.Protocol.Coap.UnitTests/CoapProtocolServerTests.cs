using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server through the datagram seam (ADR-0004; ADR-0097 decisions 0, 1, 2, 3 and 6), from
/// datagrams written by hand from RFC 7252 sections 3, 4, 5 and 7 and RFC 6690.
/// </summary>
[TestClass]
public sealed class CoapProtocolServerTests
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    [TestMethod]
    public void Schemes_IsCoap() =>
        CollectionAssert.AreEqual(new[] { "coap" }, new CoapProtocolServer(new CoapTestStore().Store, Anonymous).Schemes.ToArray());

    [TestMethod]
    public async Task Constructor_And_ServeAsync_NullArguments_Throw()
    {
        var store = new CoapTestStore().Store;
        var server = new CoapProtocolServer(store, Anonymous);
        await using var flow = new InMemoryDatagramFlow(new byte[] { 0x40, 0x00, 0x00, 0x01 });

        Assert.ThrowsExactly<ArgumentNullException>(() => new CoapProtocolServer(null!, Anonymous));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CoapProtocolServer(store, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog(), new ManualTimeProvider(), default)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(new CoapTestStore(), log, 1, [0x40, 0x01, 0xBE, 0xEF, 0xFF]);

        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0xBE, 0xEF }, sent.Single());
        Assert.AreEqual("Rejected with RST: a CON with Message ID 48879 and a message format error: A payload marker with no payload.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_MalformedNon_IsIgnored()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(new CoapTestStore(), log, 0, [0x50, 0x01, 0xBE, 0xEF, 0xF0]);

        Assert.IsEmpty(sent);
        Assert.AreEqual("Ignored: a NonConfirmable with Message ID 48879 and a message format error: Option delta 15 is reserved outside the payload marker.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyCon_IsAnsweredWithRst()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(new CoapTestStore(), log, 1, [0x40, 0x00, 0x12, 0x34]);

        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0x12, 0x34 }, sent.Single());
        Assert.AreEqual("Rejected with RST: a CON with Message ID 4660 and code 0.00, which is no request.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow((byte)0x45)]
    [DataRow((byte)0x20)]
    [DataRow((byte)0xE0)]
    public async Task ServeAsync_ConWithAResponseOrReservedCode_IsAnsweredWithRst(byte code) =>
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0x12, 0x34 }, (await ServeAsync(new CoapTestStore(), null, 1, [0x41, code, 0x12, 0x34, 0x99])).Single());

    [TestMethod]
    public async Task ServeAsync_NonWithAResponseCode_IsIgnored() =>
        Assert.IsEmpty(await ServeAsync(new CoapTestStore(), null, 0, [0x51, 0x45, 0x12, 0x34, 0x99]));

    [TestMethod]
    [DataRow(new byte[] { 0x60, 0x00, 0x12, 0x34 }, "Ignored: an unmatched Acknowledgement with Message ID 4660.")]
    [DataRow(new byte[] { 0x70, 0x00, 0x12, 0x34 }, "Ignored: an unmatched Reset with Message ID 4660.")]
    [DataRow(new byte[] { 0x40, 0x01, 0x12 }, "Ignored: A datagram shorter than the 4-byte CoAP header.")]
    [DataRow(new byte[] { 0x80, 0x01, 0x12, 0x34 }, "Ignored: A datagram of CoAP version 2, not 1.")]
    public async Task ServeAsync_AckRstAndNonCoapDatagrams_AreIgnored(byte[] datagram, string note)
    {
        var log = new RecordingExchangeLog();

        Assert.IsEmpty(await ServeAsync(new CoapTestStore(), log, 0, datagram));
        Assert.AreEqual(note, log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag()
    {
        var store = new CoapTestStore().WithFile("hello.txt", "hi");

        var sent = await ServeAsync(store, null, 1, [0x42, 0x01, 0x7D, 0x34, 0xA1, 0xA2, 0xB9, .. "hello.txt"u8]);

        byte[] expected = [0x62, 0x45, 0x7D, 0x34, 0xA1, 0xA2, 0x48, .. CoapTestStore.EntityTagOf(2), 0x80, 0x21, 0x3C, 0xFF, .. "hi"u8];
        CollectionAssert.AreEqual(expected, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_DuplicateCon_IsAnsweredWithTheSameResponseAndNotServedTwice()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        byte[] post = [0x40, 0x02, 0x00, 0x07, 0xB7, .. "log.txt"u8, 0xFF, .. "x"u8];

        var sent = await ServeAsync(store, null, 2, post, post);

        Assert.HasCount(2, sent);
        CollectionAssert.AreEqual(new byte[] { 0x60, 0x41, 0x00, 0x07 }, sent[0]);
        CollectionAssert.AreEqual(sent[0], sent[1]);
        Assert.AreEqual("x", store.Contents("log.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_NonRequests_AreAnsweredWithNonsNumberedFromTheDrawnMessageId()
    {
        var sent = await ServeAsync(new CoapTestStore(), null, 2, [0x51, 0x01, 0x7D, 0x40, 0x75], [0x51, 0x01, 0x7D, 0x41, 0x76]);

        Assert.HasCount(2, sent);
        CollectionAssert.AreEqual(new byte[] { 0x51, 0x84, 0xFF, 0xFF, 0x75 }, sent[0]);
        CollectionAssert.AreEqual(new byte[] { 0x51, 0x84, 0x00, 0x00, 0x76 }, sent[1]);
    }

    [TestMethod]
    public async Task ServeAsync_NonGet_IsAnsweredWithANon205()
    {
        var store = new CoapTestStore().WithFile("a.bin", "z");

        var sent = await ServeAsync(store, null, 1, [0x50, 0x01, 0x00, 0x09, 0xB5, .. "a.bin"u8]);

        byte[] expected = [0x50, 0x45, 0xFF, 0xFF, 0x48, .. CoapTestStore.EntityTagOf(1), 0x81, 0x2A, 0x21, 0x3C, 0xFF, .. "z"u8];
        CollectionAssert.AreEqual(expected, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_PutUnderUploads_Gets201ThenChanged204()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        byte[] put = [0x40, 0x03, 0x00, 0x01, 0xB5, .. "n.txt"u8, 0xFF, .. "one"u8];
        byte[] putAgain = [0x40, 0x03, 0x00, 0x02, 0xB5, .. "n.txt"u8, 0xFF, .. "two"u8];

        var sent = await ServeAsync(store, null, 2, put, putAgain);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x41, 0x00, 0x01 }, sent[0]);
        CollectionAssert.AreEqual(new byte[] { 0x60, 0x44, 0x00, 0x02 }, sent[1]);
        Assert.AreEqual("two", store.Contents("n.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithoutUploads_IsRefusedWith405()
    {
        var store = new CoapTestStore();

        var sent = await ServeAsync(store, null, 1, [0x40, 0x03, 0x00, 0x01, 0xB5, .. "n.txt"u8, 0xFF, .. "one"u8]);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x85, 0x00, 0x01 }, sent.Single());
        Assert.IsFalse(store.HasFile("n.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_Delete_Gets202()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("old.txt", "x");

        var sent = await ServeAsync(store, null, 1, [0x40, 0x04, 0x00, 0x03, 0xB7, .. "old.txt"u8]);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x42, 0x00, 0x03 }, sent.Single());
        Assert.IsFalse(store.HasFile("old.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_UnknownCriticalOption_Gets402NamingIt()
    {
        var sent = await ServeAsync(new CoapTestStore(), null, 1, [0x40, 0x01, 0x00, 0x04, 0xD0, 0x00]);

        CollectionAssert.AreEqual((byte[])[0x60, 0x82, 0x00, 0x04, 0xFF, .. "Unrecognized option 13"u8], sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NonWithAnUnknownCriticalOption_IsIgnored()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(new CoapTestStore(), log, 0, [0x50, 0x01, 0x00, 0x04, 0xD0, 0x00]);

        Assert.IsEmpty(sent);
        Assert.AreEqual("Ignored: a NonConfirmable with Message ID 4 and unrecognised critical option 13.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_MissingResource_Gets404WithItsToken()
    {
        var sent = await ServeAsync(new CoapTestStore(), null, 1, [0x42, 0x01, 0x7D, 0x34, 0xA1, 0xA2, 0xBB, .. "temperature"u8]);

        CollectionAssert.AreEqual(new byte[] { 0x62, 0x84, 0x7D, 0x34, 0xA1, 0xA2 }, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_UnsupportedMethod_Gets405()
    {
        var sent = await ServeAsync(new CoapTestStore(), null, 1, [0x40, 0x05, 0x00, 0x05]);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x85, 0x00, 0x05 }, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_PutWithIfNoneMatchOnAnExistingResource_Gets412AndKeepsIt()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("n.txt", "kept");

        var sent = await ServeAsync(store, null, 1, [0x40, 0x03, 0x00, 0x06, 0x50, 0x65, .. "n.txt"u8, 0xFF, .. "new"u8]);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x8C, 0x00, 0x06 }, sent.Single());
        Assert.AreEqual("kept", store.Contents("n.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_WellKnownCore_ListsTheResourcesInLinkFormat()
    {
        var store = new CoapTestStore(CoapTestStore.Listings).WithFile("b.json", "{}").WithDirectory("s").WithFile("s/a b.txt", "abc");
        var payload = "</b.json>;ct=50;sz=2;obs,</s/a%20b.txt>;ct=0;sz=3;obs"u8.ToArray();

        var sent = await ServeAsync(store, null, 1, [0x40, 0x01, 0x00, 0x08, 0xBB, .. ".well-known"u8, 0x04, .. "core"u8]);

        byte[] expected = [0x60, 0x45, 0x00, 0x08, 0x48, .. System.Security.Cryptography.SHA256.HashData(payload)[..8], 0x81, 0x28, 0x21, 0x3C, 0xFF, .. payload];
        CollectionAssert.AreEqual(expected, sent.Single());
    }

    [TestMethod]
    public async Task ServeAsync_WithTheDefaultRandomSource_AnswersANonRequestWithANonOfItsToken()
    {
        await using var flow = new InMemoryDatagramFlow(new byte[] { 0x51, 0x01, 0x7D, 0x40, 0x75 });
        using var exchange = new CancellationTokenSource();

        var serving = new CoapProtocolServer(new CoapTestStore().Store, Anonymous).ServeAsync(flow, Context(new RecordingExchangeLog(), new ManualTimeProvider(), exchange.Token));
        await WaitForSendsAsync(flow, 1);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        var answer = flow.SentDatagrams.Single();
        Assert.AreEqual((byte)0x51, answer[0]);
        Assert.AreEqual((byte)0x84, answer[1]);
        Assert.AreEqual((byte)0x75, answer[4]);
    }

    internal static async Task WaitForSendsAsync(InMemoryDatagramFlow flow, int count)
    {
        var started = DateTime.UtcNow;

        while (flow.SentDatagrams.Count < count)
        {
            Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, $"Waited for {count} datagrams; {flow.SentDatagrams.Count} were sent.");
            await Task.Delay(5);
        }
    }

    internal static ExchangeContext Context(IExchangeLog log, TimeProvider clock, CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("coap", "127.0.0.1", 5683).WithBoundPort(5683),
        new IPEndPoint(IPAddress.Loopback, 5683),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellationToken);

    private static async Task<IReadOnlyList<byte[]>> ServeAsync(CoapTestStore store, RecordingExchangeLog? log, int expectedSends, byte[] firstDatagram, params byte[][] later)
    {
        var server = new CoapProtocolServer(store.Store, Anonymous, new UnitTestCoapRandomSource());
        await using var flow = new InMemoryDatagramFlow(firstDatagram, later.Select(datagram => (ReadOnlyMemory<byte>)datagram));
        using var exchange = new CancellationTokenSource();
        log ??= new RecordingExchangeLog();

        var serving = server.ServeAsync(flow, Context(log, new ManualTimeProvider(), exchange.Token));
        await WaitForSendsAsync(flow, expectedSends);
        await WaitForNotesAsync(log, later.Length + 1, expectedSends);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), flow.LocalEndPoint);
        return flow.SentDatagrams;
    }

    // An ignored datagram sends nothing but notes why, so a test that expects fewer sends than
    // datagrams waits for the notes too before it cancels.
    private static async Task WaitForNotesAsync(RecordingExchangeLog log, int datagrams, int sends)
    {
        var started = DateTime.UtcNow;

        while (sends < datagrams && log.Notes.Count < datagrams - sends)
        {
            Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, "Waited for the server to note the datagrams it ignored.");
            await Task.Delay(5);
        }
    }
}
