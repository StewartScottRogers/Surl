using System.Net;
using System.Net.Sockets;

namespace Surl.Conformance;

/// <summary>
/// <see cref="SurlOnLoopback"/>'s published-binary run (ADR-0085, decision 2): a missing
/// <c>SURL_EXECUTABLE</c> fails at once naming the path, and a surl process is started, read and
/// stopped. The process tests start <c>surl.dll</c> from the test output folder through the
/// dotnet host, which stands in for a published binary because both take the same arguments.
/// </summary>
[TestClass]
public sealed class SurlOnLoopbackTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task StartPublishedAsync_MissingExecutable_FailsNamingThePath()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"surl-missing-{Guid.NewGuid():N}", "surl");

        var thrown = await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => SurlOnLoopback.StartPublishedAsync(missing, null, ["http://127.0.0.1:0/"], TestContext.CancellationToken));

        Assert.Contains(missing, thrown.Message);
        Assert.Contains(PublishedSurlProcess.ExecutableVariable, thrown.Message);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task StartPublishedAsync_Process_ListensAndStopsOnDispose()
    {
        var surl = await SurlOnLoopback.StartPublishedAsync(
            DotnetHost(), null, ["exec", SurlDll(), "-v", "http://127.0.0.1:0/"], TestContext.CancellationToken);
        var baseUrl = surl.BaseUrl;

        await surl.DisposeAsync();

        Assert.AreEqual("http", baseUrl.Scheme);
        Assert.AreNotEqual(0, baseUrl.Port);
        using var client = new TcpClient();
        await Assert.ThrowsAsync<SocketException>(() => client.ConnectAsync(IPAddress.Loopback, baseUrl.Port, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task StartPublishedAsync_ProcessExitsBeforeListening_FailsWithItsExitCodeAndError()
    {
        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => SurlOnLoopback.StartPublishedAsync(
                DotnetHost(), null, ["exec", SurlDll(), "--no-such-option", "http://127.0.0.1:0/"], TestContext.CancellationToken));

        Assert.StartsWith("surl exited ", thrown.Message);
        Assert.Contains("no-such-option", thrown.Message);
    }

    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host
            ? host
            : throw new AssertInconclusiveException("DOTNET_HOST_PATH is not set, so there is no dotnet host to start surl.dll with.");

    private static string SurlDll() => Path.Combine(AppContext.BaseDirectory, "surl.dll");
}
