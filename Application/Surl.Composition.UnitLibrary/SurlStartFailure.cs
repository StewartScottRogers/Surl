using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A start that ends <c>surl</c> (ADR-0007, section 5): the exit code, and each line written after
/// the <c>surl: </c> prefix. A start stopped by cancellation before serving began is
/// <see cref="SurlExitCode.Ok"/> with no lines.
/// </summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Lines">Each line as written after <c>surl: </c>, in order.</param>
public sealed record SurlStartFailure(SurlExitCode ExitCode, IReadOnlyList<string> Lines);
