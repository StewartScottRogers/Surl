using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; ADR-0144; BL-835): a request an
/// <see cref="FaultKind.Error"/> rule matches is answered with the rule's response code, and its
/// diagnostic text as the payload (RFC 7252 section 5.5.2), in place of the resource, and one
/// <see cref="FaultAppliedEvent"/> is published. Datagrams written by hand from RFC 7252 section 3.
/// </summary>
[TestClass]
public sealed class CoapRequestFaultsTests
{
    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    // CON GET /hello.txt, Message ID 0x7D34, token A1 A2.
    private static readonly byte[] GetHello = [0x42, 0x01, 0x7D, 0x34, 0xA1, 0xA2, 0xB9, .. "hello.txt"u8];

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? path = "/hello.txt", string? reply = "5.03 try later") =>
        new("f1", FaultKind.Error) { Scheme = "coap", Command = "GET", Path = path, Reply = reply };

    private async Task<(byte[] Sent, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> ServeAsync(IFaultPlan faults, byte[] datagram)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var server = new CoapProtocolServer(new CoapTestStore().WithFile("hello.txt", "hi").Store, Anonymous, new UnitTestCoapRandomSource());
        await using var flow = new InMemoryDatagramFlow(datagram);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var context = CoapProtocolServerTests.Context(log, new ManualTimeProvider(), exchange.Token) with { Faults = faults, Events = events };
        var serving = server.ServeAsync(flow, context);
        await CoapProtocolServerTests.WaitForSendsAsync(flow, 1);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        return (flow.SentDatagrams.Single(), events, log);
    }

    [TestMethod]
    public async Task ServeAsync_RequestUnderARule_GetsTheChosenCodeAndDiagnostic()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (sent, events, log) = await ServeAsync(faults, GetHello);

        byte[] expected = [0x62, 0xA3, 0x7D, 0x34, 0xA1, 0xA2, 0xFF, .. "try later"u8];
        CollectionAssert.AreEqual(expected, sent);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/hello.txt"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "coap://127.0.0.1:5683/", Scheme = "coap", Path = "/hello.txt", Command = "GET" },
            faults.Points.Single());
        Assert.Contains("The request was answered 5.03 by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RequestTheRuleDoesNotName_IsServed()
    {
        var (sent, events, _) = await ServeAsync(new ScriptedFaultPlan(ErrorRule(path: "/other")), GetHello);

        Assert.AreEqual((byte)0x45, sent[1]);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_NoRule_IsServed()
    {
        var faults = new ScriptedFaultPlan();

        var (sent, events, _) = await ServeAsync(faults, GetHello);

        Assert.AreEqual((byte)0x45, sent[1]);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual("/hello.txt", faults.Points.Single().Path);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted()
    {
        var (sent, events, log) = await ServeAsync(new ScriptedFaultPlan(ErrorRule(reply: "2.05")), GetHello);

        Assert.AreEqual((byte)0x45, sent[1]);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the request: its reply does not fit the coap error shape.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDropRule_IsNotAppliedAndIsNoted()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command };

        var (sent, events, log) = await ServeAsync(new ScriptedFaultPlan(drop), GetHello);

        Assert.AreEqual((byte)0x45, sent[1]);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the request: a Drop rule is not applied at a request.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RequestWithNoUriPath_AsksForTheRootWithItsMethodCode()
    {
        var faults = new ScriptedFaultPlan(ErrorRule(path: "/", reply: "4.04"));

        // NON 0.05 with no options: the rule names GET, so the request is answered 4.05 as usual.
        var (sent, events, _) = await ServeAsync(faults, [0x50, 0x05, 0x00, 0x01]);

        Assert.AreEqual((byte)0x85, sent[1]);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual("/", faults.Points.Single().Path);
        Assert.AreEqual("0.05", faults.Points.Single().Command);
    }

    [TestMethod]
    [DataRow((byte)0x01, "GET")]
    [DataRow((byte)0x02, "POST")]
    [DataRow((byte)0x03, "PUT")]
    [DataRow((byte)0x04, "DELETE")]
    [DataRow((byte)0x07, "0.07")]
    public void CommandOf_NamesTheMethod(byte code, string expected) =>
        Assert.AreEqual(expected, CoapRequestFaults.CommandOf(new CoapCode(code)));

    [TestMethod]
    [DataRow("4.00", (byte)0x80, "")]
    [DataRow("4.31", (byte)0x9F, "")]
    [DataRow("5.00 boom", (byte)0xA0, "boom")]
    [DataRow("5.05 échec", (byte)0xA5, "échec")]
    public void TryParse_ReplyInTheShape_GivesTheResponse(string reply, byte code, string payload)
    {
        Assert.IsTrue(CoapRequestFaults.TryParse(reply, out var response));
        Assert.AreEqual(new CoapCode(code), response.Code);
        Assert.AreEqual(payload, Encoding.UTF8.GetString(response.Payload.Span));
        Assert.IsEmpty(response.Options);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("4.0", DisplayName = "short")]
    [DataRow("4-04", DisplayName = "no dot")]
    [DataRow("4.04x", DisplayName = "no space")]
    [DataRow("4.04 ", DisplayName = "empty text")]
    [DataRow("4.32", DisplayName = "detail past 31")]
    [DataRow("4.+1", DisplayName = "signed detail")]
    [DataRow("5.06", DisplayName = "past 5.05")]
    [DataRow("2.05", DisplayName = "success")]
    [DataRow("3.00", DisplayName = "reserved class")]
    public void TryParse_ReplyOutsideTheShape_DoesNotFit(string? reply) =>
        Assert.IsFalse(CoapRequestFaults.TryParse(reply, out _));
}
