namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A listener's sessions: reserved, found by <c>sid</c>, and forgotten (ADR-0120, decisions 1 and 7).
/// </summary>
[TestClass]
public sealed class SocketIoSessionsTests
{
    [TestMethod]
    public void TryReserve_FreshListener_ReservesA20CharacterSidUntilRemoved()
    {
        var sessions = new SocketIoSessions();

        var sessionId = sessions.TryReserve()!;

        Assert.AreEqual(20, sessionId.Length);
        Assert.IsTrue(sessions.Contains(sessionId));
        Assert.AreEqual(1, sessions.Count);
        Assert.IsEmpty(sessions.Attached());
        sessions.Remove(sessionId);
        Assert.IsFalse(sessions.Contains(sessionId));
        Assert.AreEqual(0, sessions.Count);
    }

    [TestMethod]
    public void TryReserve_ListenerHoldingMaxSessions_ReturnsNull()
    {
        var sessions = new SocketIoSessions();
        for (var session = 0; session < SocketIoSessions.MaxSessions; session++)
        {
            Assert.IsNotNull(sessions.TryReserve());
        }

        Assert.IsNull(sessions.TryReserve());
    }
}
