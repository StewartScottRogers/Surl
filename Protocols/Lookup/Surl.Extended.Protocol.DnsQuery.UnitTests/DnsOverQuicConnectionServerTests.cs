using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// DNS over QUIC (RFC 9250; ADR-0092 decision 5) over the in-memory QUIC test connection
/// (<see cref="DnsOverQuicTestClient"/>): pinned upstream curl's recorded Initials with the ALPN
/// edited to <c>doq</c>, then streams written by hand from RFC 9250 section 4.2.
/// </summary>
[TestClass]
public sealed class DnsOverQuicConnectionServerTests
{
    private const ushort TypeA = 1;
    private const ulong StreamLimitError = 0x04;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicConnectionServer((DnsQueryAnswerer)null!, Context(new RecordingExchangeLog())));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicConnectionServer(new DnsQueryAnswerer(new DnsZoneSet()), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullConnection_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(null!, Token));

    [TestMethod]
    public async Task ServeAsync_QueryOnAStream_IsAnsweredOnThatStreamWhichIsThenEndedWithFin()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var answer = client.Answers(0).Single();
        Assert.AreEqual((ushort)0, answer.Header.Id);
        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)answer.Answers.Single().Data).Address);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_QueryWhoseOptCarriesPadding_IsAnsweredWithAPaddingOption()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0, additionals: PaddedOpt(1232)));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var options = DnsOptRecord.FromResourceRecord(client.Answers(0).Single().Additionals[^1]).Options;
        Assert.AreEqual(DnsOption.PaddingCode, options.Single().Code);
    }

    [TestMethod]
    public async Task ServeAsync_TwoStreams_AreAnsweredIndependently()
    {
        using var client = await StartAsync(Server());
        var first = Query("web.example.test", TypeA, id: 0);
        byte[] firstFramed = [0, (byte)first.Length, .. first];

        client.Send(0, firstFramed[..5]);
        client.SendQuery(4, Query("nowhere.invalid", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(4), Token);
        var answeredBeforeTheFirstWasWhole = client.HasEnded(0);
        client.Send(0, firstFramed[5..], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.IsFalse(answeredBeforeTheFirstWasWhole);
        Assert.AreEqual(DnsResponseCode.Refused, client.Answers(4).Single().Header.ResponseCode);
        Assert.AreEqual(DnsResponseCode.NoError, client.Answers(0).Single().Header.ResponseCode);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_QueryWithNonZeroId_ClosesTheConnectionWithProtocolError()
    {
        var log = new RecordingExchangeLog();
        using var client = await StartAsync(Server(log));

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0x1234));
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
        Assert.IsFalse(client.HasEnded(0));
        Assert.IsTrue(log.Notes.Any(note => note.Contains("Message ID 4660, not 0", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_StreamWithNoFinAfterItsQuery_IsAnsweredAndTheConnectionStaysOpen()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0), fin: false);
        await client.UntilAsync(() => client.HasEnded(0), Token);
        client.SendQuery(4, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(4), Token);

        Assert.HasCount(1, client.Answers(0));
        Assert.HasCount(1, client.Answers(4));
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_BytesAfterTheQuery_CloseTheConnectionWithProtocolError()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0), fin: false);
        await client.UntilAsync(() => client.HasEnded(0), Token);
        client.Send(0, [0x00], fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x00 }, DisplayName = "length 0")]
    [DataRow(new byte[] { 0x00, 0x20, 0x00, 0x00 }, DisplayName = "FIN before the message is whole")]
    [DataRow(new byte[] { 0x00 }, DisplayName = "FIN inside the length")]
    [DataRow(new byte[] { 0x00, 0x01, 0x00 }, DisplayName = "a message too short for an ID")]
    public async Task ServeAsync_MalformedStream_ClosesTheConnectionWithProtocolError(byte[] stream)
    {
        using var client = await StartAsync(Server());

        client.Send(0, stream, fin: true);
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_MessagePastMaxMessage_ClosesTheConnectionWithProtocolError()
    {
        var log = new RecordingExchangeLog();
        using var client = await StartAsync(Server(log, ExchangeLimits.Default with { MaxMessageBytes = 20 }));

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
        Assert.IsTrue(log.Notes.Any(note => note.Contains("past --max-message 20", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ResponseInsteadOfAQuery_ClosesTheConnectionWithProtocolError()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0, flags: 0x8000));
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_UnidirectionalStream_ClosesTheConnectionWithProtocolError()
    {
        using var client = new DnsOverQuicTestClient();
        client.Serve(Server(), Token, maxStreamsUni: 1);
        await client.CompleteHandshakeAsync(Token);

        client.SendQuery(2, Query("web.example.test", TypeA, id: 0));
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.ProtocolError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_NoMaxMessage_AnswersAQueryOfAnyLength()
    {
        using var client = await StartAsync(Server(limits: ExchangeLimits.Default with { MaxMessageBytes = 0 }));

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.HasCount(1, client.Answers(0));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, DisplayName = "inside the length")]
    [DataRow(new byte[] { 0x00, 0x20, 0x00 }, DisplayName = "inside the message")]
    public async Task ServeAsync_StreamResetBeforeItsQueryIsWhole_IsDroppedWithoutAnAnswer(byte[] sent)
    {
        var log = new RecordingExchangeLog();
        using var client = await StartAsync(Server(log));

        client.Send(0, sent);
        client.Reset(0);
        await client.UntilAsync(() => log.Notes.Any(note => note.Contains("reset by the client", StringComparison.Ordinal)), Token);
        client.SendQuery(4, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(4), Token);

        Assert.IsFalse(client.HasEnded(0));
        Assert.IsEmpty(client.StreamSent(0).Data);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_StreamPastTheHundredAllowed_IsRefusedByQuicWithStreamLimitError()
    {
        using var client = await StartAsync(Server());

        client.SendQuery(400, Query("web.example.test", TypeA, id: 0));
        await client.FinishAsync(Token);

        Assert.AreEqual(StreamLimitError, client.TransportCloseCode);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_AnswerWithSeveralMessages_SendsEachLengthPrefixedAndFinAfterTheLast()
    {
        var answerer = new DnsQueryAnswerer(DnsTestZones.LoadExample());
        var server = new DnsOverQuicConnectionServer(query => [.. answerer.AnswerOverStream(query), .. answerer.AnswerOverStream(query)], Context(new RecordingExchangeLog()));
        using var client = await StartAsync(server);

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.HasCount(2, client.Answers(0));
    }

    [TestMethod]
    public async Task ServeAsync_AnswererFails_ClosesTheConnectionWithInternalError()
    {
        var log = new RecordingExchangeLog();
        var server = new DnsOverQuicConnectionServer(_ => throw new InvalidOperationException("the zones are gone"), Context(log));
        using var client = await StartAsync(server);

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.InternalError, client.CloseCode);
        Assert.IsTrue(log.Notes.Any(note => note.Contains("the zones are gone", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_CutsOpenStreamsOffAndClosesWithNoError()
    {
        using var exchange = new CancellationTokenSource();
        using var client = await StartAsync(Server(cancellationToken: exchange.Token));
        client.Send(0, [0x00, 0x20, 0x00]);
        client.SendQuery(4, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(4), Token);

        await exchange.CancelAsync();
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.NoError, client.CloseCode);
        Assert.IsFalse(client.HasEnded(0));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesWithAQueryHalfSent_EndsWithoutAnError()
    {
        var log = new RecordingExchangeLog();
        using var client = await StartAsync(Server(log));
        client.Send(0, [0x00, 0x20, 0x00]);
        await client.UntilAsync(() => client.Quic.FramesReceived[Surl.Quic.QuicPacketType.OneRtt].Count > 0, Token);

        client.Close();
        await client.FinishAsync(Token);

        Assert.IsNull(client.CloseCode);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("DNS over QUIC connection closed", StringComparison.Ordinal)));
    }

    private async Task<DnsOverQuicTestClient> StartAsync(DnsOverQuicConnectionServer server)
    {
        var client = new DnsOverQuicTestClient();
        client.Serve(server, Token);
        await client.CompleteHandshakeAsync(Token);
        return client;
    }

    private static DnsOverQuicConnectionServer Server(RecordingExchangeLog? log = null, ExchangeLimits? limits = null, CancellationToken cancellationToken = default) =>
        new(new DnsQueryAnswerer(DnsTestZones.LoadExample()), Context(log ?? new RecordingExchangeLog(), cancellationToken) with { Limits = limits ?? ExchangeLimits.Default });

    internal static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken = default) => new(
        1,
        new ListenUrl("dns", "127.0.0.1", 853).WithBoundPort(853),
        new IPEndPoint(IPAddress.Loopback, 853),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken);
}
