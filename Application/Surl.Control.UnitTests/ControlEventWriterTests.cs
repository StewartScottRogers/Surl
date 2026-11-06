using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventWriterTests
{
    [TestMethod]
    public void ToJson_EventKindItDoesNotKnow_WritesItsHeadWithTheKindFromItsTypeName()
    {
        var json = ControlEventWriter.ToJson(new UnitTestOtherEvent { Sequence = 4 });

        Assert.AreEqual("unit_test_other", json.Kind);
        Assert.AreEqual(4, json.Sequence);
        Assert.IsNull(json.ExchangeId);
    }

    [TestMethod]
    public void ToJson_BytesPastTheEventBound_AreCut()
    {
        var json = ControlEventWriter.ToJson(new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[ControlEventWriter.MaxShownBytesPerEvent + 1]));

        Assert.IsTrue(json.IsCut);
        Assert.AreEqual(ControlEventWriter.MaxShownBytesPerEvent + 1, json.ByteCount);
        Assert.AreEqual(Convert.ToBase64String(new byte[ControlEventWriter.MaxShownBytesPerEvent]), json.Data);
    }

    [TestMethod]
    [DataRow(ConsumerGroupChange.MemberJoined, "member_joined")]
    [DataRow(ConsumerGroupChange.MemberLeft, "member_left")]
    [DataRow(ConsumerGroupChange.MemberFenced, "member_fenced")]
    public void ToJson_ConsumerGroupChangeOfAMember_WritesEveryMemberInSnakeCase(ConsumerGroupChange change, string written)
    {
        var surlEvent = new ConsumerGroupChangedEvent(3, "kafka", "orders", change, "c-1", 2, "alice") { Sequence = 9, At = At };

        var json = Written(surlEvent);

        Assert.AreEqual(
            "{\"sequence\":9,\"at\":\"2026-11-06T10:00:00.0000000\\u002B00:00\",\"kind\":\"consumer_group_changed\",\"exchange_id\":3,"
            + $"\"scheme\":\"kafka\",\"account\":\"alice\",\"change\":\"{written}\",\"group\":\"orders\",\"member_id\":\"c-1\",\"generation_id\":2}}",
            json);
    }

    [TestMethod]
    [DataRow(ConsumerGroupChange.MemberRemoved, "member_removed")]
    [DataRow(ConsumerGroupChange.GenerationStarted, "generation_started")]
    [DataRow(ConsumerGroupChange.AssignmentDistributed, "assignment_distributed")]
    public void ToJson_ConsumerGroupChangeWithNoExchangeMemberOrAccount_LeavesThoseOut(ConsumerGroupChange change, string written)
    {
        var surlEvent = new ConsumerGroupChangedEvent(null, "kafka", "orders", change, null, 1, null) { Sequence = 9, At = At };

        var json = Written(surlEvent);

        Assert.AreEqual(
            "{\"sequence\":9,\"at\":\"2026-11-06T10:00:00.0000000\\u002B00:00\",\"kind\":\"consumer_group_changed\","
            + $"\"scheme\":\"kafka\",\"change\":\"{written}\",\"group\":\"orders\",\"generation_id\":1}}",
            json);
    }

    [TestMethod]
    public void ToJson_StoreChangeCommitted_WritesCommitted()
    {
        var surlEvent = new StoreChangedEvent(StoreKind.Bus, StoreChange.Committed, "$group/orders/payments", null, 4) { Sequence = 2, At = At };

        var json = Written(surlEvent);

        Assert.AreEqual(
            "{\"sequence\":2,\"at\":\"2026-11-06T10:00:00.0000000\\u002B00:00\",\"kind\":\"store_changed\",\"exchange_id\":4,"
            + "\"store\":\"bus\",\"change\":\"committed\",\"name\":\"$group/orders/payments\"}",
            json);
    }

    private static readonly DateTimeOffset At = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static string Written(SurlEvent surlEvent) =>
        JsonSerializer.Serialize(ControlEventWriter.ToJson(surlEvent), ControlJsonContext.Default.ControlEventJson);

    private sealed record UnitTestOtherEvent : SurlEvent;
}
