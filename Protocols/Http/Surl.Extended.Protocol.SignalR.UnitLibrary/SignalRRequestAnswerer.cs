using System.Globalization;
using System.Text.Json;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Answers each HTTP request a SignalR hub's connection sends: judges its login, then answers
/// negotiate, upgrades the WebSocket transport, and refuses what the hub does not serve
/// (TransportProtocols.md "negotiate" and "WebSockets"; ADR-0119, decisions 1, 2, 5 and 7).
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
/// <c>[endpoint-base]</c>, a WebSocket upgrade is checked as ADR-0071 orders it (another method is
/// <c>405</c> with <c>Allow: GET</c>, a <c>GET</c> without an upgrade <c>426</c>), then its
/// <c>id</c>: a connection negotiated by the same caller and not yet taken up, or a new one when
/// there is no <c>id</c>, is answered <c>101</c> and served by a <see cref="SignalRHubSession"/>;
/// an unknown <c>id</c> is <c>404</c>, one already taken up <c>409</c>, one another caller
/// negotiated <c>403</c>, and a new connection on a full hub <c>503</c>. Every other path is
/// <c>404</c>.
/// </para>
/// </remarks>
internal sealed partial class SignalRRequestAnswerer(
    SignalRHubPath hubPath,
    SignalRHubConnections connections,
    IHttpAuthenticationSession authentication,
    ExchangeContext context) : IHttpRequestHandler
{
    /// <summary>
    /// The negotiate <c>error</c> text sent when the hub holds <see cref="SignalRHubConnections.MaxConnections"/>.
    /// </summary>
    public const string HubFullText = "The server has too many connections.";

    private static readonly HttpStatus Conflict = new(409, "Conflict");

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = SignalRRequestTarget.Parse(request.Target);
        var verdict = await authentication.JudgeAsync(
            new HttpAuthenticationRequest(request.Method, request.Target, IsWrite: true, FieldsWithAccessToken(request.Fields, target)),
            cancellationToken);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return Refused(request, verdict);
        }

        if (target.Path == hubPath.NegotiatePath)
        {
            return await NegotiateAsync(request, target, verdict.AccountName, cancellationToken);
        }

        if (target.Path != hubPath.EndpointBase)
        {
            return Answer(request, HttpStatus.NotFound);
        }

        return IsWebSocketUpgrade(request)
            ? AnswerWebSocketUpgrade(request, target, verdict.AccountName)
            : await AnswerHttpTransportAsync(request, target, verdict.AccountName, cancellationToken);
    }

    private static bool IsWebSocketUpgrade(HttpRequest request) =>
        request.Fields.Any(field => field.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)
            && field.Value.Contains("websocket", StringComparison.OrdinalIgnoreCase));

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

        var connection = connections.Negotiate(accountName, isVersionOne, context.Limits.HeadTimeout, context.Identifiers, AsksForStatefulReconnect(target));
        if (connection is null)
        {
            context.Log.Note($"Negotiate refused: the hub holds {SignalRHubConnections.MaxConnections} connections.");

            return Json(request, HubMessageJsonWriter.WriteRecord(writer => writer.WriteString("error", HubFullText))[..^1]);
        }

        return Json(request, NegotiateResponse(connection));
    }

    // useStatefulReconnect is the name ASP.NET Core clients send, useAck the one TransportProtocols.md gives.
    private static bool AsksForStatefulReconnect(SignalRRequestTarget target) =>
        string.Equals(target.QueryParameter("useStatefulReconnect"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(target.QueryParameter("useAck"), "true", StringComparison.OrdinalIgnoreCase);

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
            if (connection.Reconnect is not null)
            {
                writer.WriteBoolean("useStatefulReconnect", true);
            }
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
        WriteTransport(writer, "ServerSentEvents", "Text");
        WriteTransport(writer, "LongPolling", "Text", "Binary");
        writer.WriteEndArray();
    }

    private static void WriteTransport(Utf8JsonWriter writer, string name, params string[] transferFormats)
    {
        writer.WriteStartObject();
        writer.WriteString("transport", name);
        writer.WriteStartArray("transferFormats");
        foreach (var format in transferFormats)
        {
            writer.WriteStringValue(format);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    // ADR-0071's checks in order (a method other than GET is 405 Allow: GET, a GET without an
    // upgrade 426), then the id: the connection it names, or a new one when it names none.
    private HttpAnswer AnswerWebSocketUpgrade(HttpRequest request, SignalRRequestTarget target, string? accountName)
    {
        // Http1ConnectionServer, the only server of this handler, always sets the version.
        var head = new HttpRequestHead(request.Method, request.Target, request.Http1Version!, request.Fields.Select(field => new HttpRequestField(field.Key, field.Value)));
        if (WebSocketUpgradeChecks.FindRefusal(head) is { } refusal)
        {
            return Answer(request, refusal.Status, refusal.Fields);
        }

        var outcome = connections.TakeUp(target.QueryParameter("id"), accountName, out var hubConnection);
        context.Log.Note($"WebSocket upgrade: {outcome}.");

        return outcome == SignalRUpgradeOutcome.TakenUp
            ? Upgrade(head, hubConnection!)
            : Answer(request, UpgradeRefusalStatus(outcome));
    }

    private static HttpStatus UpgradeRefusalStatus(SignalRUpgradeOutcome outcome) => outcome switch
    {
        SignalRUpgradeOutcome.AlreadyTakenUp => Conflict,
        SignalRUpgradeOutcome.NegotiatedByAnotherCaller => HttpStatus.Forbidden,
        SignalRUpgradeOutcome.HubFull => HttpStatus.ServiceUnavailable,
        _ => HttpStatus.NotFound,
    };

    private HttpAnswer Upgrade(HttpRequestHead head, SignalRNegotiatedConnection hubConnection) =>
        new(
            WebSocketHttpStatuses.SwitchingProtocols,
            [
                new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
                new("Upgrade", "websocket"),
                new("Connection", "Upgrade"),
                new("Sec-WebSocket-Accept", WebSocketAcceptKey.Compute(head.GetFieldValues("Sec-WebSocket-Key")[0])),
            ],
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: false,
            new SignalRWebSocketTakeover(connections, hubConnection, hubPath.EndpointBase, context));

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
