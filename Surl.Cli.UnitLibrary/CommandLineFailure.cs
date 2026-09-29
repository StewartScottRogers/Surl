using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Why a command line was refused: the exit code <c>surl</c> ends with and the message it
/// writes to stderr (ADR-0007, section 5).
/// </summary>
/// <param name="ExitCode">The exit code the refusal ends <c>surl</c> with.</param>
/// <param name="Message">
/// The stderr text after the <c>surl: </c> prefix, which the caller writes
/// (<c>(3) URL rejected: No host part in the URL</c>).
/// </param>
/// <param name="FollowedByTryHelpLine">
/// <see langword="true"/> when the caller writes <see cref="TryHelpLine"/> after the message,
/// on its own line with the same <c>surl: </c> prefix: every command-line error of
/// ADR-0007 section 5, and no listen-URL refusal.
/// </param>
public sealed record CommandLineFailure(SurlExitCode ExitCode, string Message, bool FollowedByTryHelpLine = false)
{
    /// <summary>
    /// The line written after a command-line error, after the <c>surl: </c> prefix (ADR-0007
    /// section 5), as upstream curl writes <c>curl: try 'curl --help' or 'curl --manual' …</c>
    /// after its own (ADR-0034 decision 6).
    /// </summary>
    public const string TryHelpLine = "try 'surl --help' or 'surl --manual' for more information";
}
