using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over UDP, through the datagram seam (ADR-0004; ADR-0092 decision 3), from query
/// bytes written by hand from RFC 1035 section 4.1 and RFC 6891 section 6.1.2.
/// </summary>
[TestClass]
public sealed class DnsUdpProtocolServerTests
{
    private const ushort TypeA = 1;
    private const ushort TypeTxt = 16;

    [TestMethod]
    public void Constructor_NullZones_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsUdpProtocolServer(null!));

    [TestMethod]
    public void Schemes_IsDns() =>
        CollectionAssert.AreEqual(new[] { "dns" }, new DnsUdpProtocolServer(new DnsZoneSet()).Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new DnsUdpProtocolServer(new DnsZoneSet());
        await using var flow = new InMemoryDatagramFlow(Query("a.test", TypeA));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog(), default)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_Query_AnswersWithItsIdAndQuestionEchoed()
    {
        var sent = await ServeAsync(Query("web.example.test", TypeA, id: 0x0A0B));

        var response = sent.Single();
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x0B, 0x85, 0x00, 0x00, 0x01 }, response[..6]);
        CollectionAssert.AreEqual(Query("web.example.test", TypeA)[12..34], response[12..34]);
        var message = ReadWhole(response);
        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)message.Answers.Single().Data).Address);
    }

    [TestMethod]
    public async Task ServeAsync_AnswerOver512Bytes_SetsTcAndSendsOnlyTheRecordSetsThatFit()
    {
        var response = (await ServeAsync(Query("big.example.test", TypeTxt))).Single();

        var message = ReadWhole(response);
        Assert.IsLessThanOrEqualTo(512, response.Length);
        Assert.IsTrue(message.Header.IsTruncated);
        Assert.AreEqual(DnsRecordType.CNAME, message.Answers.Single().Type);
        Assert.IsEmpty(message.Authorities);
        Assert.IsEmpty(message.Additionals);
    }

    [TestMethod]
    public async Task ServeAsync_SameAnswerUnderAnOptPayloadSizeOf4096_SendsItWholeWithAnOpt()
    {
        var response = (await ServeAsync(Query("big.example.test", TypeTxt, additionals: Opt(4096)))).Single();

        var message = ReadWhole(response);
        Assert.IsGreaterThan(512, response.Length);
        Assert.IsFalse(message.Header.IsTruncated);
        Assert.HasCount(13, message.Answers);
        Assert.AreEqual(DnsRecordType.NS, message.Authorities.Single().Type);
        var opt = DnsOptRecord.FromResourceRecord(message.Additionals[^1]);
        Assert.AreEqual(DnsQueryAnswerer.ServerPayloadSize, opt.PayloadSize);
        Assert.AreEqual((byte)0, opt.Version);
    }

    [TestMethod]
    public async Task ServeAsync_ResponseMessage_IsIgnoredAndNoted()
    {
        var log = new RecordingExchangeLog();

        var sent = await ServeAsync(Query("web.example.test", TypeA, flags: 0x8500), log);

        Assert.IsEmpty(sent);
        Assert.AreEqual("A datagram with no readable query header, or a response, was dropped unanswered.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_TwoQuestions_AnswersFormatError()
    {
        var response = (await ServeAsync(Query("web.example.test", TypeA, questionCount: 2))).Single();

        CollectionAssert.AreEqual(new byte[] { 0xBE, 0xEF, 0x81, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 }, response);
    }

    [TestMethod]
    public async Task ServeAsync_StatusOpcode_AnswersNotImplementedWithTheQuestion()
    {
        var query = Query("web.example.test", TypeA, flags: 0x1100);

        var response = (await ServeAsync(query)).Single();

        byte[] expected = [0xBE, 0xEF, 0x91, 0x04, 0, 1, 0, 0, 0, 0, 0, 0, .. query[12..]];
        CollectionAssert.AreEqual(expected, response);
    }

    [TestMethod]
    public async Task ServeAsync_UnreadableMessageWithAHeader_AnswersFormatError()
    {
        var response = (await ServeAsync(Query("web.example.test", TypeA)[..14])).Single();

        CollectionAssert.AreEqual(new byte[] { 0xBE, 0xEF, 0x81, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 }, response);
    }

    [TestMethod]
    public async Task ServeAsync_UnreadableMessageWithoutAHeader_IsDropped() =>
        Assert.IsEmpty(await ServeAsync([0xBE, 0xEF, 0x01, 0x00, 0x00]));

    [TestMethod]
    public async Task ServeAsync_LaterQueriesOnTheFlow_AreEachAnswered()
    {
        var sent = await ServeAsync(Query("web.example.test", TypeA, id: 1), null, Query("mail.example.test", TypeA, id: 2), Query("nowhere.invalid", TypeA, id: 3));

        CollectionAssert.AreEqual(new ushort[] { 1, 2, 3 }, sent.Select(datagram => ReadWhole(datagram).Header.Id).ToArray());
        Assert.AreEqual(DnsResponseCode.Refused, ReadWhole(sent[2]).Header.ResponseCode);
    }

    // Serves firstDatagram and later, then cancels the exchange once the flow waits for more,
    // and returns what was sent.
    private static async Task<IReadOnlyList<byte[]>> ServeAsync(byte[] firstDatagram, RecordingExchangeLog? log = null, params byte[][] later)
    {
        var server = new DnsUdpProtocolServer(LoadZonesWithALargeAnswer());
        await using var flow = new InMemoryDatagramFlow(firstDatagram, later.Select(datagram => (ReadOnlyMemory<byte>)datagram));
        using var exchange = new CancellationTokenSource();

        var serving = server.ServeAsync(flow, Context(log ?? new RecordingExchangeLog(), exchange.Token));
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        return flow.SentDatagrams;
    }

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("dns", "127.0.0.1", 53).WithBoundPort(53),
        new IPEndPoint(IPAddress.Loopback, 53),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken);
}
