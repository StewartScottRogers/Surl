using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class ListenerBinderTests
{
    [TestMethod]
    public void BindAll_PortZero_AsksForTheFirstAddressesPortOnTheRest()
    {
        var asked = new List<IPEndPoint>();
        var listenUrl = new ListenUrl("http", "localhost", 0);

        var bound = ListenerBinder.BindAll(
            listenUrl,
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint =>
            {
                asked.Add(endPoint);
                return new FakeListener(endPoint.Port == 0 ? 49152 : endPoint.Port);
            },
            listener => listener.Port,
            listener => listener.Released = true);

        CollectionAssert.AreEqual(
            new[] { new IPEndPoint(IPAddress.IPv6Loopback, 0), new IPEndPoint(IPAddress.Loopback, 49152) },
            asked);
        Assert.HasCount(2, bound);
        Assert.IsTrue(bound.All(listener => !listener.Released));
    }

    [TestMethod]
    public void BindAll_FixedPort_AsksForItOnEveryAddress()
    {
        var asked = new List<IPEndPoint>();

        ListenerBinder.BindAll(
            new ListenUrl("http", "localhost", 8080),
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint =>
            {
                asked.Add(endPoint);
                return new FakeListener(endPoint.Port);
            },
            listener => listener.Port,
            listener => listener.Released = true);

        Assert.IsTrue(asked.All(endPoint => endPoint.Port == 8080));
    }

    [TestMethod]
    [DataRow(SocketError.AddressAlreadyInUse, ListenerBindFailure.AddressInUse)]
    [DataRow(SocketError.AddressNotAvailable, ListenerBindFailure.AddressNotAvailable)]
    [DataRow(SocketError.AccessDenied, ListenerBindFailure.PermissionDenied)]
    [DataRow(SocketError.NetworkDown, ListenerBindFailure.Other)]
    public void BindAll_SecondAddressFails_ReleasesTheFirstAndThrowsTheFailure(
        SocketError socketError, ListenerBindFailure expected)
    {
        var listenUrl = new ListenUrl("http", "localhost", 8080);
        var failure = new SocketException((int)socketError);
        var first = new FakeListener(8080);

        var exception = Assert.ThrowsExactly<ListenerBindException>(() => ListenerBinder.BindAll(
            listenUrl,
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint => endPoint.Address.Equals(IPAddress.Loopback) ? throw failure : first,
            listener => listener.Port,
            listener => listener.Released = true));

        Assert.IsTrue(first.Released);
        Assert.AreEqual(expected, exception.Failure);
        Assert.AreSame(listenUrl, exception.ListenUrl);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8080), exception.EndPoint);
        Assert.AreSame(failure, exception.InnerException);
    }

    [TestMethod]
    public void BindAll_BindThrowsSomethingElse_ReleasesWhatWasBoundAndRethrows()
    {
        var first = new FakeListener(8080);

        Assert.ThrowsExactly<InvalidOperationException>(() => ListenerBinder.BindAll(
            new ListenUrl("http", "localhost", 8080),
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint => endPoint.Address.Equals(IPAddress.Loopback) ? throw new InvalidOperationException() : first,
            listener => listener.Port,
            listener => listener.Released = true));

        Assert.IsTrue(first.Released);
    }

    [TestMethod]
    public void BindAll_PortZeroTakenOnALaterAddress_StartsAgainFromTheFirst()
    {
        var firstAttempts = new List<FakeListener>();
        var laterAttempts = 0;

        var bound = ListenerBinder.BindAll(
            new ListenUrl("http", "localhost", 0),
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint =>
            {
                if (endPoint.Address.Equals(IPAddress.IPv6Loopback))
                {
                    firstAttempts.Add(new FakeListener(49152 + firstAttempts.Count));
                    return firstAttempts[^1];
                }

                return ++laterAttempts == 1
                    ? throw new SocketException((int)SocketError.AddressAlreadyInUse)
                    : new FakeListener(endPoint.Port);
            },
            listener => listener.Port,
            listener => listener.Released = true);

        Assert.HasCount(2, firstAttempts);
        Assert.IsTrue(firstAttempts[0].Released);
        Assert.AreSame(firstAttempts[1], bound[0]);
        Assert.AreEqual(49153, bound[1].Port);
    }

    [TestMethod]
    public void BindAll_PortZeroTakenOnALaterAddressEveryTime_GivesUpAfterTheLastAttempt()
    {
        var firstAttempts = 0;

        var exception = Assert.ThrowsExactly<ListenerBindException>(() => ListenerBinder.BindAll(
            new ListenUrl("http", "localhost", 0),
            [IPAddress.IPv6Loopback, IPAddress.Loopback],
            endPoint => endPoint.Address.Equals(IPAddress.IPv6Loopback)
                ? new FakeListener(49152 + firstAttempts++)
                : throw new SocketException((int)SocketError.AddressAlreadyInUse),
            listener => listener.Port,
            listener => listener.Released = true));

        Assert.AreEqual(ListenerBinder.EphemeralPortAttempts, firstAttempts);
        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
    }

    [TestMethod]
    public void BindAll_FixedPortInUse_DoesNotTryAgain()
    {
        var attempts = 0;

        Assert.ThrowsExactly<ListenerBindException>(() => ListenerBinder.BindAll<FakeListener>(
            new ListenUrl("http", "127.0.0.1", 8080),
            [IPAddress.Loopback],
            _ =>
            {
                attempts++;
                throw new SocketException((int)SocketError.AddressAlreadyInUse);
            },
            listener => listener.Port,
            listener => listener.Released = true));

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public void BindAll_PortZeroFailsForAnotherReason_DoesNotTryAgain()
    {
        var attempts = 0;

        Assert.ThrowsExactly<ListenerBindException>(() => ListenerBinder.BindAll<FakeListener>(
            new ListenUrl("http", "127.0.0.1", 0),
            [IPAddress.Loopback],
            _ =>
            {
                attempts++;
                throw new SocketException((int)SocketError.AccessDenied);
            },
            listener => listener.Port,
            listener => listener.Released = true));

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public void BindAll_ReadingTheBoundPortFails_ReleasesTheListenerJustBound()
    {
        var listener = new FakeListener(8080);

        Assert.ThrowsExactly<InvalidOperationException>(() => ListenerBinder.BindAll(
            new ListenUrl("http", "127.0.0.1", 0),
            [IPAddress.Loopback],
            _ => listener,
            _ => throw new InvalidOperationException(),
            bound => bound.Released = true));

        Assert.IsTrue(listener.Released);
    }

    [TestMethod]
    public void BindAll_NoAddress_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ListenerBinder.BindAll(
            new ListenUrl("http", "localhost", 8080),
            [],
            endPoint => new FakeListener(endPoint.Port),
            listener => listener.Port,
            listener => listener.Released = true));
    }

    [TestMethod]
    public void ReleaseAll_ReleasesEveryListener()
    {
        FakeListener[] listeners = [new(1), new(2)];

        ListenerBinder.ReleaseAll(listeners, listener => listener.Released = true);

        Assert.IsTrue(listeners.All(listener => listener.Released));
    }

    private sealed class FakeListener(int port)
    {
        public int Port { get; } = port;

        public bool Released { get; set; }
    }
}
