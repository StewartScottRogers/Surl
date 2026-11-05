using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

[TestClass]
public sealed class KafkaConnectionStateTests
{
    [TestMethod]
    public void Constructor_TlsSession_IsKeptAndTheConnectionStartsLoggedOut()
    {
        var state = new KafkaConnectionState(InMemoryConnection.DefaultUpgradeTlsSession);

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, state.TlsSession);
        Assert.IsFalse(state.IsAuthenticated);
        Assert.IsNull(state.AccountName);
        Assert.IsNull(state.PendingLogin);
    }

    [TestMethod]
    public void EndLogin_Accepted_AuthenticatesTheAccountAndEndsThePendingLogin()
    {
        var state = new KafkaConnectionState(null);
        state.BeginLogin(new KafkaSaslLogin(new AnonymousAuthenticationPolicy(), "PLAIN", 1));

        state.EndLogin(true, "alice");

        Assert.IsTrue(state.IsAuthenticated);
        Assert.AreEqual("alice", state.AccountName);
        Assert.IsNull(state.PendingLogin);
    }
}
