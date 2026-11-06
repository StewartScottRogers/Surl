using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Asks the exchange's fault plan, for each request once its login proceeds and before the content
/// store is touched, whether to answer it with a chosen error instead (ADR-0124, decisions 4 and 9;
/// BL-982): the point names the scheme, the login as <c>account</c> (none for the anonymous caller),
/// the HTTP method as <c>command</c>, and as <c>path</c> the bucket or object path-style and decoded -
/// <c>/</c> for the service, <c>/&lt;bucket&gt;</c> for a bucket, <c>/&lt;bucket&gt;/&lt;name&gt;</c>
/// for an object - however the request addressed it.
/// </summary>
/// <remarks>
/// A rule's reply is a status 400 to 599, one space and an error code word of ASCII letters and digits
/// starting with a letter (<c>503 SlowDown</c>), answered as the XML API's <c>Error</c> body. The
/// message is the server's own for a code it sends itself, else a fixed one. Applying a rule publishes
/// one <see cref="FaultAppliedEvent"/> naming the path. A rule of another kind, or a reply that does
/// not fit, is noted and the request is answered as without it.
/// </remarks>
internal static class GcsRequestFaults
{
    /// <summary>
    /// The message of a chosen code the server never sends itself.
    /// </summary>
    public const string ChosenErrorMessage = "The request was answered with an error a fault rule chose.";

    private const int MaxCodeLength = 64;

    private static readonly SearchValues<char> CodeCharacters =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    // The messages of the codes the server sends itself, so a chosen one reads as the server's own.
    private static readonly FrozenDictionary<string, string> OwnMessages = new[]
    {
        GcsError.AccessDenied, GcsError.AuthenticationRequired, GcsError.BadDigest, GcsError.EntityTooLarge,
        GcsError.EntityTooSmall, GcsError.IncompleteBody, GcsError.InsufficientQuota, GcsError.InvalidAccessKeyId,
        GcsError.InvalidArgument, GcsError.InvalidBearerToken, GcsError.InvalidDigest, GcsError.InvalidHash,
        GcsError.InvalidObjectName, GcsError.InvalidPart, GcsError.InvalidPartOrder, GcsError.InvalidRange,
        GcsError.KeyTooLong, GcsError.MetadataTooLarge, GcsError.MissingContentLength, GcsError.MissingSecurityHeader,
        GcsError.NoSuchBucket, GcsError.NoSuchKey, GcsError.NoSuchUpload, GcsError.NotImplemented,
        GcsError.PreconditionFailed, GcsError.RequestTimeTooSkewed, GcsError.SignatureDoesNotMatch, GcsError.SlowDown,
    }.ToFrozenDictionary(error => error.Code, error => error.Message, StringComparer.Ordinal);

    // RFC 9110 section 15's reason phrases for the client and server errors, with RFC 6585's; any
    // other status goes with an empty reason phrase, which RFC 9112 section 4 allows.
    private static readonly FrozenDictionary<int, string> ReasonPhrases = new Dictionary<int, string>
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
    /// <param name="caller">The caller the login proceeded as.</param>
    /// <returns>The error to answer with, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<GcsError?> FindChosenErrorAsync(ExchangeContext context, string method, GcsRequestTarget target, ContentCaller caller)
    {
        var path = PathOf(target);
        var rule = await HttpCommandFaults.FindChosenErrorAsync(context, method, path, caller.AccountName);
        if (rule is null)
        {
            return null;
        }

        if (!TryRead(rule.Reply, out var error))
        {
            context.Log.Note($"{method} {path}: fault rule {rule.Id}'s reply does not fit the gs error shape; answered as without it.");

            return null;
        }

        HttpCommandFaults.PublishApplied(context, rule, method, path, $"{error.Status.Code} {error.Code}");

        return error;
    }

    /// <summary>
    /// The path a fault rule's <c>path</c> matches: <c>/</c> for the service, else the path-style
    /// path of the bucket, or of the object in it, percent-decoded.
    /// </summary>
    /// <param name="target">The request's target.</param>
    /// <returns>The path.</returns>
    public static string PathOf(GcsRequestTarget target)
    {
        if (target.NamesTheService)
        {
            return "/";
        }

        var path = target.NamesNoObject ? $"/{target.RawBucket}" : target.ObjectPath;

        return Uri.UnescapeDataString(path);
    }

    /// <summary>
    /// Reads a reply in the <c>gs</c> error shape: a status 400 to 599, one space, and an error code
    /// word of at most 64 ASCII letters and digits starting with a letter.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="error">The error the reply names.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryRead(string? reply, [NotNullWhen(true)] out GcsError? error)
    {
        error = null;
        if (!HttpCommandFaults.TrySplitCode(reply, out var status, out var code)
            || status is < 400 or > 599
            || !IsCodeWord(code))
        {
            return false;
        }

        error = new GcsError(
            code,
            new HttpStatus(status, ReasonPhrases.GetValueOrDefault(status, string.Empty)),
            OwnMessages.GetValueOrDefault(code, ChosenErrorMessage));

        return true;
    }

    private static bool IsCodeWord([NotNullWhen(true)] string? code) =>
        code is { Length: > 0 and <= MaxCodeLength }
        && char.IsAsciiLetter(code[0])
        && !code.AsSpan().ContainsAnyExcept(CodeCharacters);
}
