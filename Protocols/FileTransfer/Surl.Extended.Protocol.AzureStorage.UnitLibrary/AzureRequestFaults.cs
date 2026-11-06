using System.Collections.Frozen;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Asks the exchange's fault plan, for each request let in on either endpoint, whether to answer it
/// with a chosen error instead (ADR-0124, decisions 4 and 9): the point names the scheme
/// (<c>az</c>, <c>abfs</c> or <c>abfss</c>), the login as <c>account</c> (none for the anonymous
/// caller), the container or blob path as <c>path</c> (query excluded, as sent) and the HTTP method as
/// <c>command</c>.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Error"/> rule's reply is a status 400 to 599, a space and an error code word
/// of ASCII letters and digits (<c>503 ServerBusy</c>); the error is answered with that status,
/// <c>x-ms-error-code</c> and the endpoint's own error body, and one <see cref="FaultAppliedEvent"/> is
/// published. A rule of another kind, or a reply that does not fit, is noted and the request is
/// answered as without it.
/// </remarks>
internal static class AzureRequestFaults
{
    /// <summary>
    /// The <c>Message</c> of a chosen error, whose code may be one the server has no table row for.
    /// </summary>
    public const string ChosenErrorMessage = "The server answered this request with an error chosen by a fault rule.";

    /// <summary>
    /// The longest error code word a reply may name.
    /// </summary>
    public const int MaxCodeWordLength = 64;

    private static readonly FrozenDictionary<int, string> ReasonPhrases = new Dictionary<int, string>
    {
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [403] = "Forbidden",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [408] = "Request Timeout",
        [409] = "Conflict",
        [411] = "Length Required",
        [412] = "Precondition Failed",
        [413] = "Content Too Large",
        [416] = "Range Not Satisfiable",
        [429] = "Too Many Requests",
        [500] = "Internal Server Error",
        [501] = "Not Implemented",
        [503] = "Service Unavailable",
        [504] = "Gateway Timeout",
    }.ToFrozenDictionary();

    /// <summary>
    /// Finds the chosen error for a request that logged in, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and log.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The container or blob path, query excluded.</param>
    /// <param name="accountName">The account the request logged in as, or <see langword="null"/> when anonymous.</param>
    /// <returns>The error to answer with, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<AzureError?> FindChosenErrorAsync(ExchangeContext context, string method, string path, string? accountName)
    {
        var rule = await HttpCommandFaults.FindChosenErrorAsync(context, method, path, accountName);
        if (rule is null)
        {
            return null;
        }

        if (!TryReadChosenError(rule.Reply, out var error))
        {
            context.Log.Note($"{method} {path}: fault rule {rule.Id}'s reply does not fit the Azure Storage error shape; answered as without it.");

            return null;
        }

        HttpCommandFaults.PublishApplied(context, rule, method, path, $"{error.Status.Code} {error.Code}");

        return error;
    }

    /// <summary>
    /// Reads a reply in the Azure Storage error shape: a status 400 to 599, one space, and an error
    /// code word of 1 to <see cref="MaxCodeWordLength"/> ASCII letters and digits.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/>.</param>
    /// <param name="error">The error the reply names, with <see cref="ChosenErrorMessage"/>.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryReadChosenError(string? reply, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AzureError? error)
    {
        error = null;
        if (!HttpCommandFaults.TrySplitCode(reply, out var code, out var codeWord)
            || code is < 400 or > 599
            || !IsCodeWord(codeWord))
        {
            return false;
        }

        error = new AzureError(codeWord, new HttpStatus(code, ReasonPhrases.GetValueOrDefault(code, string.Empty)), ChosenErrorMessage);

        return true;
    }

    private static bool IsCodeWord([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? text) =>
        text is { Length: > 0 and <= MaxCodeWordLength } && text.All(char.IsAsciiLetterOrDigit);
}
