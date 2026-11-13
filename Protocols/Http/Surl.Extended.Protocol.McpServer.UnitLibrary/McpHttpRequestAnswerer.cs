using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Answers the Streamable HTTP requests of one connection (ADR-0111 decision 4), in decision 4.2's order: the
/// path - the endpoint, the RFC 9728 metadata document (decision 6.2), or one of the authorization server's paths
/// (decision 6.3) - then <c>Origin</c> (4.7), then the login (6.1), then the method (4.4), then <c>Accept</c> and
/// <c>Content-Type</c> (4.3), the body as one JSON-RPC message, and the session and <c>MCP-Protocol-Version</c>
/// (4.5, 4.6). Every answer is one of decision 7.2's rows.
/// </summary>
internal sealed class McpHttpRequestAnswerer : IHttpRequestHandler
{
    private const string InitializeMethod = "initialize";

    private const string ToolsCallMethod = "tools/call";

    private const string AbsentProtocolVersion = "2025-03-26";

    private const string SessionIdField = "Mcp-Session-Id";

    private readonly McpHttpServerSettings _settings;

    private readonly IHttpAuthenticationSession _authentication;

    private readonly ExchangeContext _context;

    private readonly string _httpScheme;

    private readonly McpHttpAnswerWriter _writer;

    private readonly McpClientRegistrationEndpoint _registration;

    private readonly McpAuthorizeEndpoint _authorize;

    private readonly McpTokenEndpoint _token;

    /// <summary>Initializes a new instance of the <see cref="McpHttpRequestAnswerer" /> class.</summary>
    /// <param name="settings">What every connection of the server shares.</param>
    /// <param name="policy">The policy whose HTTP session judges the endpoint's logins and which checks passwords.</param>
    /// <param name="context">The exchange: its scheme, clock and limits.</param>
    /// <param name="tlsSession">The connection's TLS session (<c>mcps</c>), which makes its origin <c>https</c>; <see langword="null" /> for plain <c>mcp</c>.</param>
    public McpHttpRequestAnswerer(McpHttpServerSettings settings, IAuthenticationPolicy policy, ExchangeContext context, TlsSession? tlsSession)
    {
        _settings = settings;
        _authentication = policy.StartHttpConnection(tlsSession, LoginExchange.Of(context));
        _context = context;
        _httpScheme = tlsSession is null ? "http" : "https";
        _writer = new McpHttpAnswerWriter(context.TimeProvider);
        McpPasswordCheck passwords = new(policy, context.Scheme, tlsSession);
        _registration = new McpClientRegistrationEndpoint(settings.Grants, _writer);
        _authorize = new McpAuthorizeEndpoint(settings.Grants, passwords, _writer);
        _token = new McpTokenEndpoint(settings.Grants, passwords, _writer);
    }

    private int MessageLimit =>
        _context.Limits.MaxMessageBytes is 0 or > int.MaxValue ? int.MaxValue : (int)_context.Limits.MaxMessageBytes;

    /// <inheritdoc />
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string path = request.Target.Split('?')[0];
        if (path == _settings.EndpointPath)
        {
            return await AnswerEndpointAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return AuthorizationMethodOf(path) is { } method
            ? await AnswerAuthorizationAsync(request, path, method, cancellationToken).ConfigureAwait(false)
            : _writer.Empty(request, HttpStatus.NotFound);
    }

