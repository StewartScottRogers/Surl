using System.Diagnostics.CodeAnalysis;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// What <see cref="ListenUrlParser.Parse"/> made of one argument: a listen URL, or the
/// failure that refuses it. Exactly one of the two is set.
/// </summary>
public sealed class ListenUrlParseResult
{
    private ListenUrlParseResult(ListenUrl? listenUrl, CommandLineFailure? failure)
    {
        ListenUrl = listenUrl;
        Failure = failure;
    }

    /// <summary>The listen URL, or <see langword="null"/> when the argument was refused.</summary>
    public ListenUrl? ListenUrl { get; }

    /// <summary>Why the argument was refused, or <see langword="null"/> when it was accepted.</summary>
    public CommandLineFailure? Failure { get; }

    /// <summary><see langword="true"/> when the argument was accepted as a listen URL.</summary>
    [MemberNotNullWhen(true, nameof(ListenUrl))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool Succeeded => ListenUrl is not null;

    /// <summary>A result carrying an accepted listen URL.</summary>
    /// <param name="listenUrl">The listen URL the argument names.</param>
    /// <returns>The accepted result.</returns>
    public static ListenUrlParseResult Accepted(ListenUrl listenUrl) => new(listenUrl, null);

    /// <summary>A result carrying the failure that refused the argument.</summary>
    /// <param name="exitCode">The exit code the refusal ends <c>surl</c> with.</param>
    /// <param name="message">The stderr text after the <c>surl: </c> prefix.</param>
    /// <returns>The refused result.</returns>
    public static ListenUrlParseResult Refused(SurlExitCode exitCode, string message) =>
        new(null, new CommandLineFailure(exitCode, message));
}
