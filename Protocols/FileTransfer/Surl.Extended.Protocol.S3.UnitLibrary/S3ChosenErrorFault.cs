using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Asks the exchange's fault plan about each S3 request once its login was judged, before the
/// content store is touched, and turns a matching <see cref="FaultKind.Error"/> rule into an S3
/// error (ADR-0124, decisions 4 and 9): the point names the scheme, the login as <c>account</c>
/// (none when anonymous), the method as <c>command</c> and the bucket or object path, path style
/// and as sent, as <c>path</c>. The rule's reply is a status 400 to 599 and an error code word
/// (<c>503 SlowDown</c>), written as the <c>Error</c> body's <c>Code</c>.
/// </summary>
/// <remarks>
/// A rule of another kind, or a reply that does not fit, is noted in the exchange log and the
/// request is answered as without it. Applying a rule publishes one <see cref="FaultAppliedEvent"/>
/// naming the path.
/// </remarks>
internal static class S3ChosenErrorFault
{
    /// <summary>
    /// The most characters an error code word may have.
    /// </summary>
    public const int MaxCodeLength = 64;

    // RFC 9110 section 15's reason phrases for the client and server errors, with RFC 6585's. Any
    // other status goes with an empty reason phrase, which RFC 9112 section 4 allows.
    private static readonly FrozenDictionary<int, string> ErrorReasonPhrases = new Dictionary<int, string>
    {
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [402] = "Payment Required",
        [403] = "Forbidden",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [406] = "Not Acceptable",
        [407] = "Proxy Authentication Required",
        [408] = "Request Timeout",
        [409] = "Conflict",
        [410] = "Gone",
        [411] = "Length Required",
        [412] = "Precondition Failed",
        [413] = "Content Too Large",
        [414] = "URI Too Long",
        [415] = "Unsupported Media Type",
        [416] = "Range Not Satisfiable",
        [417] = "Expectation Failed",
        [421] = "Misdirected Request",
        [422] = "Unprocessable Content",
        [426] = "Upgrade Required",
        [428] = "Precondition Required",
        [429] = "Too Many Requests",
        [431] = "Request Header Fields Too Large",
        [500] = "Internal Server Error",
        [501] = "Not Implemented",
        [502] = "Bad Gateway",
        [503] = "Service Unavailable",
        [504] = "Gateway Timeout",
        [505] = "HTTP Version Not Supported",
        [511] = "Network Authentication Required",
    }.ToFrozenDictionary();

    /// <summary>
    /// Finds the chosen error for a request, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream, log and listen URL.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="accountName">The login's account, or <see langword="null"/> when anonymous.</param>
    /// <returns>The error to answer with, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<S3Error?> FindAsync(ExchangeContext context, string method, S3RequestTarget target, string? accountName)
    {
        var path = PathOf(target);
        var rule = await HttpCommandFaults.FindChosenErrorAsync(context, method, path, accountName);
        if (rule is null)
        {
            return null;
        }

        if (!TryRead(rule.Reply, rule.Id, out var error))
        {
            context.Log.Note($"{method} {path}: fault rule {rule.Id}'s reply does not fit the s3 error shape; answered as without it.");

            return null;
        }

        HttpCommandFaults.PublishApplied(context, rule, method, path, $"{error.Status.Code} {error.Code}");

        return error;
    }

    /// <summary>
    /// The path a fault rule names a request by: <c>/&lt;bucket&gt;</c> for a bucket,
    /// <c>/&lt;bucket&gt;/&lt;key&gt;</c> for an object, path style even when the request was
    /// virtual-hosted, and otherwise the path as sent; all still percent-encoded.
    /// </summary>
    /// <param name="target">The request's target.</param>
    /// <returns>The path.</returns>
    public static string PathOf(S3RequestTarget target) =>
        target.RawBucket.Length == 0 ? target.Path
        : target.RawKey.Length == 0 ? $"/{target.RawBucket}"
        : target.ObjectPath;

    /// <summary>
    /// Reads a reply in the <c>s3</c> error shape: a status 400 to 599, one space, and an error code
    /// word - an ASCII letter, then ASCII letters and digits, at most <see cref="MaxCodeLength"/> in all.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="ruleId">The rule's ID, named in the error's <c>Message</c>.</param>
    /// <param name="error">The error the reply names.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryRead(string? reply, string ruleId, [NotNullWhen(true)] out S3Error? error)
    {
        error = null;
        if (!HttpCommandFaults.TrySplitCode(reply, out var status, out var code)
            || status is < 400 or > 599
            || !IsCodeWord(code))
        {
            return false;
        }

        error = new S3Error(code, new HttpStatus(status, ErrorReasonPhrases.GetValueOrDefault(status, string.Empty)), $"This error was chosen by fault rule {ruleId}.");

        return true;
    }

    private static bool IsCodeWord([NotNullWhen(true)] string? code) =>
        code is { Length: > 0 and <= MaxCodeLength }
        && char.IsAsciiLetter(code[0])
        && !code.AsSpan().ContainsAnyExcept(CodeWordCharacters);

    private static readonly System.Buffers.SearchValues<char> CodeWordCharacters =
        System.Buffers.SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
}
