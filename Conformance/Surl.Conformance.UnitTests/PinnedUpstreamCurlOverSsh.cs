namespace Surl.Conformance;

/// <summary>
/// Runs the pinned upstream curl build against a live <c>surl scp://</c> or <c>sftp://</c> with
/// <c>-sS</c>, <c>HOME</c> and <c>USERPROFILE</c> pointed at an <see cref="IsolatedCurlHome"/>
/// (ADR-0051 decision 8), and, when curl exits other than 0, surl's log written to the test's
/// log beside curl's result, so a failed exchange shows both sides.
/// </summary>
internal static class PinnedUpstreamCurlOverSsh
{
    /// <summary>Runs the pinned build with <c>-sS</c> and <paramref name="arguments"/>.</summary>
    public static async Task<UpstreamCurlRunResult> RunAsync(
        TestContext testContext, IsolatedCurlHome curlHome, SurlOnLoopback surl, params string[] arguments)
    {
        var result = await PinnedUpstreamCurl.RunWithEnvironmentAsync(testContext, curlHome.Environment, ["-sS", .. arguments]);
        if (result.ExitCode != 0)
        {
            testContext.WriteLine($"curl's stderr: {result.StandardError}");
            testContext.WriteLine($"surl's log:\n{surl.Log}");
        }

        return result;
    }
}
