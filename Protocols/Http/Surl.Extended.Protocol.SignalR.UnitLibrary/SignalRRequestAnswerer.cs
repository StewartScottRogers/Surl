using System.Globalization;
using System.Text.Json;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Answers each HTTP request a SignalR hub's connection sends: judges its login, then answers
/// negotiate, refuses what the hub does not serve, and leaves the WebSocket transport to BL-449
/// (TransportProtocols.md "negotiate"; ADR-0119, decisions 1, 2, 5 and 7).
/// </summary>
/// <remarks>
/// <para>
/// Every request is judged first, so an unauthenticated client learns nothing about the paths
/// served: <c>Challenge</c> is <c>401</c> with the verdict's <c>WWW-Authenticate</c> values,
/// <c>Forbidden</c> is <c>403</c>, neither with a body. An <c>access_token</c> query parameter is
/// judged as <c>Authorization: Bearer &lt;value&gt;</c> when the request has no
/// <c>Authorization</c> field.
/// </para>
/// <para>
/// <c>POST [endpoint-base]/negotiate</c> is answered <c>200</c> with the negotiate JSON (version 0
/// or 1 by <c>negotiateVersion</c>), or with <c>{"error":"The server has too many connections."}</c>
/// when the hub is full; another method there is <c>405</c> with <c>Allow: POST</c>, and a
/// <c>negotiateVersion</c> that is not a non-negative integer is <c>400</c>. On
/// <c>[endpoint-base]</c>, <c>GET</c> is <c>426 Upgrade Required</c> with
/// <c>Sec-WebSocket-Version: 13</c> until BL-449 serves the upgrade, and any other method is
/// <c>405</c> with <c>Allow: GET</c>. Every other path is <c>404</c>.
/// </para>
/// </remarks>
internal sealed class SignalRRequestAnswerer(
    SignalRHubPath hubPath,
    SignalRHubConnections connections,
    IHttpAuthenticationSession authentication,
    ExchangeContext context) : IHttpRequestHandler
{
    /// <summary>
    /// The negotiate <c>error</c> text sent when the hub holds <see cref="SignalRHubConnections.MaxConnections"/>.
    /// </summary>
    public const string HubFullText = "The server has too many connections.";

    private static readonly HttpStatus UpgradeRequired = new(426, "Upgrade Required");

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = SignalRRequestTarget.Parse(request.Target);
        var verdict = await authentication.JudgeAsync(
            new HttpAuthenticationRequest(request.Method, request.Target, IsWrite: false, FieldsWithAccessToken(request.Fields, target)),
            cancellationToken);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return Refused(request, verdict);
        }

        if (target.Path == hubPath.NegotiatePath)
        {
            return await NegotiateAsync(request, target, verdict.AccountName, cancellationToken);
        }

        return target.Path == hubPath.EndpointBase
            ? AnswerEndpointBase(request)
            : Answer(request, HttpStatus.NotFound);
    }

    // access_token stands in for an Authorization field a browser's WebSocket cannot set.
    private static IReadOnlyList<KeyValuePair<string, string>> FieldsWithAccessToken(
        IReadOnlyList<KeyValuePair<string, string>> fields, SignalRRequestTarget target)
    {
        var hasAuthorization = fields.Any(field => field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        if (hasAuthorization || target.QueryParameter("access_token") is not { } token)
        {
            return fields;
        }

        return [.. fields, new("Authorization", "Bearer " + token)];
    }

    private HttpAnswer Refused(HttpRequest request, HttpAuthenticationVerdict verdict)
    {
        context.Log.Note($"{request.Method} refused by the login check: {verdict.Outcome}.");

        return verdict.Outcome == HttpAuthenticationOutcome.Challenge
            ? Answer(request, HttpStatus.Unauthorized, [.. verdict.WwwAuthenticateValues.Select(value => new KeyValuePair<string, string>("WWW-Authenticate", value))])
            : Answer(request, HttpStatus.Forbidden);
    }

    private async Task<HttpAnswer> NegotiateAsync(HttpRequest request, SignalRRequestTarget target, string? accountName, CancellationToken cancellationToken)
    {
        if (request.Method != "POST")
        {
            return Answer(request, HttpStatus.MethodNotAllowed, [new("Allow", "POST")]);
        }

        if (!TryReadNegotiateVersion(target.QueryParameter("negotiateVersion"), out var isVersionOne))
        {
            return Answer(request, HttpStatus.BadRequest);
        }

        if (await DiscardBodyAsync(request, cancellationToken) is { } bodyRefusal)
        {
            return bodyRefusal;
        }

        var connection = connections.Negotiate(accountName, isVersionOne, context.Limits.HeadTimeout);
        if (connection is null)
        {
            context.Log.Note($"Negotiate refused: the hub holds {SignalRHubConnections.MaxConnections} connections.");

            return Json(request, HubMessageJsonWriter.WriteRecord(writer => writer.WriteString("error", HubFullText))[..^1]);
        }

        return Json(request, NegotiateResponse(connection));
    }

    // Absent is version 0; 0 is 0; any larger non-negative integer is the server's largest, 1.
    private static bool TryReadNegotiateVersion(string? value, out bool isVersionOne)
    {
        isVersionOne = false;
        if (value is null)
        {
            return true;
        }

        if (value.Length == 0 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        isVersionOne = value.Any(digit => digit != '0');

        return true;
    }

    private static async Task<HttpAnswer?> DiscardBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.Body.IsAnnounced || !request.Body.IsReadable)
        {
            return null;
        }

        var outcome = await request.Body.DiscardAsync(null, cancellationToken);
        if (outcome == HttpRequestBodyDiscardOutcome.Discarded)
        {
            return null;
        }

        var status = outcome == HttpRequestBodyDiscardOutcome.TooLarge ? HttpStatus.ContentTooLarge : HttpStatus.BadRequest;

        return new HttpAnswer(status, [new("Content-Length", "0")], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);
    }

    private static byte[] NegotiateResponse(SignalRNegotiatedConnection connection)
    {
        var record = HubMessageJsonWriter.WriteRecord(writer =>
        {
            writer.WriteNumber("negotiateVersion", connection.ConnectionToken is null ? 0 : 1);
            writer.WriteString("connectionId", connection.ConnectionId);
            if (connection.ConnectionToken is { } token)
            {
                writer.WriteString("connectionToken", token);
            }

            WriteAvailableTransports(writer);
        });

        return record[..^1];
    }

    private static void WriteAvailableTransports(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("availableTransports");
        writer.WriteStartObject();
        writer.WriteString("transport", "WebSockets");
        writer.WriteStartArray("transferFormats");
        writer.WriteStringValue("Text");
        writer.WriteStringValue("Binary");
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private HttpAnswer AnswerEndpointBase(HttpRequest request) => request.Method == "GET"
        ? Answer(request, UpgradeRequired, [new("Sec-WebSocket-Version", "13")])
        : Answer(request, HttpStatus.MethodNotAllowed, [new("Allow", "GET")]);

    private HttpAnswer Answer(HttpRequest request, HttpStatus status, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        new(status, [.. CommonFields(0), .. fields ?? []], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    private HttpAnswer Json(HttpRequest request, byte[] body) =>
        new(
            HttpStatus.Ok,
            [.. CommonFields(body.Length), new("Content-Type", "application/json")],
            new HttpAnswerBody.Bytes(body),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);

    private KeyValuePair<string, string>[] CommonFields(int contentLength) =>
    [
        new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
        new("Content-Length", contentLength.ToString(CultureInfo.InvariantCulture)),
    ];
}
