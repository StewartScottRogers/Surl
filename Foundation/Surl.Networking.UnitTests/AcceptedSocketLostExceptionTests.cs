using System.Net.Sockets;

namespace Surl.Networking;

[TestClass]
public sealed class AcceptedSocketLostExceptionTests
{
    [TestMethod]
    public void Constructor_SocketFailure_CarriesItAndItsMessage()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);

        var lost = new AcceptedSocketLostException(failure);

        Assert.AreSame(failure, lost.InnerException);
        Assert.AreEqual(failure.Message, lost.Message);
    }
}
