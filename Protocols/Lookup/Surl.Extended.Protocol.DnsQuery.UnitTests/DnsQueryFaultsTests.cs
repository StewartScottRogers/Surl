using System.Net;
using System.Text;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; BL-984): a standard query an
/// <see cref="FaultKind.Error"/> rule matches is answered with the rule's RCODE and nothing else,
/// on every transport, and one <see cref="FaultAppliedEvent"/> is published. Queries and expected
/// answers are written by hand from RFC 1035 section 4.1.
/// </summary>
[TestClass]
public sealed class DnsQueryFaultsTests
{
    private const ushort TypeA = 1;

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? path = "web.example.test", string? reply = "2") =>
        new("f1", FaultKind.Error) { Scheme = "dns", Command = "A", Path = path, Reply = reply };

    // The response to Query("web.example.test", A): ID and RD echoed, QR = 1, RCODE as chosen,
    // the question echoed and nothing else.
    private static byte[] ChosenAnswer(byte responseCode) =>
        [0xBE, 0xEF, 0x81, responseCode, 0, 1, 0, 0, 0, 0, 0, 0, .. Query("web.example.test", TypeA)[12..]];

    [TestMethod]
    public async Task ServeAsync_UdpQueryUnderARule_GetsExactlyTheChosenRcodeAndPublishesOneEvent()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (sent, events, log) = await ServeOverUdpAsync(faults, Query("web.example.test", TypeA));

        CollectionAssert.AreEqual(ChosenAnswer(0x02), sent.Single());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "web.example.test"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "dns://127.0.0.1:53/", Scheme = "dns", Path = "web.example.test", Command = "A" },
            faults.Points.Single());
        Assert.Contains("The query was answered RCODE 2 by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_UdpQueryTheRuleDoesNotName_IsAnsweredFromTheZones()
    {
        var (sent, events, _) = await ServeOverUdpAsync(new ScriptedFaultPlan(ErrorRule(path: "mail.example.test")), Query("web.example.test", TypeA));

        var message = ReadWhole(sent.Single());
        Assert.AreEqual(DnsResponseCode.NoError, message.Header.ResponseCode);
        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)message.Answers.Single().Data).Address);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_UdpQueryWithAnOpt_GetsTheChosenRcodeWithAnOpt()
    {
        var (sent, _, _) = await ServeOverUdpAsync(new ScriptedFaultPlan(ErrorRule(reply: "5")), Query("web.example.test", TypeA, additionals: Opt(4096)));

        var message = ReadWhole(sent.Single());
        Assert.AreEqual(DnsResponseCode.Refused, message.Header.ResponseCode);
        Assert.IsEmpty(message.Answers);
        var opt = DnsOptRecord.FromResourceRecord(message.Additionals.Single());
        Assert.AreEqual(DnsQueryAnswerer.ServerPayloadSize, opt.PayloadSize);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyThatIsNoRcodeOneToFive_IsNotAppliedAndIsNoted()
    {
        var (sent, events, log) = await ServeOverUdpAsync(new ScriptedFaultPlan(ErrorRule(reply: "9")), Query("web.example.test", TypeA));

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(sent.Single()).Header.ResponseCode);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the query: its reply is not an RCODE 1 to 5.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDropRule_IsNotAppliedAndIsNoted()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command };

        var (sent, events, log) = await ServeOverUdpAsync(new ScriptedFaultPlan(drop), Query("web.example.test", TypeA));

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(sent.Single()).Header.ResponseCode);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the query: a Drop rule is not applied at a query.", log.Notes);
    }

    [TestMethod]
    [DataRow((ushort)0x8500, 1, DisplayName = "a response")]
    [DataRow((ushort)0x1100, 1, DisplayName = "opcode STATUS")]
    [DataRow(RecursionDesired, 2, DisplayName = "two questions")]
    [DataRow(RecursionDesired, 0, DisplayName = "no question")]
    public async Task ServeAsync_MessageThatIsNoStandardQueryWithOneQuestion_AsksNoRule(ushort flags, int questionCount)
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "2" });

        var (_, events, _) = await ServeOverUdpAsync(faults, Query("web.example.test", TypeA, flags: flags, questionCount: questionCount));

        Assert.IsEmpty(faults.Points);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_UnreadableMessageOrTwoOpts_AsksNoRule()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "2" });

        await ServeOverUdpAsync(faults, Query("web.example.test", TypeA)[..14]);
        await ServeOverUdpAsync(faults, Query("web.example.test", TypeA, additionals: [Opt(1232), Opt(1232)]));
        await ServeOverUdpAsync(faults, [0xBE, 0xEF]);

        Assert.IsEmpty(faults.Points);
    }

    [TestMethod]
    public async Task ServeAsync_TcpQueryUnderARule_GetsExactlyTheChosenRcode()
    {
        var events = new RecordingSurlEventPublisher();
        var server = new DnsTcpProtocolServer(DnsTestZones.LoadExample());
        var query = Query("web.example.test", TypeA);
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)(byte[])[0, (byte)query.Length, .. query]]);

        await server.ServeAsync(connection, Context(new RecordingExchangeLog(), new ScriptedFaultPlan(ErrorRule(reply: "3")), events, default));

        CollectionAssert.AreEqual((byte[])[0, (byte)ChosenAnswer(0x03).Length, .. ChosenAnswer(0x03)], connection.WrittenBytes);
        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_TcpQueryNoRuleNames_IsAnsweredFromTheZones()
    {
        var server = new DnsTcpProtocolServer(DnsTestZones.LoadExample());
        var query = Query("web.example.test", TypeA);
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)(byte[])[0, (byte)query.Length, .. query]]);

        await server.ServeAsync(connection, Context(new RecordingExchangeLog(), new ScriptedFaultPlan(), new RecordingSurlEventPublisher(), default));

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(connection.WrittenBytes[2..]).Header.ResponseCode);
    }

    [TestMethod]
    public async Task ServeAsync_QuicQueryUnderARule_GetsExactlyTheChosenRcode()
    {
        var events = new RecordingSurlEventPublisher();
        var context = DnsOverQuicConnectionServerTests.Context(new RecordingExchangeLog()) with { Faults = new ScriptedFaultPlan(ErrorRule(reply: "1")), Events = events };
        using var client = new DnsOverQuicTestClient();
        client.Serve(new DnsOverQuicConnectionServer(new DnsQueryAnswerer(DnsTestZones.LoadExample()), context), TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), TestContext.CancellationToken);

        var answer = client.Answers(0).Single();
        Assert.AreEqual(DnsResponseCode.FormatError, answer.Header.ResponseCode);
        Assert.IsEmpty(answer.Answers);
        Assert.HasCount(1, events.Events);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public void HttpsHandlerConstructor_NullExchange_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverHttpsHandler(new DnsZoneSet(), (ExchangeContext)null!));

    [TestMethod]
    public async Task AnswerAsync_HttpsQueryUnderARule_Is200WithExactlyTheChosenRcode()
    {
        var events = new RecordingSurlEventPublisher();
        var context = Context(new RecordingExchangeLog(), new ScriptedFaultPlan(ErrorRule(reply: "4")), events, TestContext.CancellationToken);

        var body = await GetOverHttpsAsync(context, Query("web.example.test", TypeA));

        CollectionAssert.AreEqual(ChosenAnswer(0x04), body);
        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public async Task AnswerAsync_HttpsQueryNoRuleNames_IsAnsweredFromTheZones()
    {
        var context = Context(new RecordingExchangeLog(), new ScriptedFaultPlan(), new RecordingSurlEventPublisher(), TestContext.CancellationToken);

        var body = await GetOverHttpsAsync(context, Query("web.example.test", TypeA));

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(body).Header.ResponseCode);
    }

    [TestMethod]
    [DataRow((ushort)1, "A")]
    [DataRow((ushort)15, "MX")]
    [DataRow((ushort)65280, "TYPE65280")]
    public void CommandOf_NamesTheQtype(ushort type, string expected) =>
        Assert.AreEqual(expected, DnsQueryFaults.CommandOf((DnsRecordType)type));

    [TestMethod]
    [DataRow(".", ".")]
    [DataRow("Web.EXAMPLE.test.", "web.example.test")]
    [DataRow("a-1.z9.", "a-1.z9")]
    public void PathOf_IsTheNameInLowerCaseWithoutItsTrailingDot(string name, string expected) =>
        Assert.AreEqual(expected, DnsQueryFaults.PathOf(DnsName.Parse(name)));

    [TestMethod]
    [DataRow("1", DnsResponseCode.FormatError)]
    [DataRow("2", DnsResponseCode.ServerFailure)]
    [DataRow("3", DnsResponseCode.NonExistentDomain)]
    [DataRow("4", DnsResponseCode.NotImplemented)]
    [DataRow("5", DnsResponseCode.Refused)]
    public void TryParse_RcodeOneToFive_GivesIt(string reply, DnsResponseCode expected)
    {
        Assert.IsTrue(DnsQueryFaults.TryParse(reply, out var responseCode));
        Assert.AreEqual(expected, responseCode);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("0", DisplayName = "NOERROR")]
    [DataRow("6", DisplayName = "past 5")]
    [DataRow("12", DisplayName = "two digits")]
    [DataRow("SERVFAIL", DisplayName = "a mnemonic")]
    public void TryParse_AnythingElse_DoesNotFit(string? reply)
    {
        Assert.IsFalse(DnsQueryFaults.TryParse(reply, out var responseCode));
        Assert.AreEqual(DnsResponseCode.NoError, responseCode);
    }

    private async Task<(IReadOnlyList<byte[]> Sent, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> ServeOverUdpAsync(IFaultPlan faults, byte[] datagram)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var server = new DnsUdpProtocolServer(DnsTestZones.LoadExample());
        await using var flow = new InMemoryDatagramFlow(datagram);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var serving = server.ServeAsync(flow, Context(log, faults, events, exchange.Token));
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        return (flow.SentDatagrams, events, log);
    }

    private static async Task<byte[]> GetOverHttpsAsync(ExchangeContext context, byte[] query)
    {
        var parameter = Convert.ToBase64String(query).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var head = $"GET {DnsOverHttpsHandler.Path}?dns={parameter} HTTP/1.1\r\nHost: dns.example\r\nConnection: close\r\n\r\n";
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes(head)]);

        await new Http1ConnectionServer(new DnsOverHttpsHandler(DnsTestZones.LoadExample(), context), context).ServeAsync(connection);

        var written = connection.WrittenBytes;
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", Encoding.ASCII.GetString(written));
        return written[(written.AsSpan().IndexOf("\r\n\r\n"u8) + 4)..];
    }

    private static ExchangeContext Context(IExchangeLog log, IFaultPlan faults, ISurlEventPublisher events, CancellationToken cancellationToken) => new ExchangeContext(
        1,
        new ListenUrl("dns", "127.0.0.1", 53).WithBoundPort(53),
        new IPEndPoint(IPAddress.Loopback, 53),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        new ManualTimeProvider(),
        cancellationToken) with
    { Faults = faults, Events = events };
}
