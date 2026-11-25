using Surl.Cookies;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Publishes each <c>name=value</c> pair of a request's <c>Cookie</c> headers as a <see cref="CookieReceivedEvent"/> before
/// the reply, and adds a <c>Set-Cookie</c> field for each configured cookie that matches the request path and listener to
/// the answer another handler gives, which is otherwise unchanged (ADR-0189 decisions 2, 3 and 5). With no cookie
/// configured it still publishes what the client sent.
/// </summary>
/// <param name="handler">The handler whose answers gain the fields.</param>
/// <param name="cookies">The cookies to set, in the order written.</param>
/// <param name="context">The exchange: its id, listen URL and event stream.</param>
internal sealed class CookieHandler(IHttpRequestHandler handler, IReadOnlyList<CookieDefinition> cookies, ExchangeContext context) : IHttpRequestHandler
{
    private const string CookieField = "Cookie";

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        PublishReceivedCookies(request);
        var answer = await handler.AnswerAsync(request, cancellationToken);
        var path = PathOf(request.Target);
        var fields = cookies
            .Where(cookie => IsOnPath(cookie.OnPath, path) && IsOnListener(cookie.OnListener))
            .Select(cookie => new KeyValuePair<string, string>(SetCookieHeaderWriter.HeaderName, SetCookieHeaderWriter.WriteFieldValue(cookie)))
            .ToArray();

        return fields.Length == 0 ? answer : answer with { Fields = [.. answer.Fields, .. fields] };
    }

    private void PublishReceivedCookies(HttpRequest request)
    {
        foreach (var field in request.Fields.Where(field => string.Equals(field.Key, CookieField, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var pair in CookieHeaderReader.Read(field.Value))
            {
                context.Events.Publish(new CookieReceivedEvent(context.ExchangeId, context.Scheme, pair.Name, pair.Value));
            }
        }
    }

    private static string PathOf(string target)
    {
        var path = target;
        var schemeEnd = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            var pathStart = path.IndexOf('/', schemeEnd + 3);
            path = pathStart < 0 ? "/" : path[pathStart..];
        }

        var end = path.IndexOfAny(['?', '#']);

        return end < 0 ? path : path[..end];
    }

    private static bool IsOnPath(string? prefix, string path) =>
        prefix is null
        || path == prefix
        || (path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.Ordinal) && (prefix.EndsWith('/') || path[prefix.Length] == '/'));

    private bool IsOnListener(string? listener) =>
        listener is null || IsSameListener(listener, ReportedListenUrl.Format(context.ListenUrl));

    // As a fault rule's listener key is matched: the text, or scheme and host, and the port unless the key's is 0.
    private static bool IsSameListener(string wanted, string actual) =>
        string.Equals(wanted, actual, StringComparison.Ordinal)
        || (Uri.TryCreate(wanted, UriKind.Absolute, out var want)
            && Uri.TryCreate(actual, UriKind.Absolute, out var have)
            && string.Equals(want.Scheme, have.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(want.Host, have.Host, StringComparison.OrdinalIgnoreCase)
            && (want.Port == 0 || want.Port == have.Port));
}