namespace Surl.Conformance;

/// <summary>
/// One client's run of a Phase 7 case, as compared: what the client produced and what its own
/// <c>surl</c> shows. Built from an <see cref="UpstreamCurlRunResult"/> or a
/// <see cref="CurlPortClientRunResult"/>, never from a path, so the two runners stay apart
/// (ADR-0083 decision 2).
/// </summary>
/// <param name="ExitCode">The client's exit code, or <see langword="null"/> when it was stopped at its timeout.</param>
/// <param name="StandardOutput">The bytes the client wrote to stdout, unchanged.</param>
/// <param name="StandardError">The text the client wrote to stderr, unchanged.</param>
/// <param name="Surl">What the run's own <c>surl</c> shows.</param>
public sealed record CurlPortMeasuredRun(int? ExitCode, byte[] StandardOutput, string StandardError, CurlPortSurlSide Surl)
{
    /// <summary>
    /// Gets upstream curl's run as compared.
    /// </summary>
    /// <param name="result">What the pinned upstream build produced.</param>
    /// <param name="surl">What its <c>surl</c> shows.</param>
    /// <returns>The run.</returns>
    public static CurlPortMeasuredRun Of(UpstreamCurlRunResult result, CurlPortSurlSide surl)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(surl);
        return new(result.ExitCode, result.StandardOutput, result.StandardError, surl);
    }

    /// <summary>
    /// Gets the Curl port's run as compared.
    /// </summary>
    /// <param name="result">What the pinned Curl port client produced.</param>
    /// <param name="surl">What its <c>surl</c> shows.</param>
    /// <returns>The run.</returns>
    public static CurlPortMeasuredRun Of(CurlPortClientRunResult result, CurlPortSurlSide surl)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(surl);
        return new(result.ExitCode, result.StandardOutput, result.StandardError, surl);
    }
}
