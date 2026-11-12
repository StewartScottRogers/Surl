using System.Text.RegularExpressions;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The state a broker's connections share: client identifiers, pending Wills, and the message
/// bus every published message goes to.
/// </summary>
[TestClass]
public sealed partial class Mqtt5BrokerStateTests
{
    private static readonly Mqtt5ApplicationMessage First = new("a", [1], 0, false, []);
    private static readonly Mqtt5ApplicationMessage Second = new("b", [2], 0, false, []);

    [TestMethod]
    public void Constructor_NullBus_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5BrokerState(null!));
    }

    [TestMethod]
    public void NewAssignedClientIdentifier_IsSurlAnd32LowerCaseHexDigits()
    {
        Assert.IsTrue(AssignedClientIdentifierPattern().IsMatch(Mqtt5BrokerState.NewAssignedClientIdentifier(new SeededIdentifierRandom(1))));
    }

    [TestMethod]
    public void AssignedClientIdentifier_SameSeededIdentifiers_IsTheSameEveryRun()
    {
        var firstRun = NewState().RegisterAssignedClient(new SeededIdentifierRandom(7), out _);

        var secondRun = NewState().RegisterAssignedClient(new SeededIdentifierRandom(7), out _);

        Assert.AreEqual(firstRun, secondRun);
        Assert.IsTrue(AssignedClientIdentifierPattern().IsMatch(firstRun));
    }

    [TestMethod]
    public void AssignedClientIdentifier_CollidesWithASession_IsDrawnAgain()
    {
        var draws = new SeededIdentifierRandom(7);
        var firstDraw = Mqtt5BrokerState.NewAssignedClientIdentifier(draws);
        var secondDraw = Mqtt5BrokerState.NewAssignedClientIdentifier(draws);
        var state = NewState();
        state.RegisterClient(firstDraw);

        var assigned = state.RegisterAssignedClient(new SeededIdentifierRandom(7), out _);

        Assert.AreEqual(secondDraw, assigned);
        Assert.IsTrue(state.IsConnected(secondDraw));
    }

    [TestMethod]
    public void UnregisterClient_StaleRegistration_KeepsTheLaterConnection()
    {
        var state = NewState();
        var earlier = state.RegisterClient("c");
        var later = state.RegisterClient("c");

        state.UnregisterClient("c", earlier);
        Assert.IsTrue(state.IsConnected("c"));
        state.UnregisterClient("c", later);

        Assert.IsFalse(state.IsConnected("c"));
    }

    [TestMethod]
    public void Publish_ForAnAccount_PublishesOnTheBusAsThatAccount()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: MessageBusConfiguration.None with { AccessGrants = [new TopicAccessGrant("a", "alice", TopicAccessRights.Publish)] });
        var state = new Mqtt5BrokerState(bus);

        var allowed = state.Publish(First, "alice", DateTimeOffset.UnixEpoch);
        var refused = state.Publish(First, "bob", DateTimeOffset.UnixEpoch);

        Assert.AreEqual(MessageBusOutcome.Succeeded, allowed.Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, refused.Outcome);
    }

    [TestMethod]
    public void ScheduleWill_WhileAnotherWaits_PublishesTheEarlierAtOnceAndTheLaterWhenDue()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var published = PublishedOn(state);

        state.ScheduleWill("c", First, null, TimeSpan.FromSeconds(5), clock);
        state.ScheduleWill("c", Second, null, TimeSpan.FromSeconds(5), clock);
        CollectionAssert.AreEqual(new[] { "a" }, published.Select(message => message.Topic).ToArray());
        clock.Advance(TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(new[] { "a", "b" }, published.Select(message => message.Topic).ToArray());
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public void SettlePendingWill_NothingPending_PublishesNothing()
    {
        var state = NewState();
        var published = PublishedOn(state);

        state.SettlePendingWill("c", sessionResumed: false);

        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void TimerFiringAfterTheWillWasSettled_PublishesNothing()
    {
        var clock = new CallbackCapturingTimeProvider();
        var state = NewState();
        var published = PublishedOn(state);
        state.ScheduleWill("c", First, null, TimeSpan.FromSeconds(5), clock);
        state.SettlePendingWill("c", sessionResumed: true);

        clock.FireEveryCallback();

        Assert.IsEmpty(published);
    }

    [GeneratedRegex("^surl-[0-9a-f]{32}$")]
    private static partial Regex AssignedClientIdentifierPattern();

    /// <summary>
    /// A clock whose timers never fire on their own: <see cref="FireEveryCallback"/> runs every
    /// callback ever scheduled, disposed or not, standing in for a timer that fired on another
    /// thread while its Will was being settled.
    /// </summary>
    [TestMethod]
    public void ExpiryTimerFiringAfterTheSessionWasResumed_KeepsIt()
    {
        var clock = new CallbackCapturingTimeProvider();
        var state = NewState();
        var session = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(5), "mqtt5", out _)!;
        state.CloseSession(session, clock);
        var resumed = state.OpenSession("c", null, cleanStart: false, TimeSpan.FromSeconds(5), "mqtt5", out var wasResumed);

        clock.FireEveryCallback();

        Assert.IsTrue(wasResumed);
        Assert.AreSame(session, resumed);
        Assert.IsTrue(state.HasSession("c"));
    }

    [TestMethod]
    public void ExpireSession_ForASessionNoLongerKept_DoesNothing()
    {
        var state = NewState();
        var session = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(5), "mqtt5", out _)!;
        state.CloseSession(session, new CallbackCapturingTimeProvider());
        var replacement = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(5), "mqtt5", out _)!;
        state.CloseSession(replacement, new CallbackCapturingTimeProvider());

        state.ExpireSession(session);

        Assert.IsTrue(state.HasSession("c"));
    }

    [TestMethod]
    public void CloseSession_ReplacedSessionWhoseReplacementHasEnded_EndsIt()
    {
        var state = NewState();
        var replaced = state.OpenSession("c", null, cleanStart: false, TimeSpan.FromSeconds(5), "mqtt5", out _)!;
        var replacement = state.OpenSession("c", null, cleanStart: false, TimeSpan.Zero, "mqtt5", out var resumed)!;
        state.CloseSession(replacement, new CallbackCapturingTimeProvider());

        state.CloseSession(replaced, new CallbackCapturingTimeProvider());

        Assert.IsFalse(resumed);
        Assert.IsFalse(state.HasSession("c"));
    }

    /// <summary>A seeded identifier source: the same seed draws the same bytes every run.</summary>
    private sealed class SeededIdentifierRandom(int seed) : IIdentifierRandom
    {
        private readonly Random random = new(seed);

        public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
    }

    private sealed class CallbackCapturingTimeProvider : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> callbacks = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            callbacks.Add((callback, state));
            return new InertTimer();
        }

        public void FireEveryCallback()
        {
            foreach (var (callback, state) in callbacks)
            {
                callback(state);
            }
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
