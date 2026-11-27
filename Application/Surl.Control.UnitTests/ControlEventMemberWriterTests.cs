using System.Reflection;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventMemberWriterTests
{
    private static readonly HashSet<string> NotWrittenMembers = new(StringComparer.Ordinal)
    {
        nameof(SurlEvent.Sequence), nameof(SurlEvent.At), nameof(SurlEvent.KindName), nameof(SurlEvent.Scheme), nameof(SurlEvent.Account),
    };

    private static SurlEvent[] OneOfEveryKind() =>
    [
        new ExchangeStartedEvent(1, "http://127.0.0.1:80", "http", ControlHarness.LoopbackPeer),
        new ExchangeBytesEvent(2, ExchangeDirection.Sent, new byte[] { 1, 2 }) { Layer = ExchangeBytesLayer.Decrypted, StreamId = 4 },
        new ExchangeEndedEvent(3, 5, 6),
        new LoginJudgedEvent(4, "smtp", "alice", "PLAIN", IsAccepted: true, IsRefusedByFault: true),
        new StoreChangedEvent(StoreKind.Mail, StoreChange.Renamed, "INBOX", 1, 5) { PreviousName = "Old" , DeliveredFor = ["a@x", "b@x"] },
        new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Accept, 6, "smtp://127.0.0.1:25") { Close = FaultClose.Reset },
        new HookFailedEvent("h1", HookFailure.Threw, "x"),
        new ConsumerGroupChangedEvent(7, "kafka", "orders", ConsumerGroupChange.MemberJoined, "c-1", 1, "alice"),
        new CookieReceivedEvent(8, "http", "sid", "abc123"),
    ];

    private static IEnumerable<Type> ExportedEventTypes() =>
        typeof(SurlEvent).Assembly.GetExportedTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(SurlEvent)));

    [TestMethod]
    public void Known_EveryExportedSurlEventRecord_IsAKindACallerMayFilterBy()
    {
        foreach (var eventType in ExportedEventTypes())
        {
            var sample = OneOfEveryKind().SingleOrDefault(candidate => candidate.GetType() == eventType);

            Assert.IsNotNull(sample, $"{eventType.Name} has no sample in OneOfEveryKind");
            Assert.AreEqual(sample.KindName, ControlEventKinds.NameOfType(eventType));
            Assert.Contains(sample.KindName, ControlEventKinds.Known, $"{eventType.Name} is not in ControlEventKinds.Known");
            Assert.AreEqual(sample.KindName, ControlEventKinds.NameOf(sample));
        }

        Assert.HasCount(ExportedEventTypes().Count(), ControlEventKinds.Known);
    }

    [TestMethod]
    public void ToJson_EveryPublicPropertyOfEveryExportedSurlEventRecord_IsInItsJson()
    {
        foreach (var surlEvent in OneOfEveryKind())
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(ControlEventWriter.ToJson(surlEvent), ControlJsonContext.Default.ControlEventJson));
            var written = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var properties = surlEvent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Where(name => !NotWrittenMembers.Contains(name) && surlEvent.GetType().GetProperty(name)!.GetValue(surlEvent) is not null);

            foreach (var name in properties)
            {
                var jsonName = name switch
                {
                    "Bytes" => "data",
                    "Kind" => "fault_kind",
                    _ => JsonNamingPolicy.SnakeCaseLower.ConvertName(name),
                };

                Assert.Contains(jsonName, written, $"{surlEvent.GetType().Name}.{name} is missing from its control JSON");
            }
        }
    }

    [TestMethod]
    public void ToJson_ExchangeBytes_WritesLayerStreamAndTheBytes()
    {
        var json = ControlEventWriter.ToJson((ExchangeBytesEvent)OneOfEveryKind()[1]);

        Assert.AreEqual("decrypted", json.Layer);
        Assert.AreEqual(4, json.StreamId);
        Assert.AreEqual("sent", json.Direction);
        Assert.AreEqual(2, json.ByteCount);
        Assert.IsFalse(json.IsCut);
    }

    [TestMethod]
    public void Number_GenerationIdWithAValueOrNull_WritesItOrLeavesItNull()
    {
        var writer = new ControlEventMemberWriter(new ControlEventJson { Sequence = 1, At = "t", Kind = "k" });

        writer.Number("GenerationId", 7);
        Assert.AreEqual(7, writer.Written.GenerationId);

        writer.Number("GenerationId", null);
        Assert.IsNull(writer.Written.GenerationId);
    }

    [TestMethod]
    public void Text_NameTheJsonHasNoPlaceFor_Throws()
    {
        var writer = new ControlEventMemberWriter(new ControlEventJson { Sequence = 1, At = "t", Kind = "k" });

        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Text("Nope", "x"));
        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Number("Nope", 1));
        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Flag("Nope", true));
        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Choice("Nope", ExchangeDirection.Sent));
        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Bytes("Nope", default));
    }
}
