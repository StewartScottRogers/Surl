using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

[TestClass]
public sealed class RecordedEventWriterTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 5, 11, 0, 0, TimeSpan.FromHours(1));

    [TestMethod]
    public void WriteEvent_ExchangeStarted_WritesItsHeadListenUrlSchemeAndClient() =>
        AssertWritten(
            new ExchangeStartedEvent(4, "http://127.0.0.1:49731/", "http", new IPEndPoint(IPAddress.Loopback, 50123)),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"exchange_started","exchange_id":4,"listen_url":"http://127.0.0.1:49731/","scheme":"http","client":"127.0.0.1:50123"}""");

    [TestMethod]
    public void WriteEvent_ExchangeEnded_WritesItsByteCounts() =>
        AssertWritten(
            new ExchangeEndedEvent(4, 78, 120),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"exchange_ended","exchange_id":4,"received_byte_count":78,"sent_byte_count":120}""");

    [TestMethod]
    public void WriteEvent_LoginJudged_WritesTheAccountAndVerdictAndNoSecret() =>
        AssertWritten(
            new LoginJudgedEvent(4, "smtp", "bob", "PLAIN", IsAccepted: false, IsRefusedByFault: true),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"login_judged","exchange_id":4,"scheme":"smtp","account":"bob","mechanism":"PLAIN","is_accepted":false,"is_refused_by_fault":true}""");

    [TestMethod]
    public void WriteEvent_StoreChangedWithEveryMember_WritesThemInSnakeCase() =>
        AssertWritten(
            new StoreChangedEvent(StoreKind.Content, StoreChange.Renamed, "b.txt", 5, 4) { PreviousName = "a.txt" },
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"store_changed","exchange_id":4,"store":"content","change":"renamed","name":"b.txt","previous_name":"a.txt","byte_count":5}""");

    [TestMethod]
    public void WriteEvent_StoreChangedWithNoExchangeByteCountOrPreviousName_LeavesThemOut() =>
        AssertWritten(
            new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "alice", null, null),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"store_changed","store":"accounts","change":"account_added","name":"alice"}""");

    [TestMethod]
    public void WriteEvent_FaultApplied_WritesTheRuleKindStageAndTarget()
    {
        var fault = new FaultAppliedEvent("f1", default, default, 4, "/x");

        var written = Write(fault);

        StringAssert.StartsWith(
            written,
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"fault_applied","exchange_id":4,"rule_id":"f1","fault_kind":""" + "\"");
        using var document = JsonDocument.Parse(written);
        Assert.AreEqual(JsonNamingPolicy.SnakeCaseLower.ConvertName(fault.Kind.ToString()), document.RootElement.GetProperty("fault_kind").GetString());
        Assert.AreEqual(JsonNamingPolicy.SnakeCaseLower.ConvertName(fault.Stage.ToString()), document.RootElement.GetProperty("stage").GetString());
        Assert.AreEqual("/x", document.RootElement.GetProperty("target").GetString());
    }

    [TestMethod]
    public void WriteEvent_HookFailed_WritesTheHookFailureAndMessage() =>
        AssertWritten(
            new HookFailedEvent("h1", HookFailure.TimedOut, "took too long"),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"hook_failed","hook_id":"h1","failure":"timed_out","message":"took too long"}""");

    [TestMethod]
    public void WriteEvent_ConsumerGroupChangedWithEveryMember_WritesThemInSnakeCase() =>
        AssertWritten(
            new ConsumerGroupChangedEvent(4, "kafka", "g1", ConsumerGroupChange.MemberJoined, "m1", 2, "alice"),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"consumer_group_changed","exchange_id":4,"scheme":"kafka","group":"g1","change":"member_joined","member_id":"m1","generation_id":2,"account":"alice"}""");

    [TestMethod]
    public void WriteEvent_ConsumerGroupChangedWithNoExchangeMemberOrAccount_LeavesThemOut() =>
        AssertWritten(
            new ConsumerGroupChangedEvent(null, "kafka", "g1", ConsumerGroupChange.GenerationStarted, null, 3, null),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"consumer_group_changed","scheme":"kafka","group":"g1","change":"generation_started","generation_id":3}""");

    [TestMethod]
    public void WriteEvent_ExchangeBytesWhichNoServerKeeps_WritesItsHeadAlone() =>
        AssertWritten(
            new ExchangeBytesEvent(4, ExchangeDirection.Received, "x"u8.ToArray()),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"exchange_bytes"}""");

    private static void AssertWritten(SurlEvent surlEvent, string expected) => Assert.AreEqual(expected, Write(surlEvent));

    private static string Write(SurlEvent surlEvent)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            RecordedEventWriter.WriteEvent(writer, surlEvent with { Sequence = 9, At = At });
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
