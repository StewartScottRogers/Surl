using System.Net.Sockets;

namespace Surl.Networking;

[TestClass]
public sealed class AcceptFailureClassifierTests
{
    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    [DataRow(SocketError.HostDown)]
    [DataRow(SocketError.HostUnreachable)]
    [DataRow(SocketError.NetworkUnreachable)]
    public void IsPerConnection_AnErrorOfOneClient_ReturnsTrue(SocketError socketError)
    {
        Assert.IsTrue(AcceptFailureClassifier.IsPerConnection(socketError));
    }

    [TestMethod]
    [DataRow(SocketError.OperationAborted)]
    [DataRow(SocketError.InvalidArgument)]
    [DataRow(SocketError.NotSocket)]
    [DataRow(SocketError.TooManyOpenSockets)]
    [DataRow(SocketError.NoBufferSpaceAvailable)]
    [DataRow(SocketError.NetworkDown)]
    [DataRow(SocketError.SocketError)]
    public void IsPerConnection_AnErrorOfTheListener_ReturnsFalse(SocketError socketError)
    {
        Assert.IsFalse(AcceptFailureClassifier.IsPerConnection(socketError));
    }

    [TestMethod]
    public void IsPerConnection_SocketExceptionOfOneClient_ReturnsTrue()
    {
        Assert.IsTrue(AcceptFailureClassifier.IsPerConnection(new SocketException((int)SocketError.ConnectionAborted)));
    }

    [TestMethod]
    public void IsPerConnection_SocketExceptionOfTheListener_ReturnsFalse()
    {
        Assert.IsFalse(AcceptFailureClassifier.IsPerConnection(new SocketException((int)SocketError.TooManyOpenSockets)));
    }

    [TestMethod]
    public void IsPerConnection_AcceptedSocketLost_ReturnsTrue()
    {
        var lost = new AcceptedSocketLostException(new SocketException((int)SocketError.InvalidArgument));

        Assert.IsTrue(AcceptFailureClassifier.IsPerConnection(lost));
    }

    [TestMethod]
    public void IsPerConnection_AnotherException_ReturnsFalse()
    {
        Assert.IsFalse(AcceptFailureClassifier.IsPerConnection(new InvalidOperationException()));
    }
}
