namespace Surl.Protocol.Abstractions;

/// <summary>
/// The exit codes the <c>surl</c> process returns.
/// </summary>
/// <remarks>
/// Only the values the Phase 0 placeholder executable needs exist yet. Phase 1 decides the
/// full table in an ADR, reusing upstream curl's <c>CURLE_*</c> number wherever a
/// server-side meaning carries over (https://curl.se/libcurl/c/libcurl-errors.html, curl
/// 8.21.0). A value is never renumbered once it exists.
/// </remarks>
public enum SurlExitCode
{
    /// <summary>
    /// Success. The same number as upstream curl's <c>CURLE_OK</c>.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// Surl could not start. The same number and meaning as upstream curl's
    /// <c>CURLE_FAILED_INIT</c>; the Phase 0 placeholder returns it for every command line.
    /// </summary>
    FailedInit = 2,
}
