using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.McpServer.McpHttpHarness;

namespace Surl.Extended.Protocol.McpServer;

// Every request and every expected answer is written from MCP 2025-06-18 "Base Protocol > Transports > Streamable
// HTTP" and "Authorization", RFC 9728, RFC 6750 and the WHATWG HTML "Server-sent events" section, as ADR-0111
// decisions 4, 6 and 7 read them.
[TestClass]
public sealed class McpProtocolServerTests
{
    private const string InstructionsAsWritten =
        "This server lists, reads and, when uploads are allowed, writes the files Surl serves. Paths are \\u0027/\\u0027-separated and relative to the served directory.";

    private const string InitializeResult =
        $$$"""{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"surl","title":"Surl MCP server","version":"1.2.3"},"instructions":"{{{InstructionsAsWritten}}}"}}""";

    private const string Challenge = "Bearer resource_metadata=\"http://127.0.0.1:8080/.well-known/oauth-protected-resource/mcp\"";

    [TestMethod]
    public async Task ServeAsync_InitializeByPost_AnswersJsonWithAnMcpSessionId()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync(InitializeMessage);

        Assert.AreEqual(BodyAnswer("200 OK", "application/json", InitializeResult, $"Mcp-Session-Id: {SessionIdNumber(1)}\r\n"), response);
        Assert.AreEqual(43, SessionIdNumber(1).Length);
    }

    [TestMethod]
    public async Task ServeAsync_LaterRequestWithTheSessionId_IsAnswered()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}", "MCP-Protocol-Version: 2025-06-18");

        Assert.AreEqual(BodyAnswer("200 OK", "application/json", """{"jsonrpc":"2.0","id":2,"result":{}}"""), response);
    }

    [TestMethod]
    public async Task ServeAsync_RequestWithoutAProtocolVersion_IsTakenAs20250326AndAnswered()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_RequestWithoutASessionIdAfterInitialization_Gets400()
    {
        McpHttpHarness harness = new();
        await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownSession_Gets404()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", "Mcp-Session-Id: no-such-session");

        Assert.AreEqual(EmptyAnswer("404 Not Found"), response);
    }

    [TestMethod]
    public async Task ServeAsync_SessionOfAnotherLogin_Gets404()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.SendAsync(
            "POST", "/mcp", """{"jsonrpc":"2.0","id":2,"method":"ping"}""",
            $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.AliceToken}", AcceptField, JsonField, $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(EmptyAnswer("404 Not Found"), response);
    }

    [TestMethod]
    public async Task ServeAsync_SessionOfANamedAccount_IsAnsweredForThatAccount()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync($"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.AliceToken}");

        string response = await harness.SendAsync(
            "POST", "/mcp", """{"jsonrpc":"2.0","id":2,"method":"ping"}""",
            $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.AliceToken}", AcceptField, JsonField, $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_SessionIdleForThirtyMinutes_HasExpiredAndGets404()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();
        harness.TimeProvider.UtcNow += TimeSpan.FromMinutes(30);

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_SessionUsedWithinThirtyMinutes_StaysLive()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();
        harness.TimeProvider.UtcNow += TimeSpan.FromMinutes(29);
        await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}");
        harness.TimeProvider.UtcNow += TimeSpan.FromMinutes(29);

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":3,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_Notification_Gets202WithNoBody()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(EmptyAnswer("202 Accepted"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResponse_Gets202WithNoBody()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":"s1","result":{}}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(EmptyAnswer("202 Accepted"), response);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledNotificationForNoRunningRequest_Gets202()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync(
            """{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":9}}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(EmptyAnswer("202 Accepted"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ToolsCall_IsAnsweredOnAnEventStreamWithOneDataEvent()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""", $"Mcp-Session-Id: {sessionId}");

        // One event of the default type: its data line, then the blank line that dispatches it; no id: field.
        Assert.AreEqual(
            EventStreamAnswer("data: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"hi\"}],\"isError\":false}}\n\n"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ToolsListOtherThanACall_IsAnsweredWithJson()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("application/json", FieldOf(response, "Content-Type"));
        StringAssert.Contains(BodyOf(response), "\"name\":\"echo\"");
    }

    [TestMethod]
    [DataRow("http://evil.example")]
    [DataRow("null")]
    [DataRow("http://127.0.0.1:8080/path")]
    [DataRow("https://127.0.0.1.evil.example:8080")]
    public async Task ServeAsync_RefusedOrigin_Gets403(string origin)
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync(InitializeMessage, $"Origin: {origin}");

        Assert.AreEqual(EmptyAnswer("403 Forbidden"), response);
    }

    [TestMethod]
    [DataRow("http://localhost:3000")]
    [DataRow("http://127.0.0.1:8080")]
    [DataRow("http://[::1]")]
    public async Task ServeAsync_LoopbackOrSameOrigin_IsServed(string origin)
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync(InitializeMessage, $"Origin: {origin}");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_SameOriginOverTls_IsServedAsHttps()
    {
        McpHttpHarness harness = new();
        string request = $"POST /mcp HTTP/1.1\r\nHost: mcp.example:8443\r\nOrigin: https://mcp.example:8443\r\n{BearerField}\r\n{AcceptField}\r\n{JsonField}\r\n"
            + $"Content-Length: {InitializeMessage.Length}\r\nConnection: close\r\n\r\n{InitializeMessage}";

        string response = await harness.SendRawAsync(request, InMemoryConnection.DefaultUpgradeTlsSession);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, harness.Policy.StartedConnections.Single());
    }

    [TestMethod]
    [DataRow("1999-01-01")]
    [DataRow("2025-06-18x")]
    public async Task ServeAsync_UnsupportedProtocolVersion_Gets400(string version)
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}", $"MCP-Protocol-Version: {version}");

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_Delete_EndsTheSessionWith204AndItIsThen404()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        string deleted = await harness.SendAsync("DELETE", "/mcp", null, BearerField, $"Mcp-Session-Id: {sessionId}");
        string after = await harness.PostAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\nDate: {NowHttpDate}\r\nServer: surl\r\nConnection: close\r\n\r\n", deleted);
        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLineOf(after));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteWithoutASessionId_Gets400()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("DELETE", "/mcp", null, BearerField);

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteOfAnUnknownSession_Gets404()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("DELETE", "/mcp", null, BearerField, "Mcp-Session-Id: gone");

        Assert.AreEqual(EmptyAnswer("404 Not Found"), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoTokenWithAnonymousOff_Gets401WithTheResourceMetadataChallenge()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, AcceptField, JsonField);

        Assert.AreEqual(EmptyAnswer("401 Unauthorized", $"WWW-Authenticate: {Challenge}\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_TokenNamingNoAccount_Gets401WithInvalidToken()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, "Authorization: Bearer wrong", AcceptField, JsonField);

        Assert.AreEqual(EmptyAnswer("401 Unauthorized", $"WWW-Authenticate: {Challenge}, error=\"invalid_token\"\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_TokenInTheQueryString_IsNotReadAndGets401()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp?access_token=" + UnitTestBearerAuthenticationPolicy.Token, InitializeMessage, AcceptField, JsonField);

        Assert.AreEqual(EmptyAnswer("401 Unauthorized", $"WWW-Authenticate: {Challenge}\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoTokenWithAnonymousOn_IsServed()
    {
        McpHttpHarness harness = new(allowsAnonymous: true);

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, AcceptField, JsonField);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_AnotherAuthorizationScheme_Gets400()
    {
        McpHttpHarness harness = new(allowsAnonymous: true);

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, "Authorization: Basic dXNlcjpwYXNz", AcceptField, JsonField);

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataDocument_IsServedWithoutAuthorization()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("GET", "/.well-known/oauth-protected-resource/mcp", null);

        Assert.AreEqual(
            BodyAnswer(
                "200 OK",
                "application/json",
                """{"resource":"http://127.0.0.1:8080/mcp","authorization_servers":["http://127.0.0.1:8080"],"bearer_methods_supported":["header"],"resource_name":"Surl MCP server"}"""),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataDocumentByPost_Gets405AllowingGet()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/.well-known/oauth-protected-resource/mcp", "{}");

        Assert.AreEqual(EmptyAnswer("405 Method Not Allowed", "Allow: GET\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataDocumentFromAnotherOrigin_Gets403()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("GET", "/.well-known/oauth-protected-resource/mcp", null, "Origin: http://evil.example");

        Assert.AreEqual(EmptyAnswer("403 Forbidden"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnotherPath_Gets404()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/other", InitializeMessage, BearerField, AcceptField, JsonField);

        Assert.AreEqual(EmptyAnswer("404 Not Found"), response);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("PUT")]
    public async Task ServeAsync_GetOrAnotherMethodOnTheEndpoint_Gets405AllowingPostAndDelete(string method)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync(method, "/mcp", null, BearerField);

        Assert.AreEqual(EmptyAnswer("405 Method Not Allowed", "Allow: POST, DELETE\r\n"), response);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Accept: application/json")]
    [DataRow("Accept: text/event-stream")]
    public async Task ServeAsync_AcceptMissingAType_Gets406(string? accept)
    {
        McpHttpHarness harness = new();
        string[] fields = accept is null ? [BearerField, JsonField] : [BearerField, accept, JsonField];

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, fields);

        Assert.AreEqual(EmptyAnswer("406 Not Acceptable"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AcceptSplitOverTwoFields_IsServed()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, BearerField, "Accept: application/json", "Accept: text/event-stream", JsonField);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Content-Type: text/plain")]
    public async Task ServeAsync_ContentTypeNotJson_Gets415(string? contentType)
    {
        McpHttpHarness harness = new();
        string[] fields = contentType is null ? [BearerField, AcceptField] : [BearerField, AcceptField, contentType];

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, fields);

        Assert.AreEqual(EmptyAnswer("415 Unsupported Media Type"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ContentTypeJsonWithACharset_IsServed()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, BearerField, AcceptField, "Content-Type: Application/JSON; charset=utf-8");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_BodyThatIsNotJson_Gets400WithTheCoresParseErrorAndANullId()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync("{not json");

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Parse error"}}"""), response);
    }

    [TestMethod]
    public async Task ServeAsync_InvalidRequestWithAnId_Gets400WithANullId()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync("""{"jsonrpc":"1.0","id":7,"method":"ping"}""");

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request"}}"""), response);
    }

    [TestMethod]
    public async Task ServeAsync_PostWithNoBody_Gets400WithAParseError()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/mcp", null, BearerField, AcceptField, JsonField);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
        StringAssert.Contains(BodyOf(response), "-32700");
    }

    [TestMethod]
    public async Task ServeAsync_InitializeNamingASession_Gets400()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync(InitializeMessage, "Mcp-Session-Id: anything");

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_InitializeTheCoreRefuses_IsAnsweredWithItsErrorAndNoSession()
    {
        McpHttpHarness harness = new();

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}""");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
        Assert.IsNull(FieldOf(response, "Mcp-Session-Id"));
        StringAssert.Contains(BodyOf(response), "-32602");
        Assert.AreEqual(0, harness.IdSource.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_InitializePastTheSessionBound_Gets503WithRetryAfter()
    {
        McpHttpHarness harness = new();
        for (int session = 0; session < McpHttpSessionTable.MaximumSessions; session++)
        {
            await harness.InitializeAsync();
        }

        string response = await harness.PostAsync(InitializeMessage);

        Assert.AreEqual(EmptyAnswer("503 Service Unavailable", "Retry-After: 60\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BodyAnnouncedPastTheMessageLimit_Gets413AndCloses()
    {
        McpHttpHarness harness = new() { Limits = ExchangeLimits.Default with { MaxMessageBytes = 10 } };

        string response = await harness.PostAsync(InitializeMessage);

        Assert.AreEqual(EmptyAnswer("413 Content Too Large"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBodyPastTheMessageLimit_Gets413()
    {
        McpHttpHarness harness = new() { Limits = ExchangeLimits.Default with { MaxMessageBytes = 10 } };

        string response = await harness.SendRawAsync(ChunkedPost("0123456789AB"));

        Assert.AreEqual(EmptyAnswer("413 Content Too Large"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBodyPastTheUploadLimit_Gets413()
    {
        McpHttpHarness harness = new() { Limits = ExchangeLimits.Default with { MaxUploadBytes = 4 } };

        string response = await harness.SendRawAsync(ChunkedPost("0123456789AB"));

        Assert.AreEqual(EmptyAnswer("413 Content Too Large"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBodyWithinTheLimit_IsRead()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendRawAsync(ChunkedPost(InitializeMessage));

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_NoMessageLimit_ReadsTheBody()
    {
        McpHttpHarness harness = new() { Limits = ExchangeLimits.Default with { MaxMessageBytes = 0 } };

        string response = await harness.PostAsync(InitializeMessage);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_MessageLimitPastAnInt_ReadsTheBody()
    {
        McpHttpHarness harness = new() { Limits = ExchangeLimits.Default with { MaxMessageBytes = (long)int.MaxValue + 1 } };

        string response = await harness.PostAsync(InitializeMessage);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_BodyCutShort_Gets400AndCloses()
    {
        McpHttpHarness harness = new();
        string request = $"POST /mcp HTTP/1.1\r\nHost: {Host}\r\n{BearerField}\r\n{AcceptField}\r\n{JsonField}\r\nContent-Length: 100\r\n\r\n{{\"jsonrpc\"";

        string response = await harness.SendRawAsync(request);

        Assert.AreEqual(EmptyAnswer("400 Bad Request"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BodyInATransferCodingItCannotRead_Gets400AndCloses()
    {
        McpHttpHarness harness = new();
        string request = $"POST /mcp HTTP/1.1\r\nHost: {Host}\r\n{BearerField}\r\n{AcceptField}\r\n{JsonField}\r\nTransfer-Encoding: gzip\r\n\r\nxyz";

        string response = await harness.SendRawAsync(request);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListenPathOfItsOwn_IsTheEndpointAndItsMetadataPath()
    {
        McpHttpHarness harness = new(listenPath: "/agents/mcp");

        string answered = await harness.SendAsync("POST", "/agents/mcp?x=1", InitializeMessage, BearerField, AcceptField, JsonField);
        string metadata = await harness.SendAsync("GET", "/.well-known/oauth-protected-resource/agents/mcp", null);
        string defaultPath = await harness.SendAsync("POST", "/mcp", InitializeMessage, BearerField, AcceptField, JsonField);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(answered));
        StringAssert.Contains(BodyOf(metadata), "\"resource\":\"http://127.0.0.1:8080/agents/mcp\"");
        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLineOf(defaultPath));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("/")]
    public void Constructor_NoListenPath_IsTheDefaultEndpoint(string? listenPath)
    {
        McpHttpHarness harness = new(listenPath: listenPath);

        Assert.AreEqual("/mcp", harness.Server.EndpointPath);
        CollectionAssert.AreEqual(new[] { "mcp", "mcps" }, harness.Server.Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_ListenPathWithoutASlash_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new McpHttpHarness(listenPath: "mcp"));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        ContentStore store = new McpHttpHarness().Store;
        AnonymousAuthenticationPolicy policy = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => new McpProtocolServer(null!, policy, "1", TimeProvider.System, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpProtocolServer(store, null!, "1", TimeProvider.System, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentException>(() => new McpProtocolServer(store, policy, " ", TimeProvider.System, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpProtocolServer(store, policy, "1", null!, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpProtocolServer(store, policy, "1", TimeProvider.System, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        McpHttpHarness harness = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    private static string ChunkedPost(string body) =>
        $"POST /mcp HTTP/1.1\r\nHost: {Host}\r\n{BearerField}\r\n{AcceptField}\r\n{JsonField}\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n"
        + $"{Encoding.UTF8.GetByteCount(body):x}\r\n{body}\r\n0\r\n\r\n";

    [TestMethod]
    public async Task WriteRefusalAsync_Writes503WithNoBodyAndCompletesWrites()
    {
        McpHttpHarness harness = new();
        InMemoryConnection connection = new([]);

        await harness.Server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        Assert.AreEqual(EmptyAnswer("503 Service Unavailable"), Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        McpHttpHarness harness = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await harness.Server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None));
    }
}
