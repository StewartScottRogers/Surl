using System.Net;
using System.Net.Sockets;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ListenerBindExceptionTests
{
    [TestMethod]
    public void Constructor_AddressInUse_KeepsEveryMember()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 8080);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var inner = new SocketException((int)SocketError.AddressAlreadyInUse);

        var exception = new ListenerBindException(listenUrl, endPoint, ListenerBindFailure.AddressInUse, inner);

        Assert.AreSame(listenUrl, exception.ListenUrl);
        Assert.AreSame(endPoint, exception.EndPoint);
        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.AreSame(inner, exception.InnerException);
        Assert.AreEqual("Could not listen on http://127.0.0.1:8080: AddressInUse.", exception.Message);
    }

    [TestMethod]
    public void Constructor_HostNotFound_HasNoEndPoint()
    {
        var listenUrl = new ListenUrl("http", "no-such-host.invalid", 80);

        var exception = new ListenerBindException(listenUrl, null, ListenerBindFailure.HostNotFound, null);

        Assert.IsNull(exception.EndPoint);
        Assert.IsNull(exception.InnerException);
        Assert.AreEqual(ListenerBindFailure.HostNotFound, exception.Failure);
    }

    [TestMethod]
    public void Constructor_NullListenUrl_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ListenerBindException(null!, null, ListenerBindFailure.Other, null));
    }
}
