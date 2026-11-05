namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The hub's negotiated connections: fresh IDs, the 1024 ceiling and expiry (ADR-0119, decisions 2 and 7).
/// </summary>
[TestClass]
public sealed class SignalRHubConnectionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Negotiate_VersionOne_GivesAnIdAndAnIndependentTokenEach22Base64UrlCharacters()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        var connection = connections.Negotiate("alice", sendsConnectionToken: true, TimeSpan.FromSeconds(30))!;

        Assert.MatchesRegex("^[A-Za-z0-9_-]{22}$", connection.ConnectionId);
        Assert.MatchesRegex("^[A-Za-z0-9_-]{22}$", connection.ConnectionToken!);
        Assert.AreNotEqual(connection.ConnectionId, connection.ConnectionToken);
        Assert.AreEqual(connection.ConnectionToken, connection.UpgradeId);
        Assert.AreEqual("alice", connection.AccountName);
        Assert.AreEqual(Now.AddSeconds(30), connection.ExpiresAt);
    }

    [TestMethod]
    public void Negotiate_VersionZero_HasNoTokenAndUpgradesByItsId()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        var connection = connections.Negotiate(null, sendsConnectionToken: false, TimeSpan.FromSeconds(30))!;

        Assert.IsNull(connection.ConnectionToken);
        Assert.AreEqual(connection.ConnectionId, connection.UpgradeId);
    }

    [TestMethod]
    public void Negotiate_Twice_GivesFreshIds()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        var first = connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;
        var second = connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;

        Assert.AreNotEqual(first.ConnectionId, second.ConnectionId);
        Assert.AreNotEqual(first.ConnectionToken, second.ConnectionToken);
        Assert.AreEqual(2, connections.Count);
    }

    [TestMethod]
    public void Negotiate_InfiniteWindow_NeverExpires()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        var connection = connections.Negotiate(null, true, Timeout.InfiniteTimeSpan)!;

        Assert.AreEqual(DateTimeOffset.MaxValue, connection.ExpiresAt);
    }

    [TestMethod]
    public void Negotiate_HubFull_IsRefusedUntilOneExpires()
    {
        var clock = new SteppingTimeProvider(Now);
        var connections = new SignalRHubConnections(clock);
        connections.Negotiate(null, true, TimeSpan.FromSeconds(10));
        for (var index = 1; index < SignalRHubConnections.MaxConnections; index++)
        {
            connections.Negotiate(null, true, TimeSpan.FromSeconds(60));
        }

        var refused = connections.Negotiate(null, true, TimeSpan.FromSeconds(60));
        clock.Advance(TimeSpan.FromSeconds(10));
        var afterExpiry = connections.Negotiate(null, true, TimeSpan.FromSeconds(60));

        Assert.IsNull(refused);
        Assert.IsNotNull(afterExpiry);
        Assert.AreEqual(SignalRHubConnections.MaxConnections, connections.Count);
    }

    [TestMethod]
    public void Count_AfterTheUpgradeWindow_ForgetsTheConnection()
    {
        var clock = new SteppingTimeProvider(Now);
        var connections = new SignalRHubConnections(clock);
        connections.Negotiate(null, true, TimeSpan.FromSeconds(30));

        clock.Advance(TimeSpan.FromSeconds(29));
        var beforeExpiry = connections.Count;
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(1, beforeExpiry);
        Assert.AreEqual(0, connections.Count);
    }

    [TestMethod]
    public void TakeUp_NegotiatedTokenBySameCaller_HandsOverTheConnectionOnce()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));
        var negotiated = connections.Negotiate("alice", true, TimeSpan.FromSeconds(30))!;

        var first = connections.TakeUp(negotiated.UpgradeId, "alice", out var taken);
        var second = connections.TakeUp(negotiated.UpgradeId, "alice", out var again);

        Assert.AreEqual(SignalRUpgradeOutcome.TakenUp, first);
        Assert.AreSame(negotiated, taken);
        Assert.AreEqual(SignalRUpgradeOutcome.AlreadyTakenUp, second);
        Assert.IsNull(again);
        Assert.AreEqual(1, connections.Count);
    }

    [TestMethod]
    public void TakeUp_AfterEnd_IsNotFound()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));
        var negotiated = connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;
        connections.TakeUp(negotiated.UpgradeId, null, out _);

        connections.End(negotiated);

        Assert.AreEqual(SignalRUpgradeOutcome.NotFound, connections.TakeUp(negotiated.UpgradeId, null, out _));
        Assert.AreEqual(0, connections.Count);
    }

    [TestMethod]
    public void TakeUp_AfterTheUpgradeWindow_IsNotFound()
    {
        var clock = new SteppingTimeProvider(Now);
        var connections = new SignalRHubConnections(clock);
        var negotiated = connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.AreEqual(SignalRUpgradeOutcome.NotFound, connections.TakeUp(negotiated.UpgradeId, null, out _));
    }

    [TestMethod]
    public void TakeUp_ByAnotherAccount_IsRefusedAndLeavesTheConnectionWaiting()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));
        var negotiated = connections.Negotiate("alice", true, TimeSpan.FromSeconds(30))!;

        var outcome = connections.TakeUp(negotiated.UpgradeId, "bob", out var taken);

        Assert.AreEqual(SignalRUpgradeOutcome.NegotiatedByAnotherCaller, outcome);
        Assert.IsNull(taken);
        Assert.AreEqual(SignalRUpgradeOutcome.TakenUp, connections.TakeUp(negotiated.UpgradeId, "alice", out _));
    }

    [TestMethod]
    public void TakeUp_WithoutAnId_MakesANewConnectionThatNeverExpiresUntilTheHubIsFull()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        var outcome = connections.TakeUp(null, "alice", out var taken);
        for (var index = 1; index < SignalRHubConnections.MaxConnections; index++)
        {
            connections.TakeUp(null, "alice", out _);
        }

        Assert.AreEqual(SignalRUpgradeOutcome.TakenUp, outcome);
        Assert.AreEqual("alice", taken!.AccountName);
        Assert.IsNotNull(taken.ConnectionToken);
        Assert.AreEqual(DateTimeOffset.MaxValue, taken.ExpiresAt);
        Assert.AreEqual(SignalRUpgradeOutcome.HubFull, connections.TakeUp(null, "alice", out var refused));
        Assert.IsNull(refused);
        Assert.IsNull(connections.Negotiate("alice", true, TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public void End_NullConnection_Throws()
    {
        var connections = new SignalRHubConnections(new SteppingTimeProvider(Now));

        Assert.ThrowsExactly<ArgumentNullException>(() => connections.End(null!));
    }
}
