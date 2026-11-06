using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A start <c>surl</c> would refuse (ADR-0099, decision 5): the exit code <c>surl</c> would exit
/// with and each line it would write after <c>surl: </c>. Nothing is left bound or locked.
/// </summary>
public sealed class SurlServerStartException : Exception
{
    internal SurlServerStartException(SurlStartFailure failure)
        : base(string.Join("\n", failure.Lines))
    {
        ExitCode = failure.ExitCode;
        Lines = failure.Lines;
    }

    /// <summary>The exit code <c>surl</c> would exit with.</summary>
    public SurlExitCode ExitCode { get; }

    /// <summary>Each line <c>surl</c> would write after <c>surl: </c>, in order; <see cref="Exception.Message"/> joins them with <c>\n</c>.</summary>
    public IReadOnlyList<string> Lines { get; }
}
