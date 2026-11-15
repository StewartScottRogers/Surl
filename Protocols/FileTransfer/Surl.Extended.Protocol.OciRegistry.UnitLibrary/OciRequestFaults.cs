using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Asks the exchange's fault plan, for each registry request let in (past the token check), whether
/// to answer it with a chosen error, hold it or drop it instead (ADR-0124, decisions 4 and 9): the
/// point names the scheme <c>oci</c>, the token's account as <c>account</c> (none for an anonymous
/// caller), the request path (query excluded, as sent) as <c>path</c> and the HTTP method as
/// <c>command</c>. The token endpoint is the server's login, not a registry operation, so faults do
/// not apply to it (BL-1189).
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Error"/> rule's reply is a status 400 to 599, a space and one of the
/// distribution specification's error codes (<c>429 TOOMANYREQUESTS</c>); the request is answered with
/// that status and the specification's JSON error body, and one <see cref="FaultAppliedEvent"/> is
/// published. A rule of another kind, or a reply that does not fit, is noted and the request is
/// answered as without it.
/// </remarks>
internal static class OciRequestFaults
{
    private static readonly FrozenDictionary<string, OciError> ErrorsByCode = new[]
    {
        OciError.BlobUnknown, OciError.BlobUploadInvalid, OciError.BlobUploadUnknown, OciError.DigestInvalid,
        OciError.ManifestBlobUnknown, OciError.ManifestInvalid, OciError.ManifestUnknown, OciError.NameInvalid,
        OciError.NameUnknown, OciError.SizeInvalid, OciError.Unauthorized, OciError.Denied, OciError.UnsupportedPath,
        OciError.TooManyRequests,
    }.ToFrozenDictionary(error => error.Code, StringComparer.Ordinal);

    private static readonly FrozenDictionary<int, string> ReasonPhrases = new Dictionary<int, string>
    {
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [403] = "Forbidden",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [409] = "Conflict",
        [413] = "Content Too Large",
        [416] = "Range Not Satisfiable",
        [429] = "Too Many Requests",
        [500] = "Internal Server Error",
        [503] = "Service Unavailable",
    }.ToFrozenDictionary();

    // What a drop rule answers with: nothing is written and the exchange is ended.
    private static readonly HttpAnswer DroppedAnswer = new(
        new HttpStatus(500, string.Empty), [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false, DropsExchange: true);

    /// <summary>
    /// Asks the fault plan about a request and applies what it finds: a delay holds the request and it
    /// then goes on, a drop is answered with an answer that ends the exchange, and a chosen error is
    /// answered with its error.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and log.</param>
    /// <param name="reply">The reply that words an error.</param>
    /// <param name="request">The request.</param>
    /// <param name="accountName">The account the token was issued to, or <see langword="null"/> when anonymous.</param>
    /// <returns>The answer to give in place of the operation, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<HttpAnswer?> FindAnswerAsync(ExchangeContext context, OciReply reply, HttpRequest request, string? accountName)
    {
        var queryStart = request.Target.IndexOf('?');
        var path = queryStart < 0 ? request.Target : request.Target[..queryStart];
        var rule = await HttpCommandFaults.FindAndApplyAsync(context, request.Method, path, accountName);
        if (rule is null)
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return DroppedAnswer;
        }

        if (!TryReadChosenError(rule.Reply, out var error))
        {
            context.Log.Note($"{request.Method} {path}: fault rule {rule.Id}'s reply does not fit the OCI error shape; answered as without it.");

            return null;
        }

        HttpCommandFaults.PublishApplied(context, rule, request.Method, path, $"{error.Status.Code} {error.Code}");

        return reply.Error(error);
    }

    /// <summary>
    /// Reads a reply in the OCI error shape: a status 400 to 599, one space, and one of the
    /// distribution specification's error codes.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/>.</param>
    /// <param name="error">The error the reply names, with the chosen status.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryReadChosenError(string? reply, [NotNullWhen(true)] out OciError? error)
    {
        error = null;
        if (!HttpCommandFaults.TrySplitCode(reply, out var code, out var codeWord)
            || code is < 400 or > 599
            || codeWord is null
            || !ErrorsByCode.TryGetValue(codeWord, out var known))
        {
            return false;
        }

        error = known with { Status = new HttpStatus(code, ReasonPhrases.GetValueOrDefault(code, string.Empty)) };

        return true;
    }
}
