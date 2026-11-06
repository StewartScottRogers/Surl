using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The login a CONNECT payload carries (SIO "Connection to a namespace"; ADR-0120, decision 4).
/// </summary>
[TestClass]
public sealed class SocketIoConnectCredentialsTests
{
    [TestMethod]
    [DataRow("{\"token\":\"abc\"}", "", "abc")]
    [DataRow("{\"token\":\"abc\",\"username\":\"alice\",\"password\":\"x\"}", "", "abc")]
    [DataRow("{\"token\":7,\"username\":\"alice\",\"password\":\"x\"}", "alice", "x")]
    [DataRow("{\"username\":\"alice\",\"password\":\"x\",\"room\":\"r\"}", "alice", "x")]
    public void Read_Credentials_AreTheLoginItNames(string payload, string userName, string password)
    {
        var login = SocketIoConnectCredentials.Read(Encoding.UTF8.GetBytes(payload), "socketio", null);

        Assert.AreEqual(new PasswordLogin("socketio", userName, null, null), login with { Password = null });
        Assert.AreEqual(password, Encoding.UTF8.GetString(login.Password!.Value.Span));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{}")]
    [DataRow("{\"username\":\"alice\"}")]
    [DataRow("{\"username\":\"alice\",\"password\":1}")]
    [DataRow("{\"password\":\"x\"}")]
    public void Read_NoCredentials_IsALoginWithoutNameOrPassword(string payload)
    {
        var tlsSession = InMemoryConnection.DefaultUpgradeTlsSession;

        var login = SocketIoConnectCredentials.Read(Encoding.UTF8.GetBytes(payload), "socketios", tlsSession);

        Assert.AreEqual(new PasswordLogin("socketios", null, null, tlsSession), login);
    }
}
