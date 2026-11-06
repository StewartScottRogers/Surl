using System.Diagnostics;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Runs a driver of the pinned upstream <c>libcurl-4.dll</c>'s API through <c>dotnet run</c> -
/// <see cref="WebSocketScript"/> (ADR-0071 decision 10) or <see cref="RtspScript"/> (ADR-0074
/// decision 12) - and returns what it printed, one line per libcurl call or step. Inconclusive
/// off Windows, whose pinned builds carry no shared libcurl, and wherever the driver reports no
/// pinned library installed.
/// </summary>
internal static class PinnedLibcurlDriver
{
    /// <summary>The driver of libcurl's WebSocket API, <c>curl_ws_send</c> and <c>curl_ws_recv</c>.</summary>
    public const string WebSocketScript = "Run-LibcurlWebSocketScript.cs";

    /// <summary>The driver of libcurl's RTSP requests and interleaved receive.</summary>
    public const string RtspScript = "Run-LibcurlRtspScript.cs";

    // The driver's exit code when no libcurl is pinned or installed for this platform.
    private const int NoPinnedLibrary = 4;

    // dotnet run builds the file-based app on first use; one run at a time keeps parallel tests
    // from building it over each other.
    private static readonly SemaphoreSlim OneRunAtATime = new(1, 1);

    /// <summary>
    /// Runs the driver <paramref name="script"/> with <paramref name="arguments"/> (its options,
    /// the URL, then the steps), asserting it ran every step, and returns its standard output's lines.
    /// </summary>
    public static async Task<IReadOnlyList<string>> RunAsync(TestContext testContext, string script, params string[] arguments)
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
            if (driver.ExitCode == NoPinnedLibrary)
            {
                Assert.Inconclusive(error);
            }

            Assert.AreEqual(0, driver.ExitCode, $"{output}{error}");
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        finally
        {
            OneRunAtATime.Release();
        }
    }
}
