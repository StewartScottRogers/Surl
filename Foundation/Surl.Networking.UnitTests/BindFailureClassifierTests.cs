using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class BindFailureClassifierTests
{
    [TestMethod]
    [DataRow(SocketError.AddressAlreadyInUse, ListenerBindFailure.AddressInUse)]
    [DataRow(SocketError.AddressNotAvailable, ListenerBindFailure.AddressNotAvailable)]
    [DataRow(SocketError.AccessDenied, ListenerBindFailure.PermissionDenied)]
    [DataRow(SocketError.HostNotFound, ListenerBindFailure.HostNotFound)]
    [DataRow(SocketError.NoData, ListenerBindFailure.HostNotFound)]
    [DataRow(SocketError.NetworkDown, ListenerBindFailure.Other)]
    [DataRow(SocketError.InvalidArgument, ListenerBindFailure.Other)]
    public void Classify_SocketError_NamesTheFailure(SocketError socketError, ListenerBindFailure expected)
    {
        Assert.AreEqual(expected, BindFailureClassifier.Classify(socketError));
    }
}
