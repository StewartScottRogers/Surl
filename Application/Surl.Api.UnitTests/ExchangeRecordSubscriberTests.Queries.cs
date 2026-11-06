using System.Net;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

public sealed partial class ExchangeRecordSubscriberTests
{
    private const string SmtpListenUrl = "smtp://127.0.0.1:49731/";
    private static readonly IPEndPoint Bob = new(IPAddress.Loopback, 50002);
    private static readonly SurlEventQuery MailForBob = new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Account = "bob" };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FindExchanges_ByScheme_ReturnsExactlyTheExchangesOnThatScheme()
    {
        var (stream, _) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        stream.Publish(new ExchangeStartedEvent(2, SmtpListenUrl, "smtp", Bob));

        var smtp = subscriber.FindExchanges(new SurlEventQuery { Scheme = "smtp" });

        Assert.AreEqual(2, smtp.Single().ExchangeId);
    }

    [TestMethod]
    public async Task FindExchanges_ByListenUrl_ReturnsExactlyTheExchangesItAccepted()
    {
        var (stream, _) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        stream.Publish(new ExchangeStartedEvent(2, "http://127.0.0.1:49732/", "http", Bob));

        var found = subscriber.FindExchanges(new SurlEventQuery { ListenUrl = new Uri("http://127.0.0.1:49732/") });

        Assert.AreEqual(2, found.Single().ExchangeId);
    }

    [TestMethod]
    public async Task FindExchanges_ByAccount_ReturnsExactlyTheExchangesWhereItsLoginWasJudged()
    {
        var (stream, _) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, SmtpListenUrl, "smtp", Alice));
        stream.Publish(new ExchangeStartedEvent(2, SmtpListenUrl, "smtp", Bob));
        stream.Publish(new LoginJudgedEvent(1, "smtp", "alice", "PLAIN", true, false));
        stream.Publish(new LoginJudgedEvent(2, "smtp", "bob", "PLAIN", false, false));

        var found = subscriber.FindExchanges(new SurlEventQuery { Account = "bob" });

        Assert.AreEqual(2, found.Single().ExchangeId);
    }

    [TestMethod]
    public async Task FindExchanges_ByTimeWindow_ReturnsExactlyTheExchangesStartedInIt()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        clock.Advance(TimeSpan.FromSeconds(10));
        stream.Publish(new ExchangeStartedEvent(2, HttpListenUrl, "http", Alice));
        clock.Advance(TimeSpan.FromSeconds(10));
        stream.Publish(new ExchangeStartedEvent(3, HttpListenUrl, "http", Alice));

        var found = subscriber.FindExchanges(new SurlEventQuery
        {
            Since = ManualTimeProvider.Start.AddSeconds(10),
            Until = ManualTimeProvider.Start.AddSeconds(20),
        });

        Assert.AreEqual(2, found.Single().ExchangeId);
    }

    [TestMethod]
    public async Task FindEvents_ByKindAndStore_ReturnsExactlyTheMatchingEventsWithoutBytes()
    {
        var (stream, _) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        PublishMailForBob(stream);
        stream.Publish(new StoreChangedEvent(StoreKind.Bus, StoreChange.Published, "news", 3, null));

        var mail = subscriber.FindEvents(new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail });
        var all = subscriber.FindEvents(new SurlEventQuery());

        Assert.AreEqual("bob/INBOX/;UID=1", ((StoreChangedEvent)mail.Single()).Name);
        Assert.IsFalse(all.OfType<ExchangeBytesEvent>().Any());
        Assert.HasCount(5, all);
    }

    [TestMethod]
    public async Task FindEvents_MoreThanMaxRecordedEvents_KeepsTheNewestAndCountsTheDropped()
    {
        var (stream, _) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, new ExchangeRecordBounds(10, 100, 2));
        PublishMailForBob(stream);

        var kept = subscriber.FindEvents(new SurlEventQuery());

        CollectionAssert.AreEqual(new long[] { 4, 5 }, kept.Select(surlEvent => surlEvent.Sequence).ToArray());
        Assert.AreEqual(2, subscriber.DroppedEventCount);
    }

    [TestMethod]
    public async Task WaitForEventAsync_AnEventThatAlreadyHappened_ReturnsAtOnce()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        PublishMailForBob(stream);

        var found = await subscriber.WaitForEventAsync(MailForBob, TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.AreEqual(4, found.Sequence);
    }

    [TestMethod]
    public async Task WaitForEventAsync_ALaterEvent_ReturnsWhenItIsPublished()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));

        var waiting = subscriber.WaitForEventAsync(MailForBob, TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.IsFalse(waiting.IsCompleted);
        PublishMailForBob(stream);
        var found = await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsInstanceOfType<StoreChangedEvent>(found);
    }

    [TestMethod]
    public async Task WaitForExchangeAsync_AnExchangeThatComesToMatchLater_ReturnsItsRecordThen()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, SmtpListenUrl, "smtp", Bob));

        var waiting = subscriber.WaitForExchangeAsync(new SurlEventQuery { Account = "bob" }, Timeout.InfiniteTimeSpan, TestContext.CancellationToken);
        stream.Publish(new LoginJudgedEvent(1, "smtp", "bob", "PLAIN", true, false));
        var found = await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(1, found.ExchangeId);
    }

    [TestMethod]
    public async Task WaitForEventAsync_NothingMatchesBeforeTheTimeoutOnTheFakeClock_ThrowsTimeoutExceptionSayingWhatWasAskedAndSeen()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        stream.Publish(new HookFailedEvent("before", HookFailure.Threw, "boom"));

        var waiting = subscriber.WaitForEventAsync(MailForBob, TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        stream.Publish(new HookFailedEvent("h1", HookFailure.Threw, "boom"));
        stream.Publish(new HookFailedEvent("h2", HookFailure.TimedOut, "slow"));
        _ = subscriber.FindEvents(new SurlEventQuery());
        clock.Advance(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsExactlyAsync<TimeoutException>(() => waiting);

        Assert.AreEqual(ExchangeRecordSubscriber.TimedOutMessage("event", MailForBob, TimeSpan.FromSeconds(5), 2), exception.Message);
        StringAssert.Contains(exception.Message, "Account = bob");
        StringAssert.Contains(exception.Message, "2 events were recorded while waiting");
    }

    [TestMethod]
    public async Task WaitForExchangeAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        using var cancellation = new CancellationTokenSource();

        var waiting = subscriber.WaitForExchangeAsync(new SurlEventQuery(), TimeSpan.FromSeconds(5), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }

    [TestMethod]
    public async Task WaitForEventAsync_AlreadyCancelled_ThrowsOperationCanceledExceptionEvenWhenAnEventMatches()
    {
        var (stream, clock) = Clocked();
        await using var subscriber = new ExchangeRecordSubscriber(stream, clock, Bounds(10, 100));
        PublishMailForBob(stream);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => subscriber.WaitForEventAsync(MailForBob, TimeSpan.FromSeconds(5), new CancellationToken(canceled: true)));
    }

    private static (SurlEventStream Stream, ManualTimeProvider Clock) Clocked()
    {
        var clock = new ManualTimeProvider();
        return (new SurlEventStream(clock), clock);
    }

    // Sequences 1 to 5: start, bytes, bob's login, his mail delivered, end.
    private static void PublishMailForBob(SurlEventStream stream)
    {
        stream.Publish(new ExchangeStartedEvent(1, SmtpListenUrl, "smtp", Alice));
        stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "EHLO x\r\n"u8.ToArray()));
        stream.Publish(new LoginJudgedEvent(1, "smtp", "bob", "PLAIN", true, false));
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob/INBOX/;UID=1", 10, 1));
        stream.Publish(new ExchangeEndedEvent(1, 8, 0));
    }
}
