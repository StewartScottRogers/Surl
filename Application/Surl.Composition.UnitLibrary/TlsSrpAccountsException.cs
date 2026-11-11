using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The TLS-SRP accounts could not be composed at start (ADR-0122 decisions 4 and 6): a
/// <c>--srp-verifiers</c> or <c>--srp-groups</c> file that cannot be read or holds a malformed line
/// (<see cref="SurlExitCode.CouldNotReadFile"/>), or a user given twice
/// (<see cref="SurlExitCode.FailedInit"/>).
/// </summary>
/// <param name="exitCode">The exit code the start ends with.</param>
/// <param name="message">The message after the <c>surl: </c> prefix, starting with the exit code in parentheses.</param>
public sealed class TlsSrpAccountsException(SurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the start ends with.</summary>
    public SurlExitCode ExitCode { get; } = exitCode;
}
