using System.Formats.Asn1;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The chosen-error fault (ADR-0124 decision 4, ADR-0139): a search, compare or write a matching
/// <see cref="FaultKind.Error"/> rule names is answered with the rule's resultCode and diagnostic
/// message in place of reading the directory, after the bind check, and publishes one
/// <see cref="FaultAppliedEvent"/>. The recorded case is the bind and base search pinned upstream
/// curl 8.21.0 sent (Fixtures/README.md); a search answered with any code but success or
/// sizeLimitExceeded ends it with exit 39, as noSuchObject does (UpstreamCurlSearchesSurlOverLdapTests).
/// </summary>
[TestClass]
public sealed class LdapFaultTests
{
    private const string BusyReply = "51 directory busy: fault injected";

    private static readonly byte[] AliceBind = Message(1, SimpleBind(3, "alice", "secret"));
    private static readonly byte[] BobBind = Message(1, SimpleBind(3, "bob", "secret"));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_SearchUnderBaseDcXWithARule_AnswersTheRulesResultCodeAndNoEntry()
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var plan = new ScriptedFaultPlan(Rule("search", "dc=x"));

        var written = await ServeAsync(plan, events, log, AliceBind, Message(2, Search(Present("objectClass"), baseObject: "dc=x")));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 searchResultDone busy \"directory busy: fault injected\"" },
            LdapResponseTranscript.Of(written).ToArray());
        AssertOneFaultApplied(events, "search dc=x");
        CollectionAssert.Contains(log.Notes.ToArray(), "LDAP search dc=x answered by fault rule f1: busy");
        var point = plan.Points.Single();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("ldap://127.0.0.1:18389/", point.ListenUrl);
        Assert.AreEqual("ldap", point.Scheme);
        Assert.AreEqual("alice", point.Account);
        Assert.AreEqual("dc=x", point.Path);
        Assert.AreEqual("search", point.Command);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedCurlSearchUnderARule_AnswersTheRulesResultCodeWithNoEntry()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule("search", "dc=example,dc=com"));

        var written = await ServeAsync(plan, events, null, [.. RecordedFixture.ReadRequestMessages("simple-bind-base-search")]);

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 searchResultDone busy \"directory busy: fault injected\"" },
            LdapResponseTranscript.Of(written).ToArray());
        AssertOneFaultApplied(events, "search dc=example,dc=com");
    }

    [TestMethod]
    public async Task ServeAsync_SearchUnderAnotherBase_IsAnsweredFromTheDirectory()
    {
        var events = new RecordingSurlEventPublisher();

        var written = await ServeAsync(
            new ScriptedFaultPlan(Rule("search", "dc=x")), events, null, AliceBind, Message(2, Search(Present("objectClass"), scope: 0)));

        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(written)[^1]);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_RootDseSearchUnderARule_AnswersTheRulesResultCode()
    {
        var events = new RecordingSurlEventPublisher();

        var written = await ServeAsync(
            new ScriptedFaultPlan(Rule("search", string.Empty)), events, null, Message(2, Search(Present("objectClass"), baseObject: string.Empty, scope: 0)));

        Assert.AreEqual("#2 searchResultDone busy \"directory busy: fault injected\"", LdapResponseTranscript.Of(written).Single());
        AssertOneFaultApplied(events, "search ");
    }

    [TestMethod]
    public async Task ServeAsync_SearchBeforeABindUnderARule_IsRefusedBindFirstAndAsksThePlanNothing()
    {
        var plan = new ScriptedFaultPlan(Rule("search", "dc=x"));

        var written = await ServeAsync(plan, new RecordingSurlEventPublisher(), null, Message(2, Search(Present("objectClass"), baseObject: "dc=x")));

        Assert.AreEqual("#2 searchResultDone insufficientAccessRights \"bind first\"", LdapResponseTranscript.Of(written).Single());
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task ServeAsync_ARuleScopedToAlice_FiresForAliceOnly()
    {
        var rule = Rule("search", "dc=x") with { Account = "alice" };
        var search = Message(2, Search(Present("objectClass"), baseObject: "dc=x"));

        var aliceEvents = new RecordingSurlEventPublisher();
        var aliceWritten = await ServeAsync(new ScriptedFaultPlan(rule), aliceEvents, null, AliceBind, search);
        var bobEvents = new RecordingSurlEventPublisher();
        var bobWritten = await ServeAsync(new ScriptedFaultPlan(rule), bobEvents, null, BobBind, search);

        Assert.AreEqual("#2 searchResultDone busy \"directory busy: fault injected\"", LdapResponseTranscript.Of(aliceWritten)[^1]);
        AssertOneFaultApplied(aliceEvents, "search dc=x");
        Assert.StartsWith("#2 searchResultDone noSuchObject", LdapResponseTranscript.Of(bobWritten)[^1]);
        Assert.IsEmpty(bobEvents.Events);
    }

    [TestMethod]
    public async Task ServeAsync_CompareUnderARule_AnswersTheRulesResultCode()
    {
        var events = new RecordingSurlEventPublisher();
        var compare = Message(
            3,
            writer =>
            {
                using var request = writer.PushSequence(LdapTags.CompareRequest);
                Text(writer, "cn=alice,dc=example,dc=com");
                using var assertion = writer.PushSequence();
                Text(writer, "sn");
                Text(writer, "Smith");
            });

        var written = await ServeAsync(
            new ScriptedFaultPlan(Rule("compare", "cn=alice,dc=example,dc=com") with { Reply = "53 unwilling" }), events, null, AliceBind, compare);

        Assert.AreEqual("#3 compareResponse unwillingToPerform \"unwilling\"", LdapResponseTranscript.Of(written)[^1]);
        AssertOneFaultApplied(events, "compare cn=alice,dc=example,dc=com");
    }

    [TestMethod]
    public async Task ServeAsync_AddUnderARule_AnswersTheRulesResultCodeInPlaceOfTheReadOnlyRefusal()
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var add = Message(4, writer =>
        {
            using var request = writer.PushSequence(new Asn1Tag(TagClass.Application, 8, isConstructed: true));
            Text(writer, "cn=carol,dc=example,dc=com");
        });

        var written = await ServeAsync(new ScriptedFaultPlan(Rule("add", null) with { Reply = "68 entry exists" }), events, log, AliceBind, add);

        Assert.AreEqual("#4 addResponse entryAlreadyExists \"entry exists\"", LdapResponseTranscript.Of(written)[^1]);
        AssertOneFaultApplied(events, "add");
        Assert.IsFalse(log.Notes.Any(note => note.Contains("read-only", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("0 success", DisplayName = "Success is not an error")]
    [DataRow("81 past the range", DisplayName = "Past 80")]
    [DataRow("10 referral", DisplayName = "Referral")]
    [DataRow("14 bind in progress", DisplayName = "saslBindInProgress")]
    [DataRow("51", DisplayName = "No diagnostic text")]
    [DataRow("51 ", DisplayName = "Empty diagnostic text")]
    [DataRow(" busy", DisplayName = "No code")]
    [DataRow("busy now", DisplayName = "A word for a code")]
    [DataRow(null, DisplayName = "No reply")]
    public async Task ServeAsync_ARuleWhoseReplyIsNotAnLdapResult_IsNotedAndNotApplied(string? reply)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(
            new ScriptedFaultPlan(Rule("search", "dc=example,dc=com") with { Reply = reply }), events, log, AliceBind, Message(2, Search(Present("objectClass"), scope: 0)));

        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(written)[^1]);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(
            log.Notes.ToArray(), "LDAP search dc=example,dc=com: fault rule f1 is not an error with an LDAP resultCode and diagnostic text; not applied.");
    }

    // ADR-0141: a command-stage delay holds the search's response on the exchange's clock, with
    // the idle clock paused for the whole hold, then answers it from the directory as usual.
    [TestMethod]
    public async Task ServeAsync_SearchDelayRule_HoldsTheResponseUntilTheClockAdvances500Ms_WithTheIdleClockPaused()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "search", DelayMilliseconds = 500 };
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var (serving, connection, events) = StartServing(
            new ScriptedFaultPlan(delay), clock, idleClock, AliceBind, Message(2, Search(Present("objectClass"), scope: 0)));

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var writtenAtTheHold = LdapResponseTranscript.Of(connection.WrittenBytes).ToArray();
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var writtenAfter499Ms = LdapResponseTranscript.Of(connection.WrittenBytes).ToArray();
        var pausedAfter499Ms = idleClock.IsPaused;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, writtenAtTheHold);
        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, writtenAfter499Ms);
        Assert.IsTrue(pausedAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "search dc=example,dc=com"), events.Events.Single());
    }

    // ADR-0141: a command-stage drop aborts the connection in place of the search's response.
    [TestMethod]
    public async Task ServeAsync_SearchDropRule_AbortsTheConnectionWithNoResponse()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "search" };
        var (serving, connection, events) = StartServing(
            new ScriptedFaultPlan(drop), new ManualTimeProvider(), new RecordingIdleClock(), [.. RecordedFixture.ReadRequestMessages("simple-bind-base-search")]);

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "search dc=example,dc=com"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_CompareDropRule_AbortsTheConnectionWithNoResponse()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "compare" };
        var compare = Message(
            3,
            writer =>
            {
                using var request = writer.PushSequence(LdapTags.CompareRequest);
                Text(writer, "cn=alice,dc=example,dc=com");
                using var assertion = writer.PushSequence();
                Text(writer, "sn");
                Text(writer, "Smith");
            });
        var (serving, connection, events) = StartServing(new ScriptedFaultPlan(drop), new ManualTimeProvider(), new RecordingIdleClock(), AliceBind, compare);

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    public async Task ServeAsync_AddDropRule_AbortsTheConnectionWithNoResponse()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "add" };
        var add = Message(4, writer =>
        {
            using var request = writer.PushSequence(new Asn1Tag(TagClass.Application, 8, isConstructed: true));
            Text(writer, "cn=carol,dc=example,dc=com");
        });
        var (serving, connection, events) = StartServing(new ScriptedFaultPlan(drop), new ManualTimeProvider(), new RecordingIdleClock(), AliceBind, add);

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "add"), events.Events.Single());
    }

    private (Task Serving, InMemoryConnection Connection, RecordingSurlEventPublisher Events) StartServing(
        IFaultPlan plan, ManualTimeProvider clock, RecordingIdleClock idleClock, params byte[][] messages)
    {
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(messages.Select(message => new ReadOnlyMemory<byte>(message)));
        var context = Context(TestContext.CancellationToken, timeProvider: clock) with { Faults = plan, Events = events, IdleClock = idleClock };

        return (Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, context), connection, events);
    }

    [TestMethod]
    public async Task ServeAsync_AnOutOfSpaceRuleAtTheCommandStage_IsNotedAndNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Command = "search" });

        var written = await ServeAsync(plan, events, log, AliceBind, Message(2, Search(Present("objectClass"), scope: 0)));

        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(written)[^1]);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(
            log.Notes.ToArray(), "LDAP search dc=example,dc=com: fault rule f1 is not an error with an LDAP resultCode and diagnostic text; not applied.");
    }

    private static FaultRule Rule(string operation, string? baseDn) =>
        new("f1", FaultKind.Error) { Scheme = "ldap", Command = operation, Path = baseDn, Reply = BusyReply };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    private async Task<byte[]> ServeAsync(IFaultPlan plan, ISurlEventPublisher events, RecordingExchangeLog? log, params byte[][] messages)
    {
        var connection = new InMemoryConnection(messages.Select(message => new ReadOnlyMemory<byte>(message)));
        var context = Context(TestContext.CancellationToken, log: log) with { Faults = plan, Events = events };

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, context);

        return connection.WrittenBytes;
    }
}
