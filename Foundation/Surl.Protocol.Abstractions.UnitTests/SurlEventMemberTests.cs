using System.Net;
using System.Reflection;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class SurlEventMemberTests
{
    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50000);

    [TestMethod]
    public void WriteMembers_EveryEventRecord_WritesEachPropertyItDeclares()
    {
        var gaps = new List<string>();
        foreach (var (type, surlEvent) in OneOfEachEvent())
        {
            var writer = new RecordingSurlEventMemberWriter();
            surlEvent.WriteMembers(writer);
            var written = writer.Members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
            gaps.AddRange(DeclaredPublicProperties(type).Where(name => !written.Contains(name)).Select(name => $"{type.Name}.{name}"));
        }

        Assert.IsEmpty(gaps, $"Not written through WriteMembers: {string.Join(", ", gaps)}");
    }

    [TestMethod]
    public void WriteMembers_StoreChangedDeliveredFor_WritesTheListOrNull()
    {
        var with = new RecordingSurlEventMemberWriter();
        var without = new RecordingSurlEventMemberWriter();

        new StoreChangedEvent(StoreKind.Mail, StoreChange.Written, "INBOX", 1, 2) { DeliveredFor = ["a@x", "b@x"] }.WriteMembers(with);
        new StoreChangedEvent(StoreKind.Mail, StoreChange.Written, "INBOX", 1, 2).WriteMembers(without);

        Assert.AreEqual("a@x|b@x", with.ValueOf("DeliveredFor"));
        Assert.IsNull(without.ValueOf("DeliveredFor"));
        CollectionAssert.AreEqual(
            new[] { "Store", "Change", "Name", "ByteCount", "ExchangeId", "PreviousName", "DeliveredFor" },
            without.Members.Select(member => member.Name).ToArray());
    }

    [TestMethod]
    public void SurlEvent_EveryEventRecordDeclaringExchangeIdSchemeOrAccount_OverridesTheBaseMember()
    {
        var shadows = new List<string>();
        foreach (var type in EventRecordTypes())
        {
            foreach (var name in new[] { nameof(SurlEvent.ExchangeId), nameof(SurlEvent.Scheme), nameof(SurlEvent.Account) })
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetMethod!.GetBaseDefinition().DeclaringType != typeof(SurlEvent))
                {
                    shadows.Add($"{type.Name}.{name}");
                }
            }
        }

        Assert.IsEmpty(shadows, $"Shadow the base member: {string.Join(", ", shadows)}");
    }

    [TestMethod]
    public void SurlEvent_EveryEventRecord_IsListedHereSoTheGuardsSeeIt()
    {
        var listed = OneOfEachEvent().Select(pair => pair.Type).ToArray();

        CollectionAssert.AreEquivalent(EventRecordTypes().ToArray(), listed);
    }

    [TestMethod]
    public void KindName_EachRecord_IsItsTypeNameWithoutEventInSnakeCase()
    {
        var names = OneOfEachEvent().Select(pair => pair.Event.KindName).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "consumer_group_changed", "cookie_received", "exchange_bytes", "exchange_ended", "exchange_started",
                "fault_applied", "hook_failed", "login_judged", "store_changed",
            },
            names);
    }

    [TestMethod]
    public void KindName_TypeWithoutTheEventSuffix_KeepsItsWholeName()
    {
        Assert.AreEqual("plain_notice", new PlainNotice().KindName);
    }

    [TestMethod]
    public void Base_EventWithNoFacts_HasNullExchangeSchemeAndAccountAndWritesNothing()
    {
        SurlEvent bare = new PlainNotice();
        var writer = new RecordingSurlEventMemberWriter();

        bare.WriteMembers(writer);

        Assert.IsNull(bare.ExchangeId);
        Assert.IsNull(bare.Scheme);
        Assert.IsNull(bare.Account);
        Assert.IsEmpty(writer.Members);
    }

    [TestMethod]
    public void Facts_ExchangeStarted_SaysItsExchangeAndScheme()
    {
        SurlEvent started = new ExchangeStartedEvent(4, "http://127.0.0.1:8080", "http", Client);
        var writer = new RecordingSurlEventMemberWriter();

        started.WriteMembers(writer);

        Assert.AreEqual(4, started.ExchangeId);
        Assert.AreEqual("http", started.Scheme);
        Assert.IsNull(started.Account);
        Assert.AreEqual("127.0.0.1:50000", writer.ValueOf("Client"));
        Assert.AreEqual("http://127.0.0.1:8080", writer.ValueOf("ListenUrl"));
    }

    [TestMethod]
    public void Facts_LoginJudgedAndConsumerGroupChanged_SayTheirAccount()
    {
        SurlEvent login = new LoginJudgedEvent(3, "imap", "alice", "PLAIN", true, false);
        SurlEvent group = new ConsumerGroupChangedEvent(null, "kafka", "g", ConsumerGroupChange.MemberJoined, null, 1, "bob");

        Assert.AreEqual("alice", login.Account);
        Assert.AreEqual("imap", login.Scheme);
        Assert.AreEqual(3, login.ExchangeId);
        Assert.AreEqual("bob", group.Account);
        Assert.AreEqual("kafka", group.Scheme);
        Assert.IsNull(group.ExchangeId);
    }

    [TestMethod]
    public void Facts_StoreChanged_SaysItsNameAsTheAccountOnlyForTheAccountBook()
    {
        SurlEvent accounts = new StoreChangedEvent(StoreKind.Accounts, StoreChange.Created, "carol", null, 9);
        SurlEvent content = new StoreChangedEvent(StoreKind.Content, StoreChange.Created, "/a.txt", 3, null);

        Assert.AreEqual("carol", accounts.Account);
        Assert.AreEqual(9, accounts.ExchangeId);
        Assert.IsNull(content.Account);
        Assert.IsNull(content.ExchangeId);
    }

    [TestMethod]
    public void WriteMembers_ExchangeBytes_WritesDirectionBytesLayerAndStream()
    {
        var bytes = new ExchangeBytesEvent(2, ExchangeDirection.Sent, new byte[] { 0xAB, 0x01 }) { StreamId = 8 };
        var writer = new RecordingSurlEventMemberWriter();

        bytes.WriteMembers(writer);

        Assert.AreEqual("Sent", writer.ValueOf("Direction"));
        Assert.AreEqual("AB01", writer.ValueOf("Bytes"));
        Assert.AreEqual("Wire", writer.ValueOf("Layer"));
        Assert.AreEqual("8", writer.ValueOf("StreamId"));
        Assert.AreEqual(2, bytes.ExchangeId);
    }

    [TestMethod]
    public void WriteMembers_LoginJudged_WritesItsFlags()
    {
        var writer = new RecordingSurlEventMemberWriter();

        new LoginJudgedEvent(1, "ftp", "dave", "password", false, true).WriteMembers(writer);

        Assert.AreEqual("false", writer.ValueOf("IsAccepted"));
        Assert.AreEqual("true", writer.ValueOf("IsRefusedByFault"));
        Assert.AreEqual("password", writer.ValueOf("Mechanism"));
    }

    private static IEnumerable<(Type Type, SurlEvent Event)> OneOfEachEvent()
    {
        yield return (typeof(ConsumerGroupChangedEvent), new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.MemberLeft, "m", 2, "a"));
        yield return (typeof(CookieReceivedEvent), new CookieReceivedEvent(1, "http", "sid", "v"));
        yield return (typeof(ExchangeBytesEvent), new ExchangeBytesEvent(1, ExchangeDirection.Received, new byte[] { 1 }));
        yield return (typeof(ExchangeEndedEvent), new ExchangeEndedEvent(1, 10, 20));
        yield return (typeof(ExchangeStartedEvent), new ExchangeStartedEvent(1, "http://127.0.0.1:1", "http", Client));
        yield return (typeof(FaultAppliedEvent), new FaultAppliedEvent("r", FaultKind.Drop, FaultStage.Command, 1, "t"));
        yield return (typeof(HookFailedEvent), new HookFailedEvent("h", HookFailure.TimedOut, "m"));
        yield return (typeof(LoginJudgedEvent), new LoginJudgedEvent(1, "ftp", "a", "password", true, false));
        yield return (typeof(StoreChangedEvent), new StoreChangedEvent(StoreKind.Mail, StoreChange.Created, "n", 1, 1));
    }

    private static IEnumerable<string> DeclaredPublicProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.Name != "EqualityContract" && !IsDerivedFact(type, property))
            .Select(property => property.Name);

    // A fact computed from other members (StoreChangedEvent.Account is its Name) is written through them.
    private static bool IsDerivedFact(Type type, PropertyInfo property) =>
        property.Name == nameof(SurlEvent.Account)
        && type.GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance) is null;

    private static IEnumerable<Type> EventRecordTypes() =>
        typeof(SurlEvent).Assembly.GetExportedTypes().Where(type => type.IsSubclassOf(typeof(SurlEvent)));

    private sealed record PlainNotice : SurlEvent;
}
