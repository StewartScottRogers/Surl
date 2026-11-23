using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl 8.21.0 downloads a file that <c>drop;after-bytes=N;close=graceful</c> or
/// <c>close=reset</c> cuts short (ADR-0188 decision 2, measured by BL-1618): HTTP/1.1 with a <c>Content-Length</c>, FTP
/// <c>RETR</c>, and each over TLS. HTTP/1.1 chunked cannot be served by surl's own file server (it answers files with a
/// <c>Content-Length</c>), so that case is cut by a loopback server in this file that does what the graceful cut does to the
/// bytes: sends a prefix, then closes (with <c>close_notify</c> over TLS). Each test pins curl's exit
/// code and that the bytes it kept are a non-empty strict prefix of the file. Inconclusive where no pinned build is
/// installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlMeetsASurlCutDownloadTests
{
    // CURLE_PARTIAL_FILE and CURLE_RECV_ERROR, measured 2026-11-23 with curl 8.21.0 (ADR-0188 amendment 1).
    private const int PartialFile = 18;
    private const int ReceiveFailure = 56;
    private const int FileLength = 40_000;
    private const long CutAfterBytes = 12_000;
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("graceful", "http")]
    [DataRow("reset", "http")]
    [DataRow("graceful", "https")]
    [DataRow("reset", "https")]
    [DataRow("graceful", "ftp")]
    [DataRow("reset", "ftp")]
    [DataRow("graceful", "ftps")]
    [DataRow("reset", "ftps")]
    public async Task Download_CutAfterBytes_ExitsAsMeasured(string close, string scheme)
    {
        var content = Content();
        var options = new SurlServerOptions { RecordExchanges = true, AllowAnonymous = true, SelfSigned = scheme is "https" or "ftps" };
        options.ListenUrls.Add($"{scheme}://127.0.0.1:0");
        options.Files["big.bin"] = content;
        options.Faults.Add(SurlFault.DropConnection() with
        {
            AfterBytes = CutAfterBytes,
            Close = close == "graceful" ? FaultClose.Graceful : FaultClose.Reset,
        });
        await using var server = await SurlServer.StartAsync(options, TestContext.CancellationToken);
        var bound = server.BoundUrls.Single();

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-k", "-o", "-", $"{scheme}://{bound.Host}:{bound.Port}/big.bin");
        await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, WaitLimit, TestContext.CancellationToken);

        AssertKeptAPrefix(content, result.StandardOutput, mustKeepSome: close == "graceful");
        Assert.AreEqual(close == "graceful" ? PartialFile : ReceiveFailure, result.ExitCode, $"{scheme} {close}: kept {result.StandardOutput.Length}; {result.StandardError}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DownloadChunked_ClosedGracefullyBeforeTheLastChunk_ExitsPartialFile(bool overTls)
    {
        var content = Content();
        using var authority = TestCertificateAuthority.Create();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = ServeChunkedAsync(listener, content, graceful: true, overTls ? authority : null);
        var scheme = overTls ? "https" : "http";

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-o", "-", $"{scheme}://127.0.0.1:{port}/big.bin");
        await served;

        AssertKeptAPrefix(content, result.StandardOutput, mustKeepSome: true);
        Assert.AreEqual(PartialFile, result.ExitCode, $"{scheme} chunked graceful: kept {result.StandardOutput.Length}; {result.StandardError}");
    }

    private static void AssertKeptAPrefix(byte[] content, byte[] kept, bool mustKeepSome)
    {
        Assert.IsTrue((kept.Length > 0 || !mustKeepSome) && kept.Length < content.Length, $"kept {kept.Length} of {content.Length}");
        CollectionAssert.AreEqual(content.AsSpan(0, kept.Length).ToArray(), kept);
    }

    private static byte[] Content() => [.. Enumerable.Range(0, FileLength).Select(index => (byte)('a' + (index % 26)))];

    private async Task ServeChunkedAsync(TcpListener listener, byte[] content, bool graceful, TestCertificateAuthority? authority)
    {
        using var client = await listener.AcceptTcpClientAsync(TestContext.CancellationToken);
        Stream stream = client.GetStream();
        SslStream? tls = null;
        if (authority is not null)
        {
            tls = new SslStream(stream);
            using var pem = X509Certificate2.CreateFromPemFile(authority.ServerCertificateFile, authority.ServerKeyFile);
            using var certificate = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, TestContext.CancellationToken);
            stream = tls;
        }

        var buffer = new byte[4096];
        var request = new StringBuilder();
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, TestContext.CancellationToken);
            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Type: application/octet-stream\r\n\r\n"), TestContext.CancellationToken);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"{CutAfterBytes:x}\r\n"), TestContext.CancellationToken);
        await stream.WriteAsync(content.AsMemory(0, (int)CutAfterBytes), TestContext.CancellationToken);
        await stream.FlushAsync(TestContext.CancellationToken);
        if (graceful)
        {
            if (tls is not null)
            {
                await tls.ShutdownAsync();
            }

            client.Client.Shutdown(SocketShutdown.Send);
            _ = await stream.ReadAsync(buffer, TestContext.CancellationToken);
        }
        else
        {
            client.Client.LingerState = new LingerOption(true, 0);
        }
    }
}
