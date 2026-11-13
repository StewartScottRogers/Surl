using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Cryptography.Srp;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Networking;

/// <summary>
/// TLS-SRP on a <see cref="StreamConnection"/> (ADR-0122 decision 5): settings from
/// <see cref="ServerTlsSettings.ForTlsSrp"/> make <see cref="ServerTlsHandshake"/> run
/// <see cref="TlsSrpServer"/> instead of <see cref="SslStream"/>. The client is upstream curl
/// 8.21.0's own recorded bytes (Cryptography/Surl.TlsSrp.UnitTests/Fixtures/README.md), replayed
/// against the account and random bytes the recording's server used, so no socket is opened.
/// </summary>
[TestClass]
public sealed class StreamConnectionTlsSrpTests
{
    /// <summary>The plaintext request inside the recording, as BL-566's relay forwarded it.</summary>
    public static readonly byte[] RecordedRequest =
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18566\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8.ToArray();

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 443);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UpgradeToTlsAsync_RecordedUpstreamCurlJsmithAbc_CompletesWithTheSrpSessionAndCarriesTheRequest()
    {
        var (connection, client) = Create(RecordedSettings());
        await using var _ = connection;
        await client.WriteAsync(Recording("CurlTlsSrpJsmithAbc"), TestContext.CancellationToken);

        var session = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        var request = await ReadAllAsync(connection);

        Assert.AreEqual(SslProtocols.Tls12, session.Protocol);
        Assert.AreEqual(TlsCipherSuite.TLS_SRP_SHA_WITH_AES_256_CBC_SHA, session.CipherSuite);
        Assert.AreEqual("http/1.1", session.ApplicationProtocol);
        Assert.IsNull(session.ServerName);
        Assert.IsNull(session.ClientCertificate);
        Assert.AreEqual("jsmith", session.SrpUserName);
        Assert.AreSame(session, connection.TlsSession);
        Assert.AreEqual(Encoding.ASCII.GetString(RecordedRequest), Encoding.ASCII.GetString(request));
    }

    [TestMethod]
    public async Task CompleteWritesAsync_TlsSrpConnection_SendsTheReplyThenCloseNotifyThenFin()
    {
        var (connection, client) = Create(RecordedSettings());
        await using var _ = connection;
        await client.WriteAsync(Recording("CurlTlsSrpJsmithAbc"), TestContext.CancellationToken);
        await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        await ReadAllAsync(connection);

        await connection.WriteAsync("FINE\n"u8.ToArray(), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        var records = RecordTypes(await ReadToEndAsync(client));
        CollectionAssert.AreEqual(new byte[] { 0x16, 0x14, 0x16, 0x17, 0x15 }, records);
    }

    [TestMethod]
    [DataRow("CurlTlsSrpJsmithBadpass", "wrong password for 'jsmith'")]
    [DataRow("CurlTlsSrpBaduserBadpass", "unknown user 'baduser'")]
    public async Task UpgradeToTlsAsync_RecordedUpstreamCurlWrongPasswordOrUnknownUser_ThrowsTlsHandshakeExceptionSayingWhichAfterBadRecordMac(
        string caseName, string which)
    {
        var (connection, client) = Create(RecordedSettings());
        await using var _ = connection;
        await client.WriteAsync(Recording(caseName), TestContext.CancellationToken);
        client.CompleteWrites();

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.IsInstanceOfType<TlsAlertException>(failure.InnerException);
        StringAssert.StartsWith(failure.Message, $"The TLS-SRP login failed: {which}. ");
        Assert.IsNull(connection.TlsSession);
        await connection.DisposeAsync();
        var written = await ReadToEndAsync(client);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020214"), written[^7..]);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_ClientClosesDuringTheTlsSrpHandshake_ThrowsTlsHandshakeException()
    {
        var (connection, client) = Create(RecordedSettings());
        await using var _ = connection;
        client.CompleteWrites();

        var failure = await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        Assert.IsInstanceOfType<EndOfStreamException>(failure.InnerException);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_SettingsWithAnRsaCertificate_ChoosesTheRsaSignedSuiteFromTheRecordedClientHello()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=srp.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var settings = RecordedSettings(new TlsSrpServerCertificate([certificate.RawData], key));
        var (connection, client) = Create(settings);
        await using var _ = connection;
        var clientHello = Recording("CurlTlsSrpJsmithAbc");
        var firstRecordLength = 5 + ((clientHello[3] << 8) | clientHello[4]);
        await client.WriteAsync(clientHello.AsMemory(0, firstRecordLength), TestContext.CancellationToken);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        var header = await ReadExactlyAsync(client, 5);
        var serverHello = await ReadExactlyAsync(client, (header[3] << 8) | header[4]);
        client.CompleteWrites();

        Assert.AreEqual(0x16, header[0]);
        Assert.AreEqual(2, serverHello[0]);
        var suiteOffset = 4 + 2 + 32 + 1 + serverHello[4 + 2 + 32];
        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x21 }, serverHello[suiteOffset..(suiteOffset + 2)]);
        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => upgrade);
    }

    private async Task<byte[]> ReadExactlyAsync(InMemoryDuplexStream client, int count)
    {
        var buffer = new byte[count];
        await client.ReadExactlyAsync(buffer, TestContext.CancellationToken);
        return buffer;
    }

    /// <summary>
    /// The settings the recording's server had:<c>jsmith</c> with <c>abc</c>, salt <c>10 11 ... 1F</c>,
    /// RFC 5054's 2048-bit group, a zero simulation secret, and every random byte <c>20 21 ... 3F</c> repeated.
    /// </summary>
    public static ServerTlsSettings RecordedSettings(TlsSrpServerCertificate? certificate = null)
    {
        var salt = Sequence(0x10, 16);
        var group = SrpGroup.Rfc5054Group2048;
        var verifier = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(group, salt, "jsmith"u8, "abc"u8));
        var accounts = new TlsSrpAccounts([new SrpVerifierEntry("jsmith", salt, verifier, group)], new byte[TlsSrpAccounts.SimulationSecretLength]);
        return ServerTlsSettings.ForTlsSrp(accounts, new RepeatingRandomNumberGenerator(Sequence(0x20, 32)), certificate);
    }

    private static (StreamConnection Connection, InMemoryDuplexStream Client) Create(ServerTlsSettings settings)
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        var handshake = ServerTlsHandshake.ForListener(settings, new ListenUrl("https", "127.0.0.1", 443));
        return (new StreamConnection(server, Local, Remote, new CompletingTransportControl(server), handshake), client);
    }

    private static byte[] Recording(string caseName)
    {
        using var resource = typeof(StreamConnectionTlsSrpTests).Assembly.GetManifestResourceStream($"TlsSrp/{caseName}/client.bin")
            ?? throw new InvalidOperationException($"No recording {caseName}.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    private async Task<byte[]> ReadAllAsync(StreamConnection connection)
    {
        var received = new List<byte>();
        var buffer = new byte[256];
        int count;
        while ((count = await connection.ReadAsync(buffer, TestContext.CancellationToken)) > 0)
        {
            received.AddRange(buffer.AsSpan(0, count));
        }

        return [.. received];
    }

    private async Task<byte[]> ReadToEndAsync(InMemoryDuplexStream client)
    {
        using var copy = new MemoryStream();
        await client.CopyToAsync(copy, TestContext.CancellationToken);
        return copy.ToArray();
    }

    private static byte[] RecordTypes(byte[] written)
    {
        var types = new List<byte>();
        for (var offset = 0; offset < written.Length; offset += 5 + ((written[offset + 3] << 8) | written[offset + 4]))
        {
            types.Add(written[offset]);
        }

        return [.. types];
    }

    private static byte[] Sequence(int first, int count) => Enumerable.Range(first, count).Select(value => (byte)value).ToArray();

    private sealed class RepeatingRandomNumberGenerator(byte[] pattern) : RandomNumberGenerator
    {
        public override void GetBytes(byte[] data)
        {
            for (var index = 0; index < data.Length; index++)
            {
                data[index] = pattern[index % pattern.Length];
            }
        }
    }

    private sealed class CompletingTransportControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public void ShutdownSend() => stream.CompleteWrites();

        public void ResetAndClose() => stream.Dispose();
    }
}
