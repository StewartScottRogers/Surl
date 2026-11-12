using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Numerics;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Surl.Composition;
using Surl.Cryptography.Srp;
using Surl.Networking;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> serves HTTPS over TLS-SRP (ADR-0122 decisions 4 to 6; BL-567).
/// The client is upstream curl 8.21.0's own recorded bytes for
/// <c>-k --tlsauthtype SRP --tlsuser jsmith --tlspassword abc https://...</c>
/// (Cryptography/Surl.TlsSrp.UnitTests/Fixtures/README.md), replayed through the TLS-SRP accounts
/// the command line composed - read from <c>--srp-verifiers</c> and <c>--srp-groups</c> files that hold
/// the recording's salt and verifier - with the recording's server random bytes, so curl's
/// <c>Finished</c> verifies only if the accounts are right. The test decrypts the reply from the
/// SRP math's server side, with no socket.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerTlsSrpTests
{
    private const string VerifierFile = "srp-verifier-db";
    private const string GroupFile = "srp-verifier-conf";
    private const int MacLength = 20;
    private const int KeyLength = 32;
    private const int IvLength = 16;

    private static readonly string NewLine = Environment.NewLine;
    private static readonly byte[] Salt = Sequence(0x10, 16);
    private static readonly byte[] ServerRandomPattern = Sequence(0x20, 32);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_TlsSrpFromVerifierFiles_CompletesUpstreamCurlsHandshakeAndAnswersItsRequest()
    {
        var client = Recording("CurlTlsSrpJsmithAbc");
        var factory = new ReplayListenerFactory(client);

        var run = await ServeOneConnectionAsync(
            factory, "--tlsauthtype", "SRP", "--srp-verifiers", VerifierFile, "--srp-groups", GroupFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
        Assert.AreEqual($"Listening on https://127.0.0.1:{ReplayListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.IsNotNull(factory.Settings?.TlsSrpAccounts);
        var session = factory.Connection.TlsSession!;
        Assert.AreEqual("jsmith", session.SrpUserName);
        Assert.AreEqual(TlsCipherSuite.TLS_SRP_SHA_WITH_AES_256_CBC_SHA, session.CipherSuite);
        StringAssert.StartsWith(DecryptReply(client, factory.Connection.Written), "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task RunAsync_TlsSrpWithAWrongPassword_EndsTheHandshakeWithBadRecordMacAndKeepsServing()
    {
        var factory = new ReplayListenerFactory(Recording("CurlTlsSrpJsmithBadpass"));

        var run = await ServeOneConnectionAsync(
            factory, "--tlsauthtype", "SRP", "--srp-verifiers", VerifierFile, "--srp-groups", GroupFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
        Assert.IsNull(factory.Connection.TlsSession);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020214"), factory.Connection.Written[^7..]);
    }

    [TestMethod]
    public async Task RunAsync_TlsUserAndPassword_ServesTlsSrpWithNoCertificate()
    {
        var factory = new ReplayListenerFactory([]);

        var run = await ServeOneConnectionAsync(factory, "--tlsuser", "jsmith", "--tlspassword", "abc", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
        Assert.IsNotNull(factory.Settings?.TlsSrpAccounts);
        Assert.AreEqual($"Listening on https://127.0.0.1:{ReplayListenerFactory.BoundPort}/" + NewLine, run.Output);
    }

    [TestMethod]
    [DataRow(VerifierFile, "--srp-verifiers")]
    [DataRow(GroupFile, "--srp-groups")]
    public async Task RunAsync_SrpFileMissing_WritesCouldNotReadAndReturnsCouldNotReadFile(string missing, string option)
    {
        var factory = new ReplayListenerFactory([]);

        var run = await RunAsync(
            factory, path => path == missing ? throw new FileNotFoundException("not found") : ReadSrpFile(path),
            "--tlsauthtype", "SRP", "--srp-verifiers", VerifierFile, "--srp-groups", GroupFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual($"surl: (37) Could not read the {option} file '{missing}': not found" + NewLine, run.Error);
        Assert.IsNull(factory.Settings);
    }

    [TestMethod]
    public async Task RunAsync_SrpVerifierFileWithAMalformedLine_NamesTheFileAndLine()
    {
        var factory = new ReplayListenerFactory([]);

        var run = await RunAsync(
            factory, path => path == VerifierFile ? "jsmith:only-two\n"u8.ToArray() : ReadSrpFile(path),
            "--srp-verifiers", VerifierFile, "--srp-groups", GroupFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        StringAssert.StartsWith(run.Error, $"surl: (37) Could not read the --srp-verifiers file '{VerifierFile}': line 1: ");
    }

    [TestMethod]
    public async Task RunAsync_TlsUserAlsoInTheVerifierFile_IsRefusedAsFailedInit()
    {
        var factory = new ReplayListenerFactory([]);

        var run = await RunAsync(
            factory, ReadSrpFile,
            "--tlsuser", "jsmith", "--tlspassword", "abc", "--srp-verifiers", VerifierFile, "--srp-groups", GroupFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) The TLS-SRP user 'jsmith' is given twice" + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_TlsSrpWithAWebTransportListenUrl_StillNeedsACertificate()
    {
        var factory = new ReplayListenerFactory([]);

        var run = await RunAsync(factory, ReadSrpFile, "--tlsuser", "jsmith", "--tlspassword", "abc", "https://127.0.0.1:0/", "webtransport://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        StringAssert.StartsWith(run.Error, "surl: (58) webtransport://127.0.0.1:0/ needs a certificate");
    }

    // The recording's account as GnuTLS files: jsmith, salt 10 11 ... 1F, abc's verifier in RFC 5054's 2048-bit group.
    private static byte[] ReadSrpFile(string path)
    {
        var group = SrpGroup.Rfc5054Group2048;
        var verifier = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(group, Salt, "jsmith"u8, "abc"u8));
        return path switch
        {
            VerifierFile => Encoding.ASCII.GetBytes($"jsmith:{GnuTlsSrpBase64Encode(verifier)}:{GnuTlsSrpBase64Encode(Salt)}:1\n"),
            GroupFile => Encoding.ASCII.GetBytes($"1:{GnuTlsSrpBase64Encode(Srp6a.ToUnsignedBigEndian(group.Prime))}:{GnuTlsSrpBase64Encode([2])}\n"),
            _ => throw new FileNotFoundException(path),
        };
    }

    // GnuTLS's SRP base64 (lib/auth/srp_sb64.c), written out from its definition: the fewest digits
    // whose length GnuTlsSrpBase64.Decode reads back as the same number of bytes.
    private static string GnuTlsSrpBase64Encode(byte[] bytes)
    {
        int[] leadingGroupByteCounts = [0, 1, 1, 2];
        var length = 1;
        while ((3 * (length / 4)) + leadingGroupByteCounts[length % 4] != bytes.Length || 6 * length < 8 * bytes.Length)
        {
            length++;
        }

        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var digits = new char[length];
        for (var index = length - 1; index >= 0; index--)
        {
            digits[index] = GnuTlsSrpBase64.Alphabet[(int)(value & 63)];
            value >>= 6;
        }

        return new string(digits);
    }

    /// <summary>
    /// Decrypts the application data the server wrote, from the server's side of the SRP math
    /// (RFC 5054 section 2.6) and RFC 5246's PRF, RFC 7627's extended master secret and RFC 7366's
    /// encrypt-then-MAC, which curl's hello asked for.
    /// </summary>
    private static string DecryptReply(byte[] client, byte[] written)
    {
        var clientRecords = Fragments(client);
        var serverRecords = Fragments(written);
        var hello = clientRecords[0].Fragment;
        var keyExchange = clientRecords[1].Fragment;
        var serverFlight = serverRecords[0].Fragment;
        var group = SrpGroup.Rfc5054Group2048;
        var verifier = Srp6a.ComputeVerifier(group, Salt, "jsmith"u8, "abc"u8);
        var serverPrivate = Srp6a.FromUnsignedBigEndian(ServerRandomPattern);
        var clientPublic = Srp6a.FromUnsignedBigEndian(keyExchange.AsSpan(6));
        var serverPublic = Srp6a.ComputeServerPublicValue(group, verifier, serverPrivate);
        var premaster = Srp6a.ToUnsignedBigEndian(Srp6a.ComputePremasterSecret(
            group, clientPublic, verifier, Srp6a.ComputeScrambler(group, clientPublic, serverPublic), serverPrivate));
        var sessionHash = SHA256.HashData([.. hello, .. serverFlight, .. keyExchange]);
        var masterSecret = Prf(premaster, "extended master secret", sessionHash, 48);
        var keyBlock = Prf(masterSecret, "key expansion", [.. serverFlight[6..38], .. hello[6..38]], (2 * MacLength) + (2 * KeyLength));
        using var aes = Aes.Create();
        aes.Key = keyBlock[((2 * MacLength) + KeyLength)..];
        var reply = new List<byte>();
        foreach (var (_, fragment) in serverRecords.Where(record => record.Type == 0x17))
        {
            var plain = aes.DecryptCbc(fragment[IvLength..^MacLength], fragment[..IvLength], PaddingMode.None);
            reply.AddRange(plain[..^(plain[^1] + 1)]);
        }

        return Encoding.ASCII.GetString([.. reply]);
    }

    // RFC 5246 section 5: P_SHA256(secret, label + seed), cut to length.
    private static byte[] Prf(byte[] secret, string label, byte[] seed, int length)
    {
        byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
        var output = new List<byte>();
        var a = labelAndSeed;
        while (output.Count < length)
        {
            a = HMACSHA256.HashData(secret, a);
            byte[] block = [.. a, .. labelAndSeed];
            output.AddRange(HMACSHA256.HashData(secret, block));
        }

        return [.. output.Take(length)];
    }

    private static List<(byte Type, byte[] Fragment)> Fragments(byte[] capture)
    {
        var records = new List<(byte, byte[])>();
        for (var offset = 0; offset < capture.Length;)
        {
            var length = (capture[offset + 3] << 8) | capture[offset + 4];
            records.Add((capture[offset], capture[(offset + 5)..(offset + 5 + length)]));
            offset += 5 + length;
        }

        return records;
    }

    private static byte[] Recording(string caseName)
    {
        using var resource = typeof(CommandLineRunnerTlsSrpTests).Assembly.GetManifestResourceStream($"TlsSrp/{caseName}/client.bin")
            ?? throw new InvalidOperationException($"No recording {caseName}.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[] Sequence(int first, int count) => Enumerable.Range(first, count).Select(value => (byte)value).ToArray();

    // Serves until the one connection is closed, then stops.
    private async Task<Run> ServeOneConnectionAsync(ReplayListenerFactory factory, params string[] args)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var running = RunAsync(factory, ReadSrpFile, stop.Token, args);
        await factory.Connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        return await running;
    }

    private Task<Run> RunAsync(ReplayListenerFactory factory, Func<string, byte[]> readStartFile, params string[] args) =>
        RunAsync(factory, readStartFile, TestContext.CancellationToken, args);

    private static async Task<Run> RunAsync(
        ReplayListenerFactory factory, Func<string, byte[]> readStartFile, CancellationToken cancellationToken, string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        CommandLineRunner runner = new(
            factory.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, readStartFile: readStartFile);

        var exitCode = await runner.RunAsync(args, output, error, cancellationToken);
        return new Run(exitCode, output.ToString(), error.ToString());
    }

    private sealed record Run(SurlExitCode ExitCode, string Output, string Error);

    // Hands out one ReplayConnection, then waits until cancelled.
    private sealed class ReplayListenerFactory(byte[] clientBytes) : IListenerFactory
    {
        public const int BoundPort = 49732;

        private int accepted;

        public ServerTlsSettings? Settings { get; private set; }

        public ReplayConnection Connection { get; private set; } = null!;

        public IListenerFactory Create(ServerTlsSettings? settings)
        {
            Settings = settings;
            Connection = new ReplayConnection(clientBytes, settings);
            return this;
        }

        public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IConnectionListener>(new Listener(listenUrl.WithBoundPort(BoundPort), this));

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            throw new UnreachableException();

        private sealed class Listener(ListenUrl listenUrl, ReplayListenerFactory factory) : IConnectionListener
        {
            public ListenUrl ListenUrl { get; } = listenUrl;

            public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, BoundPort)];

            public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Exchange(ref factory.accepted, 1) == 0)
                {
                    return factory.Connection;
                }

                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new UnreachableException();
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A connection whose client is a replayed byte script and whose TLS upgrade runs
    /// <see cref="TlsSrpServer"/> over the composed accounts with the recording's random bytes.
    /// </summary>
    private sealed class ReplayConnection(byte[] clientBytes, ServerTlsSettings? settings) : IConnection
    {
        private readonly ReplayStream transport = new(clientBytes);
        private Stream stream = null!;

        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, ReplayListenerFactory.BoundPort);

        public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

        public TlsSession? TlsSession { get; private set; }

        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public byte[] Written => transport.Written;

        public async ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
        {
            var server = new TlsSrpServer(settings!.TlsSrpAccounts!, new RepeatingRandomNumberGenerator(ServerRandomPattern));
            TlsSrpSession session;
            try
            {
                session = await server.AuthenticateAsync(transport, cancellationToken);
            }
            catch (Exception exception) when (exception is TlsAlertException or IOException)
            {
                throw new TlsHandshakeException(exception.Message, exception);
            }

            stream = session.Stream;
            TlsSession = new TlsSession(SslProtocols.Tls12, (TlsCipherSuite)(ushort)session.CipherSuite, session.ApplicationProtocol, session.ServerName, null)
            {
                SrpUserName = session.UserName,
            };
            return TlsSession;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => stream.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => stream.WriteAsync(bytes, cancellationToken);

        public async ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
            await ((TlsSrpStream)stream).ShutdownAsync(cancellationToken);

        public void Abort()
        {
        }

        public ValueTask DisposeAsync()
        {
            Disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    // Reads the script, then the end of the stream; keeps every byte written.
    private sealed class ReplayStream(byte[] script) : Stream
    {
        private readonly MemoryStream written = new();
        private int delivered;

        public byte[] Written
        {
            get
            {
                lock (written)
                {
                    return written.ToArray();
                }
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var taken = Math.Min(count, script.Length - delivered);
            script.AsSpan(delivered, taken).CopyTo(buffer.AsSpan(offset));
            delivered += taken;
            return taken;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (written)
            {
                written.Write(buffer, offset, count);
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

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
}
