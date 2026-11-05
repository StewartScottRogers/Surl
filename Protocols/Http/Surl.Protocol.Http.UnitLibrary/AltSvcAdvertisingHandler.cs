using System.Globalization;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// Adds <c>Alt-Svc: h3=":&lt;port&gt;"; ma=86400</c> (RFC 7838) to every answer another handler
/// gives on an <c>https</c> connection while HTTP/3 is served on the same port, so a client may
/// switch to it (ADR-0082, decision 6). The answer is otherwise unchanged.
/// </summary>
/// <param name="handler">The handler whose answers are advertised on.</param>
/// <param name="port">The port HTTP/3 is served on: the connection's own.</param>
internal sealed class AltSvcAdvertisingHandler(IHttpRequestHandler handler, int port) : IHttpRequestHandler
{
    /// <summary>The field's name, as RFC 7838 section 3 spells it.</summary>
    public const string FieldName = "Alt-Svc";

    /// <summary>How long a client may keep using the alternative, in seconds: one day.</summary>
    public const int MaxAgeSeconds = 86400;

    private readonly string fieldValue = string.Create(CultureInfo.InvariantCulture, $"h3=\":{port}\"; ma={MaxAgeSeconds}");

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var answer = await handler.AnswerAsync(request, cancellationToken);
        return answer with { Fields = [.. answer.Fields, new KeyValuePair<string, string>(FieldName, fieldValue)] };
    }
}
