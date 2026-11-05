using System.Text.RegularExpressions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The state a broker's connections share: client identifiers and pending Wills.
/// </summary>
[TestClass]
public sealed partial class Mqtt5BrokerStateTests
{
    private static readonly Mqtt5ApplicationMessage First = new("a", [1], 0, false, []);
    private static readonly Mqtt5ApplicationMessage Second = new("b", [2], 0, false, []);

    [TestMethod]
    public void NewAssignedClientIdentifier_IsSurlAnd32LowerCaseHexDigits()
    {
        Assert.IsTrue(AssignedClientIdentifierPattern().IsMatch(Mqtt5BrokerState.NewAssignedClientIdentifier()));
    }

    [TestMethod]
    public void RegisterAssignedClient_CandidateInUse_DrawsAnother()
    {
        var candidates = new Queue<string>(["surl-taken", "surl-free"]);
        var state = new Mqtt5BrokerState(candidates.Dequeue);
        state.RegisterClient("surl-taken");

        var assigned = state.RegisterAssignedClient(out _);

        Assert.AreEqual("surl-free", assigned);
        Assert.IsTrue(state.IsConnected("surl-free"));
    }

    [TestMethod]
    public void UnregisterClient_StaleRegistration_KeepsTheLaterConnection()
    {
        var state = new Mqtt5BrokerState();
        var earlier = state.RegisterClient("c");
        var later = state.RegisterClient("c");

        state.UnregisterClient("c", earlier);
        Assert.IsTrue(state.IsConnected("c"));
        state.UnregisterClient("c", later);

        Assert.IsFalse(state.IsConnected("c"));
    }

    [TestMethod]
    public void ScheduleWill_WhileAnotherWaits_PublishesTheEarlierAtOnceAndTheLaterWhenDue()
    {
        var clock = new ManualTimeProvider();
        var state = new Mqtt5BrokerState();
        var published = new List<Mqtt5ApplicationMessage>();
        state.ApplicationMessagePublished += published.Add;

        state.ScheduleWill("c", First, TimeSpan.FromSeconds(5), clock);
        state.ScheduleWill("c", Second, TimeSpan.FromSeconds(5), clock);
        CollectionAssert.AreEqual(new[] { First }, published);
        clock.Advance(TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(new[] { First, Second }, published);
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public void SettlePendingWill_NothingPending_PublishesNothing()
    {
        var state = new Mqtt5BrokerState();
        var published = new List<Mqtt5ApplicationMessage>();
        state.ApplicationMessagePublished += published.Add;

        state.SettlePendingWill("c", cleanStart: true);

        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void TimerFiringAfterTheWillWasSettled_PublishesNothing()
    {
        var clock = new CallbackCapturingTimeProvider();
        var state = new Mqtt5BrokerState();
        var published = new List<Mqtt5ApplicationMessage>();
        state.ApplicationMessagePublished += published.Add;
        state.ScheduleWill("c", First, TimeSpan.FromSeconds(5), clock);
        state.SettlePendingWill("c", cleanStart: false);

        clock.FireEveryCallback();

        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void Publish_NobodyListening_DoesNothing()
    {
        new Mqtt5BrokerState().Publish(First);
    }

    [GeneratedRegex("^surl-[0-9a-f]{32}$")]
    private static partial Regex AssignedClientIdentifierPattern();

    /// <summary>
    /// A clock whose timers never fire on their own: <see cref="FireEveryCallback"/> runs every
    /// callback ever scheduled, disposed or not, standing in for a timer that fired on another
    /// thread while its Will was being settled.
    /// </summary>
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
