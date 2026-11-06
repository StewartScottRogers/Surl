using System.Buffers.Binary;
using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over TCP, through the connection seam (ADR-0004; ADR-0092 decision 4), from
/// query bytes written by hand from RFC 1035 sections 4.1 and 4.2.2 and RFC 7766.
/// </summary>
[TestClass]
public sealed class DnsTcpProtocolServerTests
{
    private const ushort TypeA = 1;
    private const ushort TypeTxt = 16;

    [TestMethod]
    public void Constructor_NullZones_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsTcpProtocolServer(null!));

    [TestMethod]
    public void Schemes_IsDns() =>
        CollectionAssert.AreEqual(new[] { "dns" }, new DnsTcpProtocolServer(new DnsZoneSet()).Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new DnsTcpProtocolServer(new DnsZoneSet());
        await using var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(connection, null!));
    }

    [TestMethod]
    public async Task ServeAsync_OneQuery_AnswersWithItsLengthPrefix()
    {
        var connection = await ServeAsync([Frame(Query("web.example.test", TypeA, id: 0x0A0B))]);

        var written = connection.WrittenBytes;
        Assert.AreEqual(written.Length - 2, BinaryPrimitives.ReadUInt16BigEndian(written));
        var message = ReadWhole(written[2..]);
        Assert.AreEqual((ushort)0x0A0B, message.Header.Id);
        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)message.Answers.Single().Data).Address);
    }

    [TestMethod]
    public async Task ServeAsync_ThreePipelinedQueriesInOneRead_AnswersEachInOrderOnOneConnection()
    {
        byte[] pipelined =
        [
            .. Frame(Query("web.example.test", TypeA, id: 1)),
            .. Frame(Query("mail.example.test", TypeA, id: 2)),
            .. Frame(Query("nowhere.invalid", TypeA, id: 3)),
        ];

        var frames = Frames((await ServeAsync([pipelined])).WrittenBytes);

        CollectionAssert.AreEqual(new ushort[] { 1, 2, 3 }, frames.Select(frame => ReadWhole(frame).Header.Id).ToArray());
        Assert.AreEqual(DnsResponseCode.Refused, ReadWhole(frames[2]).Header.ResponseCode);
    }

    [TestMethod]
    public async Task ServeAsync_QuerySplitAcrossReads_IsAnsweredOnceWhole()
    {
        var frame = Frame(Query("web.example.test", TypeA));

        var frames = Frames((await ServeAsync([frame[..1], frame[1..5], frame[5..]])).WrittenBytes);

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(frames.Single()).Header.ResponseCode);
    }

    [TestMethod]
    public async Task ServeAsync_AnswerOver512Bytes_IsSentWholeWithoutTc()
    {
        var frames = Frames((await ServeAsync([Frame(Query("big.example.test", TypeTxt))])).WrittenBytes);

        Assert.IsGreaterThan(512, frames.Single().Length);
        var message = ReadWhole(frames.Single());
        Assert.IsFalse(message.Header.IsTruncated);
        Assert.HasCount(13, message.Answers);
        Assert.AreEqual(DnsRecordType.NS, message.Authorities.Single().Type);
    }

    [TestMethod]
    public async Task ServeAsync_LengthPrefixOfZero_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync([[0, 0, .. Frame(Query("web.example.test", TypeA))]], log);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("DNS connection closed: a message length of 0 is 0 or past --max-message 1048576", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_LengthPrefixOverMaxMessage_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var query = Query("web.example.test", TypeA);

        var connection = await ServeAsync([Frame(query)], log, limits: ExchangeLimits.Default with { MaxMessageBytes = query.Length - 1 });

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual($"DNS connection closed: a message length of {query.Length} is 0 or past --max-message {query.Length - 1}", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NoMaxMessage_AnswersAQueryOfAnyLength()
    {
        var connection = await ServeAsync([Frame(Query("web.example.test", TypeA))], limits: ExchangeLimits.Default with { MaxMessageBytes = 0 });

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(Frames(connection.WrittenBytes).Single()).Header.ResponseCode);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xBE, 0xEF, 0x01, 0x00, 0x00 }, DisplayName = "no header")]
    [DataRow(new byte[] { 0xBE, 0xEF, 0x85, 0x00, 0, 0, 0, 0, 0, 0, 0, 0 }, DisplayName = "a response")]
    public async Task ServeAsync_MessageTheAnswererDrops_ClosesWithNoBytes(byte[] message)
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync([[.. Frame(message), .. Frame(Query("web.example.test", TypeA))]], log);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("DNS connection closed: a message with no readable query header, or a response", log.Notes.Single());
    }

    [TestMethod]
    [DataRow(1, DisplayName = "inside the length prefix")]
    [DataRow(10, DisplayName = "inside the message")]
    public async Task ServeAsync_ClientClosesMidFrame_ClosesWithNoBytes(int sentLength)
    {
        var connection = await ServeAsync([Frame(Query("web.example.test", TypeA))[..sentLength]]);

        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_MessageNotFinishedWithinTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        await using var connection = new InMemoryConnection([Frame(Query("web.example.test", TypeA))[..5]], peerHalfClosesWhenExhausted: false);

        var serving = new DnsTcpProtocolServer(LoadZonesWithALargeAnswer()).ServeAsync(connection, Context(log, clock: clock));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("DNS connection closed: head timeout", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_IdleConnectionCancelledForALimit_ClosesWithNoMoreBytes()
    {
        var log = new RecordingExchangeLog();
        using var exchange = new CancellationTokenSource();
        await using var connection = new InMemoryConnection([Frame(Query("web.example.test", TypeA))], peerHalfClosesWhenExhausted: false);

        var serving = new DnsTcpProtocolServer(LoadZonesWithALargeAnswer()).ServeAsync(connection, Context(log, exchange.Token));
        var answered = connection.WrittenBytes;
        await exchange.CancelAsync();
        await serving;

        Assert.HasCount(1, Frames(answered));
        CollectionAssert.AreEqual(answered, connection.WrittenBytes);
        Assert.AreEqual("DNS connection closed: idle timeout or maximum duration", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_Shutdown_EndsWithOperationCanceled()
    {
        using var exchange = new CancellationTokenSource();
        await using var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var context = Context(new RecordingExchangeLog(), exchange.Token) with { ShutdownToken = exchange.Token };

        var serving = new DnsTcpProtocolServer(new DnsZoneSet()).ServeAsync(connection, context);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    [DataRow((ushort)252, DisplayName = "AXFR")]
    [DataRow((ushort)251, DisplayName = "IXFR")]
    public async Task ServeAsync_ZoneTransferByDefault_IsRefusedWithTheQuestionEchoed(ushort type)
    {
        var message = ReadWhole(Frames((await ServeAsync([Frame(Query("example.test", type))])).WrittenBytes).Single());

        Assert.AreEqual(DnsResponseCode.Refused, message.Header.ResponseCode);
        Assert.AreEqual((DnsRecordType)type, message.Questions.Single().Type);
        Assert.IsEmpty(message.Answers);
    }

    [TestMethod]
    [DataRow((ushort)252, DisplayName = "AXFR")]
    [DataRow((ushort)251, DisplayName = "IXFR")]
    public async Task ServeAsync_ZoneTransferAllowed_SendsTheZoneBetweenTwoSoas(ushort type)
    {
        var zones = DnsTestZones.LoadExample();
        var connection = await ServeAsync([Frame(Query("EXAMPLE.test", type, id: 7))], zones: zones, allowZoneTransfer: true);

        var message = ReadWhole(Frames(connection.WrittenBytes).Single());
        Assert.AreEqual((ushort)7, message.Header.Id);
        Assert.AreEqual(DnsResponseCode.NoError, message.Header.ResponseCode);
        Assert.IsTrue(message.Header.IsAuthoritativeAnswer);
        Assert.AreEqual("EXAMPLE.test", message.Questions.Single().Name.ToString().TrimEnd('.'));
        var zone = zones.ZoneAtApex(DnsName.Parse("example.test"))!;
        Assert.HasCount(zone.RecordCount + 1, message.Answers);
        Assert.AreEqual(DnsRecordType.SOA, message.Answers[0].Type);
        Assert.AreEqual(DnsRecordType.SOA, message.Answers[^1].Type);
        Assert.IsFalse(message.Answers.Skip(1).SkipLast(1).Any(record => record.Type == DnsRecordType.SOA));
    }

    [TestMethod]
    public async Task ServeAsync_ZoneTransferPastOneFrame_SpansFramesWithTheQuestionInTheFirstOnly()
    {
        var zones = DnsTestZones.Load(("large.test.zone", DnsTestZones.Minimal(
            Enumerable.Range(0, 2000).Select(index => $"r{index} TXT \"{new string('x', 60)}\"").ToArray())));

        var frames = Frames((await ServeAsync([Frame(Query("large.test", 252, additionals: Opt(1232)))], zones: zones, allowZoneTransfer: true)).WrittenBytes);

        Assert.IsGreaterThan(1, frames.Count);
        Assert.IsTrue(frames.All(frame => frame.Length <= 65535));
        var messages = frames.Select(ReadWhole).ToArray();
        Assert.HasCount(1, messages[0].Questions);
        Assert.IsTrue(messages.Skip(1).All(message => message.Questions.Count == 0));
        Assert.IsTrue(messages.All(message => message.Additionals.Single().Type == DnsRecordType.OPT));
        var records = messages.SelectMany(message => message.Answers).ToArray();
        Assert.HasCount(2002, records);
        Assert.AreEqual(DnsRecordType.SOA, records[0].Type);
        Assert.AreEqual(DnsRecordType.SOA, records[^1].Type);
    }

    [TestMethod]
    [DataRow("web.example.test", (ushort)1, DisplayName = "a name below the apex")]
    [DataRow("nowhere.invalid", (ushort)1, DisplayName = "a name in no zone")]
    [DataRow("example.test", (ushort)3, DisplayName = "class CH")]
    public async Task ServeAsync_ZoneTransferAllowedNotAtAServedApex_IsNotAuth(string name, ushort questionClass)
    {
        var connection = await ServeAsync([Frame(Query(name, 252, questionClass: questionClass))], allowZoneTransfer: true);

        var message = ReadWhole(Frames(connection.WrittenBytes).Single());
        Assert.AreEqual(DnsResponseCode.NotAuthoritative, message.Header.ResponseCode);
        Assert.HasCount(1, message.Questions);
        Assert.IsEmpty(message.Answers);
    }

    [TestMethod]
    public async Task ServeAsync_ZoneTransferAllowedInClassAny_SendsTheZone()
    {
        var connection = await ServeAsync([Frame(Query("example.test", 252, questionClass: 255))], allowZoneTransfer: true);

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(Frames(connection.WrittenBytes).Single()).Header.ResponseCode);
    }

    // Serves inbound on a connection the client half-closes once it is sent, and returns it.
    private static async Task<InMemoryConnection> ServeAsync(
        IEnumerable<byte[]> inbound,
        RecordingExchangeLog? log = null,
        ExchangeLimits? limits = null,
        DnsZoneSet? zones = null,
        bool allowZoneTransfer = false)
    {
        var server = new DnsTcpProtocolServer(zones ?? LoadZonesWithALargeAnswer(), allowZoneTransfer);
        var connection = new InMemoryConnection(inbound.Select(chunk => (ReadOnlyMemory<byte>)chunk));
        var context = Context(log ?? new RecordingExchangeLog()) with { Limits = limits ?? ExchangeLimits.Default };

        await server.ServeAsync(connection, context);
        return connection;
    }

    private static byte[] Frame(byte[] message) => [(byte)(message.Length >> 8), (byte)message.Length, .. message];

    private static List<byte[]> Frames(byte[] written)
    {
        var frames = new List<byte[]>();

        for (var offset = 0; offset < written.Length;)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(offset));
            frames.Add(written[(offset + 2)..(offset + 2 + length)]);
            offset += 2 + length;
        }

        return frames;
    }

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken = default, TimeProvider? clock = null) => new(
        1,
        new ListenUrl("dns", "127.0.0.1", 53).WithBoundPort(53),
        new IPEndPoint(IPAddress.Loopback, 53),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock ?? TimeProvider.System,
        cancellationToken);
}
