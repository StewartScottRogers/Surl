using System.Text.RegularExpressions;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Negotiate, the login check and the hub's other HTTP answers, sent as TransportProtocols.md's
/// "<c>POST [endpoint-base]/negotiate</c>" describes the requests (ADR-0119, decisions 1, 2, 5 and 7).
/// </summary>
[TestClass]
public sealed partial class SignalRRequestAnswererTests
{
    private const string Id = "[A-Za-z0-9_-]{22}";

    [TestMethod]
    public async Task AnswerAsync_NegotiateVersionOne_AnswersAFreshIdAndTokenAndTheWebSocketsTransport()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField, "Content-Length: 0");

        var match = Regex.Match(
            response,
            "^HTTP/1\\.1 200 OK\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: (\\d+)\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n"
                + "(\\{\"negotiateVersion\":1,\"connectionId\":\"(" + Id + ")\",\"connectionToken\":\"(" + Id + ")\","
                + "\"availableTransports\":\\[\\{\"transport\":\"WebSockets\",\"transferFormats\":\\[\"Text\",\"Binary\"\\]\\}\\]\\})$");
        Assert.IsTrue(match.Success, response);
        Assert.AreEqual(match.Groups[2].Length.ToString(System.Globalization.CultureInfo.InvariantCulture), match.Groups[1].Value);
        Assert.AreNotEqual(match.Groups[3].Value, match.Groups[4].Value);
        Assert.AreEqual(1, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateTwice_AnswersFreshIdsEachTime()
    {
        var harness = new SignalRServerHarness();

        var first = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);
        var second = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);

        Assert.AreNotEqual(SignalRServerHarness.BodyOf(first), SignalRServerHarness.BodyOf(second));
        Assert.AreEqual(2, harness.Connections.Count);
    }

    [TestMethod]
    [DataRow("/chat/negotiate")]
    [DataRow("/chat/negotiate?negotiateVersion=0")]
    [DataRow("/chat/negotiate?negotiateVersion=00")]
    public async Task AnswerAsync_NegotiateVersionZero_AnswersAnIdWithoutAToken(string target)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", target, SignalRServerHarness.BearerField);

        Assert.MatchesRegex(
            "^\\{\"negotiateVersion\":0,\"connectionId\":\"" + Id + "\",\"availableTransports\":\\[\\{\"transport\":\"WebSockets\",\"transferFormats\":\\[\"Text\",\"Binary\"\\]\\}\\]\\}$",
            SignalRServerHarness.BodyOf(response));
    }

    [TestMethod]
    [DataRow("2")]
    [DataRow("99999999999999999999")]
    public async Task AnswerAsync_NegotiateVersionAboveOne_AnswersVersionOne(string version)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=" + version, SignalRServerHarness.BearerField);

        Assert.StartsWith("{\"negotiateVersion\":1,", SignalRServerHarness.BodyOf(response));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-1")]
    [DataRow("1.0")]
    [DataRow("one")]
    public async Task AnswerAsync_NegotiateVersionNotANonNegativeInteger_IsBadRequest(string version)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=" + version, SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 400 Bad Request\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateWithoutAToken_IsUnauthorizedWithTheSessionsChallenge()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", "Content-Length: 0");

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nWWW-Authenticate: "
                + UnitTestBearerAuthenticationPolicy.Challenge + "\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(0, harness.Connections.Count);
        Assert.Contains("POST refused by the login check: Challenge.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateWithAForbiddenToken_IsForbidden()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", "Authorization: Bearer " + UnitTestBearerAuthenticationPolicy.ForbiddenToken);

        Assert.AreEqual(
            "HTTP/1.1 403 Forbidden\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_UnknownPathWithoutAToken_IsUnauthorizedNotNotFound()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("GET", "/secret");

        Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateAnonymouslyWhenAllowed_RecordsTheAnonymousCaller()
    {
        var harness = new SignalRServerHarness(allowsAnonymous: true);

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1");

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
        var request = harness.Policy.Requests.Single();
        Assert.AreEqual("POST", request.Method);
        Assert.AreEqual("/chat/negotiate?negotiateVersion=1", request.Target);
        Assert.IsFalse(request.IsWrite);
    }

    [TestMethod]
    public async Task AnswerAsync_WrongTokenWhenAnonymousIsAllowed_IsUnauthorizedNotServedAnonymously()
    {
        var harness = new SignalRServerHarness(allowsAnonymous: true);

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", "Authorization: Bearer wrong");

        Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", response);
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_AccessTokenQueryParameter_IsJudgedAsABearerAuthorization()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1&access_token=" + UnitTestBearerAuthenticationPolicy.Token);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
        Assert.Contains(
            new KeyValuePair<string, string>("Authorization", "Bearer " + UnitTestBearerAuthenticationPolicy.Token),
            harness.Policy.Requests.Single().Fields);
    }

    [TestMethod]
    public async Task AnswerAsync_AccessTokenBesideAnAuthorizationField_IsIgnored()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "/chat/negotiate?access_token=" + UnitTestBearerAuthenticationPolicy.Token, "authorization: Bearer wrong");

        Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", response);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("PUT")]
    public async Task AnswerAsync_NegotiateWithAnotherMethod_IsMethodNotAllowed(string method)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync(method, "/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nAllow: POST\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateWithABody_DiscardsItAndAnswers()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendRawAsync(
            "POST /chat/negotiate?negotiateVersion=1 HTTP/1.1\r\nHost: h\r\n" + SignalRServerHarness.BearerField + "\r\nContent-Length: 4\r\n\r\nbody"
                + "POST /chat/negotiate?negotiateVersion=1 HTTP/1.1\r\nHost: h\r\n" + SignalRServerHarness.BearerField + "\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(2, Regex.Count(response, "HTTP/1\\.1 200 OK\r\n"));
    }

    [TestMethod]
    public async Task AnswerAsync_HubFull_AnswersTheNegotiateErrorForm()
    {
        var harness = new SignalRServerHarness();
        for (var index = 0; index < SignalRHubConnections.MaxConnections; index++)
        {
            harness.Connections.Negotiate(null, true, TimeSpan.FromSeconds(30));
        }

        var response = await harness.SendAsync("POST", "/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 48\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n"
                + "{\"error\":\"The server has too many connections.\"}",
            response);
        Assert.Contains("Negotiate refused: the hub holds 1024 connections.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task AnswerAsync_RootHub_NegotiatesOnSlashNegotiate()
    {
        var harness = new SignalRServerHarness(listenPath: "/");

        var response = await harness.SendAsync("POST", "/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_AbsoluteFormTarget_IsAnsweredByItsPath()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", "http://127.0.0.1:5000/chat/negotiate?negotiateVersion=1", SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
    }

    [TestMethod]
    [DataRow("/other")]
    [DataRow("/chat/")]
    [DataRow("/Chat/negotiate")]
    [DataRow("/chat/negotiate/")]
    public async Task AnswerAsync_PathTheHubDoesNotServe_IsNotFound(string target)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("POST", target, SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 404 Not Found\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_GetOnTheEndpointBaseWithoutAnUpgrade_IsUpgradeRequired()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("GET", "/chat?id=x", SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 426 Upgrade Required\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nUpgrade: websocket\r\nSec-WebSocket-Version: 13\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AnswerAsync_UpgradeNamingANegotiatedConnection_Switches(bool isVersionOne)
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, isVersionOne, TimeSpan.FromSeconds(30))!;

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat?id=" + Uri.EscapeDataString(negotiated.UpgradeId)));

        Assert.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", response);
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeByAccessTokenWithoutAnId_SwitchesToANewConnection()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat?access_token=" + UnitTestBearerAuthenticationPolicy.Token, "X-Unit-Test: 1"));

        Assert.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", response);
    }

    [TestMethod]
    [DataRow("/chat?id=not-from-negotiate", 404, "Not Found")]
    [DataRow("/chat?id=", 404, "Not Found")]
    public async Task AnswerAsync_UpgradeWithAConnectionTokenNotFromNegotiate_IsNotFound(string target, int status, string reason)
    {
        var harness = new SignalRServerHarness();
        harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30));

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest(target));

        Assert.AreEqual($"HTTP/1.1 {status} {reason}\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\n\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeWithAVersionOneConnectionsId_IsNotFound()
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat?id=" + negotiated.ConnectionId));

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeOfAConnectionAlreadyTakenUp_IsConflict()
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, string.Empty, out _);

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat?id=" + negotiated.UpgradeId));

        Assert.StartsWith("HTTP/1.1 409 Conflict\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeByACallerOtherThanTheNegotiator_IsForbidden()
    {
        var harness = new SignalRServerHarness(allowsAnonymous: true);
        var negotiated = harness.Connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat?id=" + negotiated.UpgradeId));

        Assert.StartsWith("HTTP/1.1 403 Forbidden\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeWithoutAnIdOnAFullHub_IsServiceUnavailable()
    {
        var harness = new SignalRServerHarness();
        for (var index = 0; index < SignalRHubConnections.MaxConnections; index++)
        {
            harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30));
        }

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest("/chat"));

        Assert.StartsWith("HTTP/1.1 503 Service Unavailable\r\n", response);
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("DELETE")]
    public async Task AnswerAsync_OtherMethodOnTheEndpointBase_IsMethodNotAllowed(string method)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync(method, "/chat?id=x", SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nAllow: GET\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow(HttpRequestBodyDiscardOutcome.TooLarge, 413)]
    [DataRow(HttpRequestBodyDiscardOutcome.Incomplete, 400)]
    public async Task AnswerAsync_NegotiateBodyNotDiscardedWhole_IsRefused(HttpRequestBodyDiscardOutcome outcome, int status)
    {
        var harness = new SignalRServerHarness();
        var body = new UnitTestRequestBody(isAnnounced: true, isReadable: true, outcome);

        var answer = await AnswerDirectlyAsync(harness, body);

        Assert.AreEqual(status, answer.Status.Code);
        Assert.IsTrue(answer.IsRefusal);
        Assert.IsTrue(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateBodyNotReadable_AnswersAndLeavesItUnread()
    {
        var harness = new SignalRServerHarness();
        var body = new UnitTestRequestBody(isAnnounced: true, isReadable: false, HttpRequestBodyDiscardOutcome.Discarded);

        var answer = await AnswerDirectlyAsync(harness, body);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.IsTrue(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(0, body.Discards);
    }

    [TestMethod]
    public async Task AnswerAsync_NegotiateBodyDiscarded_LeavesNothingUnread()
    {
        var harness = new SignalRServerHarness();
        var body = new UnitTestRequestBody(isAnnounced: true, isReadable: true, HttpRequestBodyDiscardOutcome.Discarded);

        var answer = await AnswerDirectlyAsync(harness, body);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.IsFalse(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(1, body.Discards);
    }

    private static Task<HttpAnswer> AnswerDirectlyAsync(SignalRServerHarness harness, IHttpRequestBody body)
    {
        var answerer = new SignalRRequestAnswerer(
            SignalRHubPath.FromListenPath("/chat"),
            harness.Connections,
            harness.Policy.StartHttpConnection(null),
            harness.Context());
        var request = new HttpRequest("POST", "/chat/negotiate?negotiateVersion=1", "h", [new("Authorization", "Bearer " + UnitTestBearerAuthenticationPolicy.Token)], body);

        return answerer.AnswerAsync(request, CancellationToken.None);
    }
}
