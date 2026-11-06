using System.Net;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-811: an event callback on its own subscription - matched as FindEvents matches, stopped by disposing, and a
// throwing callback published once as HookFailedEvent (ADR-0124, decision 5).
[TestClass]
public sealed class EventCallbackHookTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly EndPoint Client = new IPEndPoint(IPAddress.Loopback, 50123);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Pump_QueryByScheme_RunsForTheEventsOfThatSchemesExchangesOnly()
    {
        var stream = new SurlEventStream(TimeProvider.System);
        var seen = new List<SurlEvent>();
        var sawLast = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new EventCallbackHook(
            "h1",
            new SurlEventQuery { Scheme = "smtp" },
            (surlEvent, _) =>
            {
                seen.Add(surlEvent);
                if (surlEvent is ExchangeStartedEvent { ExchangeId: 3 })
                {
                    sawLast.SetResult();
                }

                return ValueTask.CompletedTask;
            },
            stream);

        stream.Publish(new ExchangeStartedEvent(1, "smtp://127.0.0.1:25/", "smtp", Client));
        stream.Publish(new ExchangeStartedEvent(2, "imap://127.0.0.1:143/", "imap", Client));
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob", null, 1));
        stream.Publish(new ExchangeEndedEvent(1, 10, 20));
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "late", null, 1));
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Seeded, "seed", null, null));
        stream.Publish(new ExchangeStartedEvent(3, "smtp://127.0.0.1:25/", "smtp", Client));
        await sawLast.Task.WaitAsync(Patience, TestContext.CancellationToken);
        hook.Dispose();
        await hook.Pump.WaitAsync(Patience, TestContext.CancellationToken);

        Assert.HasCount(4, seen);
        Assert.IsInstanceOfType<ExchangeStartedEvent>(seen[0]);
        Assert.AreEqual("bob", ((StoreChangedEvent)seen[1]).Name);
        Assert.IsInstanceOfType<ExchangeEndedEvent>(seen[2]);
    }

    [TestMethod]
    public async Task Pump_CallbackThrows_PublishesHookFailedOnceAndRunsForTheNextEvent()
    {
        var stream = new SurlEventStream(TimeProvider.System);
        await using var watcher = stream.Subscribe(new SurlEventSubscriptionOptions());
        var seen = new List<SurlEvent>();
        var ranOnItsFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new EventCallbackHook(
            "h3",
            new SurlEventQuery(),
            (surlEvent, _) =>
            {
                seen.Add(surlEvent);
                if (surlEvent is HookFailedEvent)
                {
                    ranOnItsFailure.SetResult();
                }

                throw new InvalidOperationException("callback broke");
            },
            stream);

        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Seeded, "seed", null, null));
        await ranOnItsFailure.Task.WaitAsync(Patience, TestContext.CancellationToken);
        hook.Dispose();
        await hook.Pump.WaitAsync(Patience, TestContext.CancellationToken);
        var events = await ReadAsync(watcher, 2);

        Assert.AreEqual(new HookFailedEvent("h3", HookFailure.Threw, "callback broke") with { Sequence = 2, At = events[1].At }, events[1]);
        Assert.HasCount(2, seen);
        Assert.IsFalse(watcher.TryRead(out _));
    }

    [TestMethod]
    public async Task Pump_CallbackThrowsOnAnotherHooksFailure_PublishesItsOwn()
    {
        var stream = new SurlEventStream(TimeProvider.System);
        await using var watcher = stream.Subscribe(new SurlEventSubscriptionOptions());
        var hook = new EventCallbackHook(
            "h1", new SurlEventQuery { Kind = SurlEventKind.HookFailed }, (_, _) => throw new InvalidOperationException("x"), stream);

        stream.Publish(new HookFailedEvent("f9", HookFailure.TimedOut, "slow"));
        var events = await ReadAsync(watcher, 2);
        hook.Dispose();

        Assert.AreEqual("h1", ((HookFailedEvent)events[1]).HookId);
    }

    [TestMethod]
    public async Task Dispose_WhileTheCallbackRuns_CancelsItsTokenPublishesNothingAndRunsItNoMore()
    {
        var stream = new SurlEventStream(TimeProvider.System);
        await using var watcher = stream.Subscribe(new SurlEventSubscriptionOptions());
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var hook = new EventCallbackHook(
            "h1",
            new SurlEventQuery(),
            async (_, token) =>
            {
                calls++;
                running.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            },
            stream);

        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Seeded, "a", null, null));
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Seeded, "b", null, null));
        await running.Task.WaitAsync(Patience, TestContext.CancellationToken);
        hook.Dispose();
        hook.Dispose();
        await hook.Pump.WaitAsync(Patience, TestContext.CancellationToken);
        stream.Publish(new StoreChangedEvent(StoreKind.Mail, StoreChange.Seeded, "c", null, null));

        Assert.AreEqual(1, calls);
        Assert.HasCount(3, await ReadAsync(watcher, 3));
        Assert.IsFalse(watcher.TryRead(out _));
    }

    private async Task<List<SurlEvent>> ReadAsync(SurlEventSubscription subscription, int count)
    {
        var read = new List<SurlEvent>();
        await foreach (var surlEvent in subscription.ReadAllAsync(TestContext.CancellationToken))
        {
            read.Add(surlEvent);
            if (read.Count == count)
            {
                break;
            }
        }

        return read;
    }
}