    private async Task<HttpAnswer> AnswerEndpointAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!IsOriginAllowed(request))
        {
            return _writer.Empty(request, HttpStatus.Forbidden);
        }

        (string? login, HttpAnswer? refusal) = await LogInAsync(request, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        if (request.Method == "POST")
        {
            return await AnswerPostAsync(request, login, cancellationToken).ConfigureAwait(false);
        }

        return request.Method == "DELETE"
            ? AnswerDelete(request, login)
            : _writer.Empty(request, HttpStatus.MethodNotAllowed, [new("Allow", "POST, DELETE")]);
    }

    // The metadata documents and /authorize are GET only; /token and /register POST only.
    private string? AuthorizationMethodOf(string path) => path switch
    {
        McpOAuthJson.MetadataPath or McpOAuthJson.AuthorizePath => "GET",
        McpOAuthJson.TokenPath or McpOAuthJson.RegisterPath => "POST",
        _ when path == McpProtectedResourceMetadata.PathFor(_settings.EndpointPath) => "GET",
        _ => null,
    };

    // Served without a bearer token, after the Origin check and the method; a POST's body is read whole, bounded
    // by --max-message, before the endpoint sees it.
    private async Task<HttpAnswer> AnswerAuthorizationAsync(HttpRequest request, string path, string method, CancellationToken cancellationToken)
    {
        if (!IsOriginAllowed(request))
        {
            return _writer.Empty(request, HttpStatus.Forbidden);
        }

        if (request.Method != method)
        {
            return _writer.Empty(request, HttpStatus.MethodNotAllowed, [new("Allow", method)]);
        }

        if (method == "GET")
        {
            return await AnswerAuthorizationGetAsync(request, path, cancellationToken).ConfigureAwait(false);
        }

        (byte[]? body, HttpAnswer? refusal) = await ReadMessageAsync(request.Body, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        return path == McpOAuthJson.TokenPath
            ? await _token.AnswerAsync(request, body!, Resource(request), cancellationToken).ConfigureAwait(false)
            : _registration.Answer(request, body!);
    }

    private async Task<HttpAnswer> AnswerAuthorizationGetAsync(HttpRequest request, string path, CancellationToken cancellationToken) => path switch
    {
        McpOAuthJson.MetadataPath => _writer.Json(request, HttpStatus.Ok, McpOAuthJson.WriteMetadata(Issuer(request))),
        McpOAuthJson.AuthorizePath => await _authorize.AnswerAsync(request, Resource(request), cancellationToken).ConfigureAwait(false),
        _ => _writer.Json(request, HttpStatus.Ok, McpProtectedResourceMetadata.Write(Issuer(request), _settings.EndpointPath)),
    };

    private bool IsOriginAllowed(HttpRequest request) =>
        McpOriginCheck.IsAllowed(McpHttpFields.Field(request, "Origin"), _httpScheme, request.Authority);

    // Another Authorization scheme is 400. A live token this server issued for this resource logs in as its
    // account; any other token, or none, is judged by the policy's session, whose refusal is 401 with the RFC
    // 9728 challenge. A token is never read from the query string, so one there is judged as none.
    private async Task<(string? Login, HttpAnswer? Refusal)> LogInAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        string? authorization = McpHttpFields.Field(request, "Authorization");
        if (authorization is not null && !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return (null, _writer.Empty(request, HttpStatus.BadRequest));
        }

        if (authorization is not null && _settings.Grants.AccountOf(authorization["Bearer ".Length..].Trim(), Resource(request)) is { } issuedAccount)
        {
            return (issuedAccount, null);
        }

        bool isWrite = request.Method is not ("GET" or "HEAD");
        HttpAuthenticationVerdict verdict = await _authentication
            .JudgeAsync(new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields), cancellationToken)
            .ConfigureAwait(false);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            string challenge = McpProtectedResourceMetadata.Challenge(Issuer(request), _settings.EndpointPath, authorization is not null);
            return (null, _writer.Empty(request, HttpStatus.Unauthorized, [new("WWW-Authenticate", challenge)]));
        }

        return (verdict.AccountName, null);
    }

    private async Task<HttpAnswer> AnswerPostAsync(HttpRequest request, string? login, CancellationToken cancellationToken)
    {
        if (MediaTypeRefusal(request) is { } mediaTypeRefusal)
        {
            return mediaTypeRefusal;
        }

        (byte[]? message, HttpAnswer? refusal) = await ReadMessageAsync(request.Body, cancellationToken).ConfigureAwait(false);
        return refusal ?? await AnswerMessageAsync(request, login, message!, cancellationToken).ConfigureAwait(false);
    }

    private HttpAnswer? MediaTypeRefusal(HttpRequest request)
    {
        if (!McpAcceptField.AcceptsJsonAndEventStream(McpHttpFields.JoinedField(request, "Accept")))
        {
            return _writer.Empty(request, NotAcceptable);
        }

        return McpHttpFields.IsMediaType(McpHttpFields.Field(request, "Content-Type"), McpHttpAnswerWriter.JsonMediaType)
            ? null
            : _writer.Empty(request, UnsupportedMediaType);
    }

    private async Task<HttpAnswer> AnswerMessageAsync(HttpRequest request, string? login, byte[] message, CancellationToken cancellationToken)
    {
        JsonRpcIncoming incoming = JsonRpcMessageParser.Parse(message);
        if (incoming is JsonRpcInvalidMessage invalid)
        {
            return _writer.Json(request, HttpStatus.BadRequest, JsonRpcResponseWriter.WriteError(null, invalid.Error));
        }

        return incoming is JsonRpcRequest { Method: InitializeMethod }
            ? await AnswerInitializeAsync(request, login, message, cancellationToken).ConfigureAwait(false)
            : await AnswerInSessionAsync(request, login, incoming, message, cancellationToken).ConfigureAwait(false);
    }

    // The body is one message, bounded by --max-message: announced past it, or arriving past it, is 413 and
    // the connection closes; a body that cannot be read whole is 400 and the connection closes.
    private async Task<(byte[]? Message, HttpAnswer? Refusal)> ReadMessageAsync(IHttpRequestBody body, CancellationToken cancellationToken)
    {
        if (RefusalBeforeReading(body) is { } refusal)
        {
            return (null, refusal);
        }

        return body.IsAnnounced
            ? await CopyMessageAsync(body, cancellationToken).ConfigureAwait(false)
            : ([], null);
    }

    private HttpAnswer? RefusalBeforeReading(IHttpRequestBody body)
    {
        if (body.AnnouncedLength > MessageLimit)
        {
            return _writer.Refusal(HttpStatus.ContentTooLarge);
        }

        return body.IsAnnounced && !body.IsReadable ? _writer.Refusal(HttpStatus.BadRequest) : null;
    }

    private async Task<(byte[]? Message, HttpAnswer? Refusal)> CopyMessageAsync(IHttpRequestBody body, CancellationToken cancellationToken)
    {
        using McpMessageBuffer buffer = new(MessageLimit);
        HttpRequestBodyDiscardOutcome outcome = await body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (buffer.IsPastLimit || outcome == HttpRequestBodyDiscardOutcome.TooLarge)
        {
            return (null, _writer.Refusal(HttpStatus.ContentTooLarge));
        }

        return outcome == HttpRequestBodyDiscardOutcome.Discarded
            ? (buffer.ToArray(), null)
            : (null, _writer.Refusal(HttpStatus.BadRequest));
    }

    // An initialize starts a new session, so one naming a session is 400. The session is kept only when the
    // core answered initialize; past the session bound it is 503 and nothing is kept.
    private async Task<HttpAnswer> AnswerInitializeAsync(HttpRequest request, string? login, byte[] message, CancellationToken cancellationToken)
    {
        if (McpHttpFields.Field(request, SessionIdField) is not null)
        {
            return _writer.Empty(request, HttpStatus.BadRequest);
        }

        McpServerSession session = _settings.CreateSession(login, _context.Scheme, MessageLimit);
        byte[] response = (await session.AnswerAsync(message, cancellationToken).ConfigureAwait(false))!;
        if (session.NegotiatedProtocolVersion is null)
        {
            return _writer.Json(request, HttpStatus.Ok, response);
        }

        return _settings.Sessions.Add(login, session) is { } id
            ? _writer.Json(request, HttpStatus.Ok, response, [new(SessionIdField, id)])
            : _writer.Empty(request, HttpStatus.ServiceUnavailable, [new("Retry-After", "60")]);
    }

    private async Task<HttpAnswer> AnswerInSessionAsync(HttpRequest request, string? login, JsonRpcIncoming incoming, byte[] message, CancellationToken cancellationToken)
    {
        (McpHttpSession? session, HttpAnswer? refusal) = FindSession(request, login);
        if (session is null)
        {
            return refusal!;
        }

        if (incoming is not JsonRpcRequest call)
        {
            await session.AcceptAsync(incoming, message, cancellationToken).ConfigureAwait(false);
            return _writer.Empty(request, Accepted);
        }

        // ADR-0144, BL-943: the fault plan is asked before anything is written, so a delay holds the whole
        // answer (the event-stream head too) and a drop writes nothing.
        McpFaultOutcome fault = await McpRequestFaults.ApplyAsync(_context, call, login, _settings.EndpointPath).ConfigureAwait(false);
        if (fault.IsDropped)
        {
            return DroppedAnswer;
        }

        // Decision 4.3: a tools/call's event-stream head goes out at once and the call runs while it is streamed.
        return call.Method == ToolsCallMethod
            ? _writer.Streamed(
                request,
                McpEventStream.MediaType,
                (writeAsync, streamCancellation) => McpEventStream.WriteAsync(
                    AnswerRequestAsync(session, call, fault, message, streamCancellation), writeAsync, _context.TimeProvider, streamCancellation))
            : _writer.Json(request, HttpStatus.Ok, (await AnswerRequestAsync(session, call, fault, message, cancellationToken).ConfigureAwait(false))!);
    }

    // What a drop rule answers with: nothing is written and the connection is aborted (ADR-0141, amendment 1).
    private static readonly HttpAnswer DroppedAnswer = new(
        new HttpStatus(500, string.Empty), [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false, DropsExchange: true);

    // ADR-0144: a chosen-error fault rule answers the request in place of the core.
    private static async Task<byte[]?> AnswerRequestAsync(McpHttpSession session, JsonRpcRequest call, McpFaultOutcome fault, byte[] message, CancellationToken cancellationToken) =>
        fault.ChosenError ?? await session.AnswerRequestAsync(call, message, cancellationToken).ConfigureAwait(false);

    private HttpAnswer AnswerDelete(HttpRequest request, string? login)
    {
        (McpHttpSession? session, HttpAnswer? refusal) = FindSession(request, login);
        if (session is null)
        {
            return refusal!;
        }

        _settings.Sessions.End(session);
        return _writer.Empty(request, NoContent, contentLength: false);
    }

    // An unsupported MCP-Protocol-Version or a missing Mcp-Session-Id is 400; a session that is not live, or
    // is another login's, is 404.
    private (McpHttpSession? Session, HttpAnswer? Refusal) FindSession(HttpRequest request, string? login)
    {
        string version = McpHttpFields.Field(request, "MCP-Protocol-Version") ?? AbsentProtocolVersion;
        if (!McpProtocolVersions.Supported.Contains(version, StringComparer.Ordinal) || McpHttpFields.Field(request, SessionIdField) is not { } id)
        {
            return (null, _writer.Empty(request, HttpStatus.BadRequest));
        }

        return _settings.Sessions.Find(id, login) is { } session
            ? (session, null)
            : (null, _writer.Empty(request, HttpStatus.NotFound));
    }

    private string Issuer(HttpRequest request) => $"{_httpScheme}://{request.Authority}";

    // The resource a token is bound to (RFC 8707) and the metadata's "resource" (RFC 9728).
    private string Resource(HttpRequest request) => Issuer(request) + _settings.EndpointPath;

    private static readonly HttpStatus Accepted = new(202, "Accepted");

    private static readonly HttpStatus NoContent = new(204, "No Content");

    private static readonly HttpStatus NotAcceptable = new(406, "Not Acceptable");

    private static readonly HttpStatus UnsupportedMediaType = new(415, "Unsupported Media Type");
}
