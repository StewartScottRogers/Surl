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
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"store_changed","store":"content","change":"renamed","name":"b.txt","byte_count":5,"exchange_id":4,"previous_name":"a.txt"}""");

    [TestMethod]
    public void WriteEvent_StoreChangedDeliveredFor_WritesTheAddressesAsAnArray() =>
        AssertWritten(
            new StoreChangedEvent(StoreKind.Mail, StoreChange.Written, "INBOX", 5, 4) { DeliveredFor = ["a@x", "b@x"] },
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"store_changed","store":"mail","change":"written","name":"INBOX","byte_count":5,"exchange_id":4,"delivered_for":["a@x","b@x"]}""");

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
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"fault_applied","rule_id":"f1","fault_kind":""" + "\"");
        using var document = JsonDocument.Parse(written);
        Assert.AreEqual(JsonNamingPolicy.SnakeCaseLower.ConvertName(fault.Kind.ToString()), document.RootElement.GetProperty("fault_kind").GetString());
        Assert.AreEqual(JsonNamingPolicy.SnakeCaseLower.ConvertName(fault.Stage.ToString()), document.RootElement.GetProperty("stage").GetString());
        Assert.AreEqual(4, document.RootElement.GetProperty("exchange_id").GetInt64());
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
    public void WriteEvent_ExchangeBytes_WritesItsDirectionBytesAndLayerAsHexadecimal() =>
        AssertWritten(
            new ExchangeBytesEvent(4, ExchangeDirection.Received, "x"u8.ToArray()),
            """{"sequence":9,"at":"2026-11-05T10:00:00.0000000+00:00","kind":"exchange_bytes","exchange_id":4,"direction":"received","bytes":"78","layer":"wire"}""");

    [TestMethod]
    public void WriteEvent_ExchangeBytesPastTheBound_WritesOnlyTheFirstBytes()
    {
        var bytes = new byte[RecordedEventWriter.MaxByteCount + 10];

        using var document = JsonDocument.Parse(Write(new ExchangeBytesEvent(4, ExchangeDirection.Sent, bytes) { StreamId = 3 }));

        Assert.AreEqual(RecordedEventWriter.MaxByteCount * 2, document.RootElement.GetProperty("bytes").GetString()!.Length);
        Assert.AreEqual(3, document.RootElement.GetProperty("stream_id").GetInt64());
    }

    [TestMethod]
    public void Kinds_EveryExportedEventRecord_IsInTheSchemaIsMatchedByItsKindAndWritesEveryPublicProperty()
    {
        var types = typeof(SurlEvent).Assembly.GetExportedTypes().Where(type => type.IsSubclassOf(typeof(SurlEvent))).ToList();
        Assert.IsNotEmpty(types);
        foreach (var type in types)
        {
            var surlEvent = (SurlEvent)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            var kind = surlEvent.KindName;

            StringAssert.Contains(EventToolFilter.EventProperties, "\"" + kind + "\"", type.Name);
            Assert.Contains(kind, EventToolFilter.AllKinds, type.Name);
            var filter = new EventToolFilter(new Surl.Api.SurlEventQuery(), new HashSet<string> { kind });
            Assert.IsTrue(filter.IsOfKind(surlEvent), type.Name);

            var sample = Samples.SingleOrDefault(candidate => candidate.GetType() == type);
            Assert.IsNotNull(sample, $"{type.Name} needs a sample in Samples");
            var recorded = new RecordingMembers();
            sample.WriteMembers(recorded);
            // StoreChangedEvent.Account only restates its Name when the store is accounts, so it is not a member of its own.
            foreach (var property in type.GetProperties().Where(property => property.DeclaringType != typeof(SurlEvent) && !(type == typeof(StoreChangedEvent) && property.Name == "Account")))
            {
                Assert.Contains(property.Name, recorded.Names, $"{type.Name}.{property.Name}");
            }
        }
    }

    private static readonly SurlEvent[] Samples =
    [
        new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.MemberJoined, "m", 1, "a"),
        new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[1]),
        new ExchangeEndedEvent(1, 1, 1),
        new ExchangeStartedEvent(1, "http://127.0.0.1:1/", "http", new IPEndPoint(IPAddress.Loopback, 1)),
        new FaultAppliedEvent("f", default, default, 1, "/"),
        new HookFailedEvent("h", HookFailure.TimedOut, "m"),
        new LoginJudgedEvent(1, "smtp", "a", "PLAIN", true, false),
        new StoreChangedEvent(StoreKind.Content, StoreChange.Renamed, "b", 1, 1) { PreviousName = "a" },
    ];

    private sealed class RecordingMembers : ISurlEventMemberWriter
    {
        public List<string> Names { get; } = [];

        public void Text(string name, string? value) => Names.Add(name);

        public void TextList(string name, IReadOnlyList<string>? values) => Names.Add(name);

        public void Number(string name, long? value) => Names.Add(name);

        public void Flag(string name, bool value) => Names.Add(name);

        public void Choice(string name, Enum value) => Names.Add(name);

        public void Bytes(string name, ReadOnlyMemory<byte> value) => Names.Add(name);
    }
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
