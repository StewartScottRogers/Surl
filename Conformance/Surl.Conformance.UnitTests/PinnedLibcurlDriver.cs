using System.Diagnostics;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Runs a driver of the pinned upstream <c>libcurl-4.dll</c>'s API through <c>dotnet run</c> -
/// <see cref="WebSocketScript"/> (ADR-0071 decision 10) or <see cref="RtspScript"/> (ADR-0074
/// decision 12) - and returns what it printed, one line per libcurl call or step; or, for Phase 7
/// (ADR-0089 decision 1 (c)), the Curl port's twin of one (<see cref="CurlPortTwinOf"/>), calling the
/// port's pinned managed <c>Libcurl</c>. Inconclusive off Windows, whose pinned builds carry no shared
/// libcurl and where no port library is pinned, and wherever the driver reports no pinned library installed.
/// </summary>
internal static class PinnedLibcurlDriver
{
    /// <summary>The driver of libcurl's WebSocket API, <c>curl_ws_send</c> and <c>curl_ws_recv</c>.</summary>
    public const string WebSocketScript = "Run-LibcurlWebSocketScript.cs";

    /// <summary>The driver of libcurl's RTSP requests and interleaved receive.</summary>
    public const string RtspScript = "Run-LibcurlRtspScript.cs";

    /// <summary>The environment variable naming the port <c>Libcurl.dll</c> a port twin compiles against.</summary>
    public const string CurlPortLibcurlVariable = "SURL_CURL_PORT_LIBCURL";

    // dotnet run builds the file-based app on first use; one run at a time keeps parallel tests
    // from building it over each other.
    private static readonly SemaphoreSlim OneRunAtATime = new(1, 1);

    /// <summary>
    /// The Curl port's twin of an upstream driver, which takes the same arguments and prints the same
    /// report: <c>Run-CurlPortLibcurlWebSocketScript.cs</c> or <c>Run-CurlPortLibcurlRtspScript.cs</c>.
    /// </summary>
    public static string CurlPortTwinOf(string script) =>
        script.Replace("Run-Libcurl", "Run-CurlPortLibcurl", StringComparison.Ordinal);

    /// <summary>
    /// Runs the upstream driver <paramref name="script"/> with <paramref name="arguments"/> (its options,
    /// the URL, then the steps), asserting it ran every step, and returns its standard output's lines.
    /// </summary>
    public static async Task<IReadOnlyList<string>> RunAsync(TestContext testContext, string script, params string[] arguments)
    {
        var run = await RunScriptAsync(testContext, script, new Dictionary<string, string>(), arguments);
        Assert.AreEqual(0, run.ExitCode, $"{string.Join('\n', run.Lines)}\n{run.StandardError}");
        return run.Lines;
    }

    /// <summary>
    /// Runs <paramref name="script"/> - an upstream driver, or a port twin with <paramref name="environment"/>
    /// naming <see cref="CurlPortLibcurlVariable"/> - and returns how it exited and what it printed, whatever
    /// the exit code, except the one saying no pinned library is installed, which makes the test Inconclusive.
    /// </summary>
    public static async Task<(int ExitCode, IReadOnlyList<string> Lines, string StandardError)> RunScriptAsync(
        TestContext testContext, string script, IReadOnlyDictionary<string, string> environment, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only the Windows reference build ships a shared libcurl to pin (ADR-0071 decision 10, ADR-0074 decision 12).");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = PinnedUpstreamCurl.RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in (string[])["run", "--file", script, "--", .. arguments])
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        await OneRunAtATime.WaitAsync(testContext.CancellationToken);
        try
        {
            using var driver = Process.Start(startInfo)!;
            var standardOutput = driver.StandardOutput.ReadToEndAsync(testContext.CancellationToken);
            var standardError = driver.StandardError.ReadToEndAsync(testContext.CancellationToken);
            try
            {
                // The first run builds the driver, so the deadline is generous.
                await driver.WaitForExitAsync(testContext.CancellationToken).WaitAsync(TimeSpan.FromMinutes(5), testContext.CancellationToken);
            }
            catch (TimeoutException)
            {
                driver.Kill(entireProcessTree: true);
                throw;
            }

            var output = await standardOutput;
            var error = await standardError;

            testContext.WriteLine($"{script} {string.Join(' ', arguments)}: exit {driver.ExitCode}\n{output}{error}");
            if (driver.ExitCode == PinnedLibcurlChoice.InconclusiveExitCode)
            {
                Assert.Inconclusive(error);
            }

            return (driver.ExitCode, output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error);
        }
        finally
        {
            OneRunAtATime.Release();
        }
    }
}
