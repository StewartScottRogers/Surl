using System.Net;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Grpc.GrpcHttp2TestFrames;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The chosen-error, delay and drop faults at a call (ADR-0124 decisions 4 and 9; BL-1188), judged by
/// the gRPC status codes document: an error rule ends the call Trailers-Only with the chosen
/// <c>grpc-status</c> and <c>grpc-message</c>.
/// </summary>
[TestClass]
public sealed class GrpcCallFaultTests
{
    private const string EchoPath = "/surl.test.Echo/Echo";
    private static readonly byte[] Hi = [0x0A, 0x02, 0x68, 0x69];

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule Rule(FaultKind kind, string? reply = null, string? command = "Echo", string? path = EchoPath, int? ms = null) =>
        new("f1", kind) { Scheme = "grpc", Command = command, Path = path, Reply = reply, DelayMilliseconds = ms };

    private async Task<(IReadOnlyList<Hpack.HpackHeaderField> Fields, byte[] Body, uint? ResetCode, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> CallAsync(FaultRule rule, string path = EchoPath)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var context = new ExchangeContext(
            1,
            new ListenUrl("grpc", "127.0.0.1", 80).WithBoundPort(80),
            new IPEndPoint(IPAddress.Loopback, 80),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            new ShortTimersOnlyTimeProvider(),
            TestContext.CancellationToken)
        {
            Faults = new ScriptedFaultPlan(rule),
            Events = events,
        };
        var handler = new GrpcStreamHandler([EchoService.Service], UnitTestHttpAuthenticationSession.ProceedingEveryRequest, context);
        var client = new GrpcHttp2TestFrames();
        var connection = new InMemoryConnection([(byte[])[.. Start, .. client.Call(1, path, endStream: false), .. Data(1, Message(Hi), endStream: true)]]);

        await new Http2ConnectionServer(handler, context, connection).ServeAsync();
        var (fields, body, _, resetCode) = Response(connection.WrittenBytes, 1);

        return (fields, body, resetCode, events, log);
    }

    private static string Text(IReadOnlyList<Hpack.HpackHeaderField> fields) =>
        string.Join("\n", fields.Select(field => $"{field.Name}: {field.Value}"));

    [TestMethod]
    public async Task ServeStreamAsync_CallUnderAnErrorRule_EndsTrailersOnlyWithTheChosenStatusAndMessage()
    {
        var (fields, body, _, events, _) = await CallAsync(Rule(FaultKind.Error, "14 try later"));

        Assert.AreEqual(
            ":status: 200\ncontent-type: application/grpc\ngrpc-status: 14\ngrpc-message: try later\ngrpc-accept-encoding: identity,deflate,gzip",
            Text(fields));
        Assert.IsEmpty(body);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, EchoPath), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeStreamAsync_ErrorRuleWithNoMessage_SendsNoGrpcMessage()
    {
        var (fields, _, _, _, _) = await CallAsync(Rule(FaultKind.Error, "5"));

        Assert.IsFalse(fields.Any(field => field.Name == "grpc-message"));
        Assert.AreEqual("5", fields.Single(field => field.Name == "grpc-status").Value);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CallNotMatchingTheRule_IsServedAndNothingIsPublished()
    {
        var (fields, body, _, events, _) = await CallAsync(Rule(FaultKind.Error, "14 x", command: "Expand", path: "/surl.test.Echo/Expand"));

        Assert.AreEqual("0", fields.Single(field => field.Name == "grpc-status").Value);
        Assert.IsNotEmpty(body);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(FaultKind.Error, "0 ok", "its reply is not a status code 1 to 16", DisplayName = "OK is not an error")]
    [DataRow(FaultKind.Error, "17", "its reply is not a status code 1 to 16", DisplayName = "Past 16")]
    [DataRow(FaultKind.Error, "boom", "its reply is not a status code 1 to 16", DisplayName = "Not a code")]
    [DataRow(FaultKind.Error, null, "its reply is not a status code 1 to 16", DisplayName = "No reply")]
    [DataRow(FaultKind.RefuseLogin, null, "a RefuseLogin rule is not applied at a call", DisplayName = "Another kind")]
    public async Task ServeStreamAsync_RuleItCannotApply_IsNotedAndTheCallIsServed(FaultKind kind, string? reply, string why)
    {
        var (fields, _, _, events, log) = await CallAsync(Rule(kind, reply));

        Assert.AreEqual("0", fields.Single(field => field.Name == "grpc-status").Value);
        Assert.IsEmpty(events.Events);
        Assert.Contains($"Fault rule f1 not applied to gRPC call {EchoPath}: {why}.", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CallUnderADelayRule_IsHeldThenAnswered()
    {
        var (fields, body, _, events, log) = await CallAsync(Rule(FaultKind.Delay, ms: 1));

        Assert.AreEqual("0", fields.Single(field => field.Name == "grpc-status").Value);
        Assert.IsNotEmpty(body);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, EchoPath), events.Events.Single());
        Assert.Contains("Echo held for 0.001 s by fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_CallUnderADropRule_ResetsTheStreamWithNoResponse()
    {
        var (fields, body, resetCode, events, log) = await CallAsync(Rule(FaultKind.Drop));

        Assert.IsEmpty(fields);
        Assert.IsEmpty(body);
        Assert.IsNotNull(resetCode);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, EchoPath), events.Events.Single());
        Assert.Contains("Echo dropped the connection, unanswered, by fault rule f1.", log.Notes);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryRead_NoCode_IsNull(string? reply) => Assert.IsNull(GrpcCallFaults.TryRead(reply));
}
