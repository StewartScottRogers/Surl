using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class ListenAddressResolverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ResolveAsync_IPv4Literal_ReturnsItWithoutResolving()
    {
        var resolver = new ListenAddressResolver(ResolverThatMustNotBeCalled);

        var addresses = await resolver.ResolveAsync(new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_IPv6Literal_ReturnsItWithoutResolving()
    {
        var resolver = new ListenAddressResolver(ResolverThatMustNotBeCalled);

        var addresses = await resolver.ResolveAsync(new ListenUrl("http", "::1", 0), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_HostName_ReturnsEveryAddressOnceInResolvedOrder()
    {
        string? askedFor = null;
        var resolver = new ListenAddressResolver((host, _) =>
        {
            askedFor = host;
            return Task.FromResult(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback, IPAddress.IPv6Loopback });
        });

        var addresses = await resolver.ResolveAsync(new ListenUrl("http", "localhost", 0), TestContext.CancellationToken);

        Assert.AreEqual("localhost", askedFor);
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_HostNameWithNoAddress_ThrowsHostNotFound()
    {
        var listenUrl = new ListenUrl("http", "empty.example", 80);
        var resolver = new ListenAddressResolver((_, _) => Task.FromResult(Array.Empty<IPAddress>()));

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => resolver.ResolveAsync(listenUrl, TestContext.CancellationToken));

        Assert.AreEqual(ListenerBindFailure.HostNotFound, exception.Failure);
        Assert.AreSame(listenUrl, exception.ListenUrl);
        Assert.IsNull(exception.EndPoint);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    [DataRow(SocketError.HostNotFound)]
    [DataRow(SocketError.TryAgain)]
    public async Task ResolveAsync_ResolutionFails_ThrowsHostNotFoundCarryingTheSocketException(SocketError socketError)
    {
        var listenUrl = new ListenUrl("http", "no-such-host.invalid", 80);
        var failure = new SocketException((int)socketError);
        var resolver = new ListenAddressResolver((_, _) => Task.FromException<IPAddress[]>(failure));

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => resolver.ResolveAsync(listenUrl, TestContext.CancellationToken));

        Assert.AreEqual(ListenerBindFailure.HostNotFound, exception.Failure);
        Assert.IsNull(exception.EndPoint);
        Assert.AreSame(failure, exception.InnerException);
    }

    [TestMethod]
    public async Task ResolveAsync_Cancelled_PassesTheTokenToTheResolver()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var resolver = new ListenAddressResolver((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Array.Empty<IPAddress>());
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(new ListenUrl("http", "localhost", 0), source.Token));
    }

    [TestMethod]
    public async Task ResolveAsync_NullListenUrl_Throws()
    {
        var resolver = new ListenAddressResolver(ResolverThatMustNotBeCalled);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => resolver.ResolveAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Constructor_NullResolver_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ListenAddressResolver(null!));
    }

    private static Task<IPAddress[]> ResolverThatMustNotBeCalled(string host, CancellationToken cancellationToken)
    {
        throw new AssertFailedException($"An IP literal was resolved: {host}");
    }
}
