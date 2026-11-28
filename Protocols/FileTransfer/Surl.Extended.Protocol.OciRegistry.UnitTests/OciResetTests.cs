using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Aborting every open upload session, revoking every token, and asking the policy whether it still
/// honours a token issued to an account, as a whole-instance reset or restore, a removed account and a
/// directory user need (ADR-0167, decisions 2 and 4 and Amendment 3; BL-1232, BL-2019).
/// </summary>
[TestClass]
public sealed class OciResetTests
{
    private static readonly string Session1 = H.SessionId(1);

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AnOpenSession_MakesALaterPatchAndPutAnswer404BlobUploadUnknown()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        await harness.Server.AbortOpenUploadsAsync();
        var patched = await harness.SendWithBodyAsync("PATCH", $"/v2/app/blobs/uploads/{Session1}", "hello", "Content-Range: 0-4");
        var put = await harness.SendWithBodyAsync("PUT", $"/v2/app/blobs/uploads/{Session1}?digest={H.Sha256Of("hello")}", "hello");

        StringAssert.StartsWith(patched, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(patched, "BLOB_UPLOAD_UNKNOWN");
        StringAssert.StartsWith(put, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(put, "BLOB_UPLOAD_UNKNOWN");
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AThousandOpenSessions_FreesEveryPlace()
    {
        var harness = H.AllowingUploads();
        for (var opened = 0; opened < OciUploadSessions.MaxOpen; opened++)
        {
            await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        }

        await harness.Server.AbortOpenUploadsAsync();
        var response = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_NothingOpen_Completes()
    {
        await H.AllowingUploads().Server.AbortOpenUploadsAsync();
    }

    [TestMethod]
    public async Task RevokeTokens_AnIssuedToken_IsAnswered401WithTheInvalidTokenChallenge()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:app:pull");
        StringAssert.StartsWith(await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {token}"), "HTTP/1.1 200 OK\r\n");

        harness.Server.RevokeTokens();
        var response = await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {token}");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "error=\"invalid_token\"");
    }

    [TestMethod]
    public async Task CheckAsync_TokenOfARemovedAccount_IsAnswered401WithTheInvalidTokenChallenge()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:app:pull");
        harness.Policy.Accounts.Remove(UnitTestAuthenticationPolicy.UserName);

        var response = await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {token}");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "error=\"invalid_token\"");
    }

    [TestMethod]
    public async Task CheckAsync_AnAnonymousTokenAfterAnAccountIsRemoved_IsStillServed()
    {
        var harness = Registry();
        harness.Policy.AllowsAnonymous = true;
        var token = await harness.FetchTokenAsAsync("Accept: */*", "repository:app:pull");
        harness.Policy.Accounts.Clear();

        var response = await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {token}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_TokenThePolicyHonoursForANameThatIsNoAccount_ServesV2()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:app:pull");
        harness.Policy.Accounts.Remove(UnitTestAuthenticationPolicy.UserName);
        harness.Policy.HonoursTokens = true;

        var response = await harness.SendAsync("GET", "/v2/", $"Authorization: Bearer {token}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_TokenOfANamedAccountThePolicyDoesNotHonour_IsAnswered401WithTheInvalidTokenChallenge()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:app:pull");
        harness.Policy.HonoursTokens = false;

        var response = await harness.SendAsync("GET", "/v2/", $"Authorization: Bearer {token}");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "error=\"invalid_token\"");
    }

    private static H Registry()
    {
        var harness = H.AllowingUploads();
        harness.SendsTokens = false;
        harness.CreateRepository("app");

        return harness;
    }
}
