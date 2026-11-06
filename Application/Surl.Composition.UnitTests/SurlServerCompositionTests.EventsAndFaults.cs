using System.Text;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The one event stream and fault plan each start composes (ADR-0124, decisions 1 and 4; BL-803),
/// over <see cref="FakeListenerFactory"/> and <see cref="StartTestSeams"/>, with no network.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    private static readonly string SmtpListenUrl = $"smtp://127.0.0.1:{FakeListenerFactory.BoundPort}/";
    private static readonly string ImapListenUrl = $"imap://127.0.0.1:{FakeListenerFactory.BoundPort}/";

    [TestMethod]
    public async Task ComposeAsync_SmtpAndImap_PublishesBothExchangesAndTheMailDeliveryOnOneSubscription()
    {
        var smtpConnection = new FakeConnection(Encoding.ASCII.GetBytes(
            "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nSubject: shared\r\n\r\nread over imap\r\n.\r\nQUIT\r\n"));
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 SELECT INBOX\r\na2 FETCH 1 BODY[TEXT]\r\na3 LOGOUT\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>>
            {
                ["smtp"] = Task.FromResult(smtpConnection),
                ["imap"] = HandOutAfterAsync(smtpConnection, imapConnection),
            },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", "smtp://127.0.0.1:0/", "imap://127.0.0.1:0/"), seams.Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });

        await ServeUntilEndedAsync(serving, TextWriter.Null, imapConnection);
        var events = Drain(subscription);

        var smtpStarted = events.OfType<ExchangeStartedEvent>().Single(started => started.Scheme == "smtp");
        var imapStarted = events.OfType<ExchangeStartedEvent>().Single(started => started.Scheme == "imap");
        var delivered = events.OfType<StoreChangedEvent>().Single(
            changed => changed.Store == StoreKind.Mail && changed.Change == StoreChange.Delivered);
        Assert.AreEqual(SmtpListenUrl, smtpStarted.ListenUrl);
        Assert.AreEqual(ImapListenUrl, imapStarted.ListenUrl);
        StringAssert.Contains(SentText(events, smtpStarted.ExchangeId), "250 2.0.0 Message accepted\r\n");
        StringAssert.Contains(SentText(events, imapStarted.ExchangeId), "read over imap\r\n");
        Assert.IsLessThan(delivered.Sequence, smtpStarted.Sequence);
        Assert.IsLessThan(imapStarted.Sequence, delivered.Sequence);
        Assert.IsTrue(events.OfType<ExchangeEndedEvent>().Any(ended => ended.ExchangeId == imapStarted.ExchangeId));
        Assert.AreEqual(0, subscription.DroppedEventCount);
    }

    [TestMethod]
    public async Task ComposeAsync_TwoStarts_GiveTwoStreamsThatNeverSeeEachOthersEvents()
    {
        var smtpConnection = new FakeConnection(Encoding.ASCII.GetBytes("EHLO c\r\nQUIT\r\n"));
        var servedSeams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["smtp"] = Task.FromResult(smtpConnection) },
        });
        var commandLine = StartTestSeams.Parse("smtp://127.0.0.1:0/");
        var first = await SurlServerComposition.ComposeAsync(commandLine, servedSeams.Seams, TestContext.CancellationToken);
        var second = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var firstServing = first.Serving!;
        await using var secondServing = second.Serving!;
        await using var firstSubscription = firstServing.Events.Subscribe(new SurlEventSubscriptionOptions());
        await using var secondSubscription = secondServing.Events.Subscribe(new SurlEventSubscriptionOptions());

        await ServeUntilEndedAsync(firstServing, TextWriter.Null, smtpConnection);

        Assert.AreNotSame(firstServing.Events, secondServing.Events);
        Assert.AreNotSame(firstServing.Faults, secondServing.Faults);
        Assert.IsTrue(Drain(firstSubscription).OfType<ExchangeStartedEvent>().Any());
        Assert.IsEmpty(Drain(secondSubscription));
        Assert.AreEqual(0, secondServing.Events.LastSequence);
    }

    [TestMethod]
    public async Task ComposeAsync_ExtensionsOn_HandsTheExtendedServersTheInstancesStreamAndPlan()
    {
        ExtendedProtocolServerServices? handed = null;
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                handed = services;
                return [];
            },
        };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--fault", "refuse-login;account=alice", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.AreSame(serving.Events, handed!.Events);
        Assert.AreSame(serving.Faults, handed.Faults);
        Assert.AreSame(serving.Events, handed.ComposedServices.Events);
    }

    [TestMethod]
    public void ComposeUnservedExtendedServices_GivesNullObjects()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        Assert.AreSame(DiscardingSurlEventPublisher.Instance, services.Events);
        Assert.AreSame(EmptyFaultPlan.Instance, services.Faults);
    }

    [TestMethod]
    public async Task ComposeAsync_FaultRules_ReachThePlanInCommandLineOrder()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(
                "--fault", "refuse-login;account=alice",
                "--fault", "delay;ms=500;stage=command;path=/slow",
                "--fault", "drop;listener=smtp://127.0.0.1:2525;times=1",
                "http://127.0.0.1:0"),
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var rules = serving.Faults.Rules;

        CollectionAssert.AreEqual(new[] { "f1", "f2", "f3" }, rules.Select(rule => rule.Id).ToArray());
        CollectionAssert.AreEqual(
            new[] { FaultKind.RefuseLogin, FaultKind.Delay, FaultKind.Drop }, rules.Select(rule => rule.Kind).ToArray());
        Assert.AreEqual("alice", rules[0].Account);
        Assert.AreEqual(500, rules[1].DelayMilliseconds);
        Assert.AreEqual("/slow", rules[1].Path);
        Assert.AreEqual(FaultStage.Command, rules[1].Stage);
        Assert.AreEqual("smtp://127.0.0.1:2525", rules[2].Listener);
        Assert.AreEqual(1, rules[2].Times);
    }

    [TestMethod]
    public async Task ComposeAsync_ADropRuleOnOneListenUrl_DropsItsConnectionUnservedAndServesTheOther()
    {
        var smtpConnection = new FakeConnection(Encoding.ASCII.GetBytes("EHLO c\r\nQUIT\r\n"));
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 LOGOUT\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>>
            {
                ["smtp"] = Task.FromResult(smtpConnection),
                ["imap"] = HandOutAfterAsync(smtpConnection, imapConnection),
            },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--fault", "drop;listener=smtp://127.0.0.1:0", "smtp://127.0.0.1:0/", "imap://127.0.0.1:0/"),
            seams.Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await ServeUntilEndedAsync(serving, TextWriter.Null, imapConnection);
        var events = Drain(subscription);

        Assert.IsTrue(smtpConnection.Aborted);
        Assert.IsEmpty(smtpConnection.WrittenBytes);
        var applied = events.OfType<FaultAppliedEvent>().Single();
        Assert.AreEqual("f1", applied.RuleId);
        Assert.AreEqual(FaultKind.Drop, applied.Kind);
        Assert.AreEqual(SmtpListenUrl, applied.Target);
        Assert.IsFalse(imapConnection.Aborted);
        StringAssert.Contains(Encoding.ASCII.GetString(imapConnection.WrittenBytes), "a1 OK");
    }

    [TestMethod]
    public async Task ComposeAsync_ARefuseLoginRule_RefusesThatAccountsLoginAndPublishesItAsRefusedByFault()
    {
        var (written, events) = await LogInOverImapAsync("--fault", "refuse-login;account=alice");

        StringAssert.Contains(written, "a1 NO");
        Assert.DoesNotContain("a1 OK", written);
        var judged = events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("alice", judged.Account);
        Assert.IsFalse(judged.IsAccepted);
        Assert.IsTrue(judged.IsRefusedByFault);
    }

    [TestMethod]
    public async Task ComposeAsync_NoFaultRule_AcceptsTheSameLoginAndAppliesNoFault()
    {
        var (written, events) = await LogInOverImapAsync();

        StringAssert.Contains(written, "a1 OK");
        var judged = events.OfType<LoginJudgedEvent>().Single();
        Assert.IsTrue(judged.IsAccepted);
        Assert.IsFalse(judged.IsRefusedByFault);
        Assert.IsFalse(events.OfType<FaultAppliedEvent>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_FaultRules_WriteTheWarningOnceBeforeTheStatusLines()
    {
        using var log = new StringWriter();
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--fault", "refuse-login;account=alice", "--fault", "out-of-space;path=/uploads", "http://127.0.0.1:0"),
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        await ServeUntilBoundAsync(serving, log);

        Assert.AreEqual(
            $"surl: warning: --fault: 2 fault rule(s) make this server misbehave on purpose{Environment.NewLine}"
            + $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_FaultRulesWithSilent_WriteNoWarning()
    {
        using var log = new StringWriter();
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("-s", "--fault", "refuse-login;account=alice", "http://127.0.0.1:0"),
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        await ServeUntilBoundAsync(serving, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_NoFaultRule_WritesNoWarningAndHoldsAnEmptyPlan()
    {
        using var log = new StringWriter();
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("http://127.0.0.1:0"), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        await ServeUntilBoundAsync(serving, log);

        Assert.AreEqual($"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}", log.ToString());
        Assert.IsEmpty(serving.Faults.Rules);
    }

    private static async Task<FakeConnection> HandOutAfterAsync(FakeConnection first, FakeConnection then)
    {
        await first.Disposed.Task;
        return then;
    }

    private static List<SurlEvent> Drain(SurlEventSubscription subscription)
    {
        List<SurlEvent> events = [];
        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return events;
    }

    private static string SentText(IEnumerable<SurlEvent> events, long exchangeId) =>
        string.Concat(events
            .OfType<ExchangeBytesEvent>()
            .Where(bytes => bytes.ExchangeId == exchangeId && bytes.Direction == ExchangeDirection.Sent)
            .Select(bytes => Encoding.ASCII.GetString(bytes.Bytes.Span)));

    // Logs alice in over IMAP with LOGIN, on a start with the given options added, and returns what
    // the server wrote and every event published.
    private async Task<(string Written, List<SurlEvent> Events)> LogInOverImapAsync(params string[] options)
    {
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 LOGIN alice secret\r\na2 LOGOUT\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["imap"] = Task.FromResult(imapConnection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "-u", "alice:secret", "--allow-plaintext-auth", "imap://127.0.0.1:0/"]),
            seams.Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await ServeUntilEndedAsync(serving, TextWriter.Null, imapConnection);

        return (Encoding.ASCII.GetString(imapConnection.WrittenBytes), Drain(subscription));
    }

    // Serves until the last connection has ended, then stops.
    private async Task ServeUntilEndedAsync(ComposedServing serving, TextWriter log, FakeConnection last)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log), stop.Token);
        await last.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        Assert.AreEqual(new SurlServingOutcome(SurlExitCode.Ok, null), await serve);
    }

    // Serves until every listener has bound, then stops.
    private async Task ServeUntilBoundAsync(ComposedServing serving, TextWriter log)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
    }
}
