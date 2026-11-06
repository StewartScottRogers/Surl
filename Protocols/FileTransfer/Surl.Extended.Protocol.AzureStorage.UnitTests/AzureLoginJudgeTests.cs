using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// How the Azure Storage server asks for a Shared Key or shared access signature login and words
/// each refusal (ADR-0109 decisions 3.2 to 3.4). The session's verdict is scripted here; the
/// signatures themselves are checked in <c>Surl.Authentication</c>'s <c>AzureStorageVerifierTests</c>.
/// </summary>
[TestClass]
public sealed class AzureLoginJudgeTests
{
    private const string AuthenticationFailedMessage =
        "Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature.";

    private const string SharedKeyFields = "Authorization: SharedKey account:c2lnbmF0dXJl";

    private static readonly string[] SharedKeyRequest = ["x-ms-version: 2025-01-05", "x-ms-date: Thu, 05 Nov 2026 12:00:00 GMT", SharedKeyFields];

    private static AzureServerHarness Readable()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        return harness;
    }

    [TestMethod]
    public async Task LogInAsync_SharedKey_TellsTheSessionTheAccountResourcePermissionAndPeer()
    {
        var harness = Readable();

        await harness.SendAsync("GET", "/photos/a%20b.txt", SharedKeyRequest);

        Assert.AreEqual(
            new AzureStorageScope("account", "/photos/a b.txt", "r", IPAddress.Loopback),
            harness.Policy.Requests[0].AzureStorage);
    }

    [TestMethod]
    public async Task LogInAsync_AccountRequest_NamesTheAccountAsTheResource()
    {
        var harness = Readable();

        await harness.SendAsync("GET", "/?comp=list", SharedKeyRequest);

        Assert.AreEqual("/", harness.Policy.Requests[0].AzureStorage!.ResourcePath);
    }

    [TestMethod]
    public async Task LogInAsync_HostWithAPort_NamesItsFirstLabelAsTheAccount()
    {
        var harness = Readable();

        await harness.SendRawAsync("GET /photos/a.txt HTTP/1.1\r\nHost: localhost:10000\r\nx-ms-version: 2025-01-05\r\nConnection: close\r\n\r\n");

        Assert.AreEqual("localhost", harness.Policy.Requests[0].AzureStorage!.AccountName);
    }

    [TestMethod]
    public async Task ServeAsync_VerifiedSharedKey_ServesTheBlobAsTheAccount()
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "account");

        var response = await harness.SendAsync("GET", "/photos/a.txt", SharedKeyRequest);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    [DataRow(HttpAuthenticationRefusal.BadSignature, "Signature did not match.", DisplayName = "a wrong signature")]
    [DataRow(HttpAuthenticationRefusal.UnknownAccount, "Signature did not match.", DisplayName = "an unknown account")]
    [DataRow(HttpAuthenticationRefusal.ClockSkew, "Request date header too old.", DisplayName = "a date more than 15 minutes off")]
    [DataRow(HttpAuthenticationRefusal.MissingRequestDate, "Request date header not specified.", DisplayName = "no date")]
    [DataRow(HttpAuthenticationRefusal.SignatureExpired, "Signed expiry time has passed.", DisplayName = "an expired SAS")]
    [DataRow(HttpAuthenticationRefusal.SignatureNotYetValid, "Signed start time is still ahead.", DisplayName = "a SAS not yet valid")]
    [DataRow(HttpAuthenticationRefusal.AddressNotAllowed, "The request address is not in the signed IP range.", DisplayName = "a SAS for other addresses")]
    [DataRow(HttpAuthenticationRefusal.ProtocolNotAllowed, "The signature allows HTTPS only.", DisplayName = "an HTTPS-only SAS over plain text")]
    [DataRow(HttpAuthenticationRefusal.UnservedSignatureKind, "This kind of shared access signature is not served.", DisplayName = "a user delegation SAS")]
    [DataRow(HttpAuthenticationRefusal.UnreadableSignatureFields, "The signed fields of the shared access signature do not read.", DisplayName = "a SAS whose fields do not read")]
    public async Task ServeAsync_RefusedSignature_Is403AuthenticationFailedWithItsDetail(HttpAuthenticationRefusal refusal, string detail)
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null, Refusal: refusal);

        var response = await harness.SendAsync("GET", "/photos/a.txt", SharedKeyRequest);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "403 Forbidden", "AuthenticationFailed", AuthenticationFailedMessage,
                $"<AuthenticationErrorDetail>{detail}</AuthenticationErrorDetail>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_SharedKeyThatDoesNotParse_Is400InvalidAuthenticationInfo()
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null, Refusal: HttpAuthenticationRefusal.MalformedCredentials);

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05", "Authorization: SharedKey account");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidAuthenticationInfo",
                "Authentication information is not given in the correct format. Check the value of Authorization header."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_SasLackingThePermission_Is403AuthorizationPermissionMismatch()
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null, Refusal: HttpAuthenticationRefusal.PermissionNotGranted);

        var response = await harness.SendAsync("GET", "/photos/a.txt?sv=2025-01-05&sr=b&sp=w&se=2026-11-06&sig=c2ln", "x-ms-version: 2025-01-05");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_DataLakeSasLackingThePermission_Is403AuthorizationPermissionMismatchInJson()
    {
        var harness = Readable().ServingDataLake();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null, Refusal: HttpAuthenticationRefusal.PermissionNotGranted);

        var response = await harness.SendAsync("GET", "/photos/a.txt?sv=2025-01-05&sr=b&sp=w&se=2026-11-06&sig=c2ln", "x-ms-version: 2025-01-05");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(
                response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_RefusalWithoutAReason_Is401InvalidAuthenticationInfoWithTheChallenge()
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null);

        var response = await harness.SendAsync("GET", "/photos/a.txt", SharedKeyRequest);

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "x-ms-error-code: InvalidAuthenticationInfo\r\n");
        StringAssert.Contains(response, $"WWW-Authenticate: {AzureLoginJudge.BearerChallenge}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_RefusalWithoutAReasonOrCredentials_Is401NoAuthenticationInformation()
    {
        var harness = Readable();
        harness.Policy.Verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, ["Basic realm=\"surl\""], null);

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "x-ms-error-code: NoAuthenticationInformation\r\n");
    }

    [TestMethod]
    public async Task LogInAsync_NoHost_NamesNoAccount()
    {
        var harness = Readable();

        await harness.SendRawAsync("GET /photos/a.txt HTTP/1.0\r\nx-ms-version: 2025-01-05\r\n\r\n");

        Assert.IsNull(harness.Policy.Requests[0].AzureStorage!.AccountName);
    }

    [TestMethod]
    [DataRow("GET", "/photos/a.txt", "r")]
    [DataRow("HEAD", "/photos/a.txt", "r")]
    [DataRow("GET", "/photos?restype=container&comp=list", "l")]
    [DataRow("GET", "/photos?resource=filesystem&recursive=false", "l")]
    [DataRow("GET", "/?resource=account", "l")]
    [DataRow("HEAD", "/photos?resource=filesystem", "r")]
    [DataRow("GET", "/?comp=list", "l")]
    [DataRow("DELETE", "/photos/a.txt", "d")]
    [DataRow("PUT", "/photos/a.txt", "w")]
    [DataRow("PATCH", "/photos/a.txt?action=append&position=0", "aw")]
    [DataRow("PATCH", "/photos/a.txt?action=flush&position=5", "w")]
    public void GrantingPermissionsOf_EachOperation_IsTheLettersThatGrantIt(string method, string target, string expected)
    {
        var permissions = AzureLoginJudge.GrantingPermissionsOf(method, AzureBlobTarget.Parse(target));

        Assert.AreEqual(expected, permissions);
    }

    [TestMethod]
    public void PeerAddressOf_PeerWithNoIpAddress_IsNone()
    {
        var harness = new AzureServerHarness();
        var context = harness.Context() with { RemoteEndPoint = UnnamedUnixSocketPeer.Instance };

        var address = AzureStorageProtocolServer.PeerAddressOf(context);

        Assert.AreEqual(IPAddress.None, address);
    }
}
