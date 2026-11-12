using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Surl.Networking;

/// <summary>
/// <see cref="TlsClientConnector"/> and <see cref="TlsClientConnection"/> (ADR-0149 decisions 5 and 11,
/// BL-1068) against an in-process <see cref="SslStream"/> server over an in-memory pipe, so no socket is
/// opened; the last test repeats the main case over a real loopback socket.
/// </summary>
[TestClass]
public sealed class TlsClientConnectorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);
    private static readonly IPEndPoint ServerEnd = new(IPAddress.Loopback, 636);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_SecureAtOnceAndCertificateTrustedByGivenCertificate_CarriesBytesOverTls()
    {
        using var certificate = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        var serving = ServeAsync(server, certificate, async stream => await Echo(stream, 4));
        var connector = new TlsClientConnector([certificate], Time, sockets);

        await using var connection = await connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken);
        await connection.Stream.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        var echoed = new byte[4];
        await connection.Stream.ReadExactlyAsync(echoed, TestContext.CancellationToken);
        await serving;

        Assert.IsTrue(connection.IsSecured);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(echoed));
    }

    [TestMethod]
    public async Task ConnectAsync_IpLiteralNamedInCertificate_IsAccepted()
    {
        using var certificate = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        var serving = ServeAsync(server, certificate, _ => Task.CompletedTask);
        var connector = new TlsClientConnector([certificate], Time, sockets);

        await using var connection = await connector.ConnectAsync("127.0.0.1", 636, true, Timeout, TestContext.CancellationToken);
        await serving;

        Assert.IsTrue(connection.IsSecured);
    }

    [TestMethod]
    public async Task StartTlsAsync_AfterClearBytesWereExchanged_SecuresTheSameStream()
    {
        using var certificate = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        var serving = Task.Run(async () =>
        {
            await server.ReadExactlyAsync(new byte[5], TestContext.CancellationToken);
            await server.WriteAsync(Encoding.ASCII.GetBytes("ok"), TestContext.CancellationToken);
            await ServeAsync(server, certificate, async stream => await Echo(stream, 2));
        }, TestContext.CancellationToken);
        var connector = new TlsClientConnector([certificate], Time, sockets);

        await using var connection = await connector.ConnectAsync("localhost", 636, false, Timeout, TestContext.CancellationToken);
        await connection.Stream.WriteAsync(Encoding.ASCII.GetBytes("start"), TestContext.CancellationToken);
        var answer = new byte[2];
        await connection.Stream.ReadExactlyAsync(answer, TestContext.CancellationToken);
        var securedBefore = connection.IsSecured;
        await connection.StartTlsAsync(TestContext.CancellationToken);
        await connection.Stream.WriteAsync(Encoding.ASCII.GetBytes("hi"), TestContext.CancellationToken);
        var echoed = new byte[2];
        await connection.Stream.ReadExactlyAsync(echoed, TestContext.CancellationToken);
        await serving;

        Assert.IsFalse(securedBefore);
        Assert.AreEqual("ok", Encoding.ASCII.GetString(answer));
        Assert.IsTrue(connection.IsSecured);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(echoed));
    }

    [TestMethod]
    public async Task StartTlsAsync_AlreadySecured_Throws()
    {
        using var certificate = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        var serving = ServeAsync(server, certificate, _ => Task.CompletedTask);
        var connector = new TlsClientConnector([certificate], Time, sockets);
        await using var connection = await connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken);
        await serving;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.StartTlsAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ConnectAsync_CertificateTrustedByNothing_IsUntrusted()
    {
        using var certificate = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        _ = ServeAsync(server, certificate, _ => Task.CompletedTask).ContinueWith(_ => { }, TaskScheduler.Default);
        var connector = new TlsClientConnector([], Time, sockets);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.UntrustedCertificate, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_GivenCertificateIsAnotherOne_IsUntrusted()
    {
        using var certificate = ServerCertificate("localhost");
        using var other = ServerCertificate("localhost");
        var (sockets, server) = SocketsToServer();
        _ = ServeAsync(server, certificate, _ => Task.CompletedTask).ContinueWith(_ => { }, TaskScheduler.Default);
        var connector = new TlsClientConnector([other], Time, sockets);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.UntrustedCertificate, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_CertificateNotNamingTheHost_IsRefusedAsNameMismatch()
    {
        using var certificate = ServerCertificate("other.example");
        var (sockets, server) = SocketsToServer();
        _ = ServeAsync(server, certificate, _ => Task.CompletedTask).ContinueWith(_ => { }, TaskScheduler.Default);
        var connector = new TlsClientConnector([certificate], Time, sockets);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.CertificateNameMismatch, exception.Failure);
    }

    [TestMethod]
    public async Task StartTlsAsync_FailedHandshake_ClosesTheConnectionAndKeepsWhatWasSentOutOfTheMessage()
    {
        var (sockets, server) = SocketsToServer();
        var connector = new TlsClientConnector([], Time, sockets);
        await using var connection = await connector.ConnectAsync("localhost", 636, false, Timeout, TestContext.CancellationToken);
        await connection.Stream.WriteAsync(Encoding.ASCII.GetBytes("secret-password"), TestContext.CancellationToken);
        server.Dispose();

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connection.StartTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.HandshakeFailed, exception.Failure);
        Assert.DoesNotContain("secret-password", exception.Message);
        Assert.IsFalse(connection.IsSecured);
    }

    [TestMethod]
    public async Task StartTlsAsync_Cancelled_ClosesTheConnectionAndThrowsCancellation()
    {
        var (sockets, _) = SocketsToServer();
        var connector = new TlsClientConnector([], Time, sockets);
        await using var connection = await connector.ConnectAsync("localhost", 636, false, Timeout, TestContext.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connection.StartTlsAsync(cancelled.Token).AsTask());
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionRefused, TlsClientFailure.Refused)]
    [DataRow(SocketError.HostUnreachable, TlsClientFailure.Unreachable)]
    public async Task ConnectAsync_EveryAddressFails_ReportsTheLastFailureDistinctly(SocketError error, TlsClientFailure expected)
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("::1", "127.0.0.1", "::ffff:127.0.0.1"),
            Connect = (_, _) => throw new SocketException((int)error),
        };
        var connector = new TlsClientConnector([], Time, sockets);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("localhost", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(expected, exception.Failure);
        Assert.HasCount(2, sockets.ConnectTargets);
    }

    [TestMethod]
    public async Task ConnectAsync_NameDoesNotResolve_IsHostNotFound()
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = (_, _) => throw new SocketException((int)SocketError.HostNotFound),
        };
        var connector = new TlsClientConnector([], Time, sockets);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("nowhere.invalid", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.HostNotFound, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_NameHasNoAddresses_IsHostNotFound()
    {
        var connector = new TlsClientConnector([], Time, new FakeOriginSockets { Resolve = Resolves() });

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(
            () => connector.ConnectAsync("empty.example", 636, true, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(TlsClientFailure.HostNotFound, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_HostDoesNotAcceptInTime_IsTimedOutDistinctFromRefused()
    {
        var time = new ManualTimeProvider();
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("127.0.0.1"),
            Connect = async (_, cancellationToken) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            },
        };
        var connector = new TlsClientConnector([], time, sockets);

        var connect = connector.ConnectAsync("127.0.0.1", 636, true, Timeout, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Timeout);

        var exception = await Assert.ThrowsExactlyAsync<TlsClientException>(() => connect);
        Assert.AreEqual(TlsClientFailure.TimedOut, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancels_ThrowsCancellationNotTimedOut()
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("127.0.0.1"),
            Connect = async (_, cancellationToken) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            },
        };
        var connector = new TlsClientConnector([], Time, sockets);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => connector.ConnectAsync("127.0.0.1", 636, true, Timeout, cancelled.Token).AsTask());
    }

    [TestMethod]
    public async Task ConnectAsync_BadArguments_AreRefused()
    {
        var connector = new TlsClientConnector([], Time, new FakeOriginSockets());

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => connector.ConnectAsync("", 636, true, Timeout, TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => connector.ConnectAsync("h", 0, true, Timeout, TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => connector.ConnectAsync("h", 65536, true, Timeout, TestContext.CancellationToken).AsTask());
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsClientConnector(null!, Time));
    }

    [TestMethod]
    public void Validate_NoCertificate_IsHandshakeFailed()
    {
        var check = new TlsServerIdentityCheck([], Time);

        var accepted = check.Validate(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.IsFalse(accepted);
        Assert.AreEqual(TlsClientFailure.HandshakeFailed, check.Refusal);
    }

    [TestMethod]
    public void Validate_ChainErrorsWithAGivenCertificateAndNoPresentedChain_BuildsFromTheGivenCertificate()
    {
        using var certificate = ServerCertificate("localhost");
        var check = new TlsServerIdentityCheck([certificate], Time);

        var accepted = check.Validate(this, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsTrue(accepted);
        Assert.IsNull(check.Refusal);
    }

    [TestMethod]
    public void Validate_NoErrors_Accepts()
    {
        var check = new TlsServerIdentityCheck([], Time);

        Assert.IsTrue(check.Validate(this, TestCertificates.CreateRsaServerCertificate(), null, SslPolicyErrors.None));
        Assert.IsNull(check.Refusal);
    }

    [TestMethod]
    public void Validate_OnlyNameMismatch_IsCertificateNameMismatch()
    {
        var check = new TlsServerIdentityCheck([], Time);

        var accepted = check.Validate(this, TestCertificates.CreateRsaServerCertificate(), null, SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.IsFalse(accepted);
        Assert.AreEqual(TlsClientFailure.CertificateNameMismatch, check.Refusal);
    }

    [TestMethod]
    public async Task ConnectAsync_RealLoopbackSocket_ShakesHandsAndCarriesBytes()
    {
        using var certificate = ServerCertificate("localhost");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(TestContext.CancellationToken);
            await using var tls = new SslStream(client.GetStream());
            await tls.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions { ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 },
                TestContext.CancellationToken);
            await Echo(tls, 4);
        }, TestContext.CancellationToken);
        var connector = new TlsClientConnector([certificate]);

        await using var connection = await connector.ConnectAsync("localhost", port, true, Timeout, TestContext.CancellationToken);
        await connection.Stream.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        var echoed = new byte[4];
        await connection.Stream.ReadExactlyAsync(echoed, TestContext.CancellationToken);
        await serving;

        Assert.AreEqual("ping", Encoding.ASCII.GetString(echoed));
    }

    private (FakeOriginSockets Sockets, InMemoryDuplexStream Server) SocketsToServer()
    {
        var transport = InMemoryDataTransport.Create(ServerEnd, ServerEnd);

        return (
            new FakeOriginSockets
            {
                Resolve = Resolves("127.0.0.1"),
                Connect = (_, _) => Task.FromResult(transport.Transport),
            },
            transport.Client);
    }

    private Task ServeAsync(Stream pipe, X509Certificate2 certificate, Func<SslStream, Task> afterHandshake) =>
        Task.Run(async () =>
        {
            await using var tls = new SslStream(pipe, leaveInnerStreamOpen: true);
            await tls.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions { ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 },
                TestContext.CancellationToken);
            await afterHandshake(tls);
        }, TestContext.CancellationToken);

    private async Task Echo(Stream stream, int count)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, TestContext.CancellationToken);
        await stream.WriteAsync(buffer, TestContext.CancellationToken);
        await stream.FlushAsync(TestContext.CancellationToken);
    }

    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolves(params string[] addresses) =>
        (_, _) => Task.FromResult(addresses.Select(IPAddress.Parse).ToArray());

    /// <summary>A self-signed server certificate naming <paramref name="dnsName"/> (and 127.0.0.1), with an exportable key.</summary>
    private static X509Certificate2 ServerCertificate(string dnsName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={dnsName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(dnsName);
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([TestCertificates.ServerAuthenticationUsage], false));
        using var selfSigned = request.CreateSelfSigned(TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));

        return X509CertificateLoader.LoadPkcs12(
            selfSigned.Export(X509ContentType.Pkcs12),
            password: null,
            ServerCertificateImport.KeyStorageFlagsFor(OperatingSystem.IsLinux()) | X509KeyStorageFlags.Exportable);
    }
}
