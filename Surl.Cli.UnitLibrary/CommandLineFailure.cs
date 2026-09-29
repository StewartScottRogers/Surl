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
public sealed record CommandLineFailure(SurlExitCode ExitCode, string Message);
