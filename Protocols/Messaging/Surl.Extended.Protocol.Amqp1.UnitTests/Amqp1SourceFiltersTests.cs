using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The source filters the container applies (AMQP 1.0 Part 3 sections 3.5.1 and 3.5.8; the Apache filter
/// registry's descriptors; ADR-0146 decision 2.1), each written from the registry and AMQP 0-9-1 section
/// 3.1.3's direct, topic and headers exchanges.
/// </summary>
[TestClass]
public sealed class Amqp1SourceFiltersTests
{
    [TestMethod]
    [DataRow(0x0000468C_00000000ul, "apache.org:legacy-amqp-direct-binding:string")]
    [DataRow(0x0000468C_00000001ul, "apache.org:legacy-amqp-topic-binding:string")]
    [DataRow(0x0000468C_00000002ul, "apache.org:legacy-amqp-headers-binding:map")]
    [DataRow(0x0000468C_00000003ul, "apache.org:no-local-filter:list")]
    [DataRow(0x0000468C_00000004ul, "apache.org:selector-filter:string")]
    public void CodeOf_UlongAndSymbolForms_NameTheSameFilter(ulong code, string name)
    {
        Assert.AreEqual(code, Amqp1SourceFilters.CodeOf(code));
        Assert.AreEqual(code, Amqp1SourceFilters.CodeOf(new Amqp1Symbol(name)));
    }

    [TestMethod]
    public void CodeOf_UnknownSymbolOrOtherType_IsNull()
    {
        Assert.IsNull(Amqp1SourceFilters.CodeOf(new Amqp1Symbol("apache.org:xquery-filter:string")));
        Assert.IsNull(Amqp1SourceFilters.CodeOf("apache.org:selector-filter:string"));
    }

    [TestMethod]
    public void Read_NoFilterMap_AppliesNothing()
    {
        var sources = new object?[]
        {
            null,
            "orders",
            new Amqp1Described(Amqp1Descriptor.Source, "orders"),
            new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { "orders" }),
            new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { "orders", null, null, null, null, null, null, "not a map" }),
        };

        foreach (var source in sources)
        {
            var filters = Amqp1SourceFilters.Read(source, isTopic: true)!;

            Assert.IsNull(filters.Applied);
            Assert.IsFalse(filters.NoLocal);
            Assert.IsNull(filters.QueuePredicate("orders"));
            Assert.IsTrue(filters.Accepts(Message("orders"), "orders"));
        }
    }

    [TestMethod]
    public void Read_EntriesNotApplied_AreLeftOut()
    {
        var filters = Read(
            isTopic: false,
            ("plain", "not described"),
            ("unknown", new Amqp1Described(new Amqp1Symbol("apache.org:xquery-filter:string"), "/a")),
            ("no-local", Filter(Amqp1SourceFilters.NoLocalFilter, new List<object?>())),
            ("direct", Filter(Amqp1SourceFilters.DirectBinding, 5L)),
            ("topic", Filter(Amqp1SourceFilters.TopicBinding, 5L)),
            ("bad-topic", Filter(Amqp1SourceFilters.TopicBinding, "a..b")),
            ("headers", Filter(Amqp1SourceFilters.HeadersBinding, "not a map")),
            ("bad-match", Filter(Amqp1SourceFilters.HeadersBinding, Arguments(("x-match", "some")))),
            ("int-match", Filter(Amqp1SourceFilters.HeadersBinding, Arguments(("x-match", 1L)))));

        Assert.IsNull(filters.Applied);
        Assert.IsFalse(filters.NoLocal);
    }

    [TestMethod]
    public void Read_NoLocalOnATopic_IsAppliedAndLetsEveryMessageThrough()
    {
        var entry = Filter(new Amqp1Symbol("apache.org:no-local-filter:list"), new List<object?>());

        var filters = Read(isTopic: true, ("nl", entry));

        Assert.IsTrue(filters.NoLocal);
        Assert.AreEqual(entry, filters.Applied!.Entries.Single().Value);
        Assert.IsTrue(filters.Accepts(Message("news"), null));
    }

    [TestMethod]
    [DataRow("color = ", DisplayName = "malformed")]
    [DataRow(null, DisplayName = "not a string")]
    public void Read_RefusedSelector_IsNull(string? selector)
    {
        var source = Source(("s", Filter(Amqp1SourceFilters.SelectorFilter, (object?)selector ?? 7L)));

        Assert.IsNull(Amqp1SourceFilters.Read(source, isTopic: false));
    }

    [TestMethod]
    public void Accepts_Selector_SelectsByTheMessagesProperties()
    {
        var filters = Read(isTopic: false, ("s", Filter(Amqp1SourceFilters.SelectorFilter, "color = 'red'")));

        Assert.IsTrue(filters.Accepts(Message("news", ("color", "red")), null));
        Assert.IsFalse(filters.Accepts(Message("news", ("color", "blue")), null));
        Assert.IsFalse(filters.QueuePredicate("orders")!(Message("orders")));
    }

    [TestMethod]
    public void Accepts_SeveralFilters_MustAllAccept()
    {
        var filters = Read(
            isTopic: true,
            ("s", Filter(Amqp1SourceFilters.SelectorFilter, "color = 'red'")),
            ("d", Filter(Amqp1SourceFilters.DirectBinding, "news")));

        Assert.HasCount(2, filters.Applied!.Entries);
        Assert.IsTrue(filters.Accepts(Message("news", ("color", "red")), null));
        Assert.IsFalse(filters.Accepts(Message("sport", ("color", "red")), null));
        Assert.IsFalse(filters.Accepts(Message("news", ("color", "blue")), null));
    }

    [TestMethod]
    public void Accepts_DirectBinding_MatchesTheSubjectElseTheTopicAsARoutingKey()
    {
        var filters = Read(isTopic: false, ("d", Filter(Amqp1SourceFilters.DirectBinding, "sensors.temp")));

        Assert.IsTrue(filters.Accepts(Message("sensors/temp"), null));
        Assert.IsTrue(filters.Accepts(WithSubject("orders", "sensors.temp"), "orders"));
        Assert.IsFalse(filters.Accepts(WithSubject("sensors/temp", "other"), null));
    }

    [TestMethod]
    public void Accepts_MessageSentStraightToTheQueueWithoutSubject_HasNoRoutingKey()
    {
        var direct = Read(isTopic: false, ("d", Filter(Amqp1SourceFilters.DirectBinding, "orders")));
        var topic = Read(isTopic: false, ("t", Filter(Amqp1SourceFilters.TopicBinding, "#")));

        Assert.IsFalse(direct.Accepts(Message("orders"), "orders"));
        Assert.IsFalse(topic.Accepts(Message("orders"), "orders"));
        Assert.IsTrue(direct.Accepts(Message("orders"), "other-queue"));
    }

    [TestMethod]
    [DataRow("a.*.c", "a.b.c", true)]
    [DataRow("a.*.c", "a.b.x.c", false)]
    [DataRow("a.#.c", "a.b.x.c", true)]
    [DataRow("a.#.c", "a.c", true)]
    [DataRow("a.#", "a", true)]
    [DataRow("#", "a.b", true)]
    [DataRow("#.#", "a", true)]
    [DataRow("*", "a.b", false)]
    [DataRow("a.b", "a", false)]
    [DataRow("a", "b", false)]
    public void Accepts_TopicBinding_MatchesAsAnAmqp091TopicPattern(string pattern, string routingKey, bool matches)
    {
        var filters = Read(isTopic: true, ("t", Filter(Amqp1SourceFilters.TopicBinding, pattern)));

        Assert.AreEqual(matches, filters.Accepts(WithSubject("news", routingKey), null));
    }

    [TestMethod]
    public void Accepts_TopicBindingAndATopicWithNoRoutingKey_DoesNotMatch()
    {
        var filters = Read(isTopic: true, ("t", Filter(Amqp1SourceFilters.TopicBinding, "#")));

        // A level holding "." cannot be written as an AMQP 0-9-1 word.
        Assert.IsFalse(filters.Accepts(Message("a.b/c"), null));
    }

    [TestMethod]
    public void Accepts_HeadersBindingAll_MatchesEveryEntryButReservedOnes()
    {
        var filters = Read(isTopic: true, ("h", Filter(Amqp1SourceFilters.HeadersBinding, Arguments(("color", "red"), ("size", null), ("x-other", "ignored"), (5L, "ignored")))));

        Assert.IsTrue(filters.Accepts(Message("news", ("color", "red"), ("size", "big")), null));
        Assert.IsFalse(filters.Accepts(Message("news", ("color", "red")), null));
        Assert.IsFalse(filters.Accepts(Message("news", ("color", "blue"), ("size", "big")), null));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "x-match a string")]
    [DataRow(false, DisplayName = "x-match a symbol")]
    public void Accepts_HeadersBindingAny_MatchesOneEntry(bool asString)
    {
        object match = asString ? "any" : new Amqp1Symbol("any");
        var filters = Read(isTopic: true, ("h", Filter(Amqp1SourceFilters.HeadersBinding, Arguments(("x-match", match), ("color", "red"), ("size", "big")))));

        Assert.IsTrue(filters.Accepts(Message("news", ("color", "red")), null));
        Assert.IsFalse(filters.Accepts(Message("news", ("color", "blue")), null));
    }

    [TestMethod]
    public void Accepts_HeadersBindingAllAsASymbol_ComparesBinaryAndOtherValues()
    {
        var native = Amqp1Encoder.Encode(new Amqp1Described(
            Amqp1Descriptor.ApplicationProperties,
            new Amqp1Map([new("id", new byte[] { 1, 2 }), new("size", 3L), new("tag", "t")])));
        var message = new BusMessage("news", ReadOnlyMemory<byte>.Empty) { NativeForm = new("amqp1", native) };

        Assert.IsTrue(Headers(("x-match", new Amqp1Symbol("all")), ("id", new byte[] { 1, 2 }), ("size", 3L)).Accepts(message, null));
        Assert.IsFalse(Headers(("id", new byte[] { 1, 3 })).Accepts(message, null));
        Assert.IsFalse(Headers(("id", 1L)).Accepts(message, null));
        Assert.IsFalse(Headers(("size", new byte[] { 3 })).Accepts(message, null));
        Assert.IsFalse(Headers(("tag", new byte[] { 0x74 })).Accepts(message, null));
    }

    private static Amqp1SourceFilters Headers(params (object Key, object? Value)[] arguments) =>
        Read(isTopic: true, ("h", Filter(Amqp1SourceFilters.HeadersBinding, Arguments(arguments))));

    private static Amqp1SourceFilters Read(bool isTopic, params (string Key, object? Value)[] entries) =>
        Amqp1SourceFilters.Read(Source(entries), isTopic)!;

    private static Amqp1Described Source(params (string Key, object? Value)[] entries) => new(
        Amqp1Descriptor.Source,
        new List<object?> { "node", null, null, null, null, null, null, new Amqp1Map([.. entries.Select(entry => new KeyValuePair<object?, object?>(new Amqp1Symbol(entry.Key), entry.Value))]) });

    private static Amqp1Described Filter(object descriptor, object? value) => new(descriptor, value);

    private static Amqp1Map Arguments(params (object Key, object? Value)[] entries) =>
        new([.. entries.Select(entry => new KeyValuePair<object?, object?>(entry.Key, entry.Value))]);

    private static BusMessage Message(string topic, params (string Name, string Value)[] headers) =>
        new(topic, Encoding.UTF8.GetBytes("body")) { Headers = [.. headers.Select(header => new BusMessageHeader(header.Name, header.Value))] };

    private static BusMessage WithSubject(string topic, string subject) =>
        new(topic, ReadOnlyMemory<byte>.Empty) { NativeForm = new("amqp1", Amqp1Encoder.Encode(new Amqp1Properties { Subject = subject }.ToDescribed())) };
}
