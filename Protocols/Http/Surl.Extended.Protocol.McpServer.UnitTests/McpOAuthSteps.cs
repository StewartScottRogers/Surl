using System.Text;
using static Surl.Extended.Protocol.McpServer.McpHttpHarness;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The constants and client steps the authorization server's tests share: RFC 7636 appendix B's verifier and
/// challenge, a loopback redirect URI, alice's Basic login, and registering, authorizing and redeeming through an
/// <see cref="McpHttpHarness" />.
/// </summary>
internal static class McpOAuthSteps
{
    public const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    public const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    public const string RedirectUri = "http://127.0.0.1:33418/callback";

    public const string Resource = "http://127.0.0.1:8080/mcp";

    public const string FormField = "Content-Type: application/x-www-form-urlencoded";

    public const string NoStore = "Cache-Control: no-store\r\n";

    public static readonly string AliceBasic = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:alice-password"));

    public static string AuthorizeTarget(string clientId) =>
        $"/authorize?response_type=code&client_id={clientId}&code_challenge={Challenge}&code_challenge_method=S256";

    public static async Task<string> RegisterAsync(McpHttpHarness harness, string redirectUri = RedirectUri)
    {
        string response = await harness.SendAsync("POST", "/register", $$"""{"redirect_uris":["{{redirectUri}}"]}""", JsonField);
        string body = BodyOf(response);
        int start = body.IndexOf("\"client_id\":\"", StringComparison.Ordinal) + 13;
        return body[start..body.IndexOf('"', start)];
    }

    public static async Task<string> AuthorizeAsync(McpHttpHarness harness, string clientId)
    {
        string response = await harness.SendAsync("GET", AuthorizeTarget(clientId), null, AliceBasic);
        string location = FieldOf(response, "Location")!;
        return location[(location.IndexOf("code=", StringComparison.Ordinal) + 5)..];
    }

    public static Task<string> RedeemAsync(McpHttpHarness harness, string clientId, string code) =>
        harness.SendAsync("POST", "/token", $"grant_type=authorization_code&code={code}&client_id={clientId}&code_verifier={Verifier}", FormField);

    public static async Task<string> AccessTokenByClientCredentialsAsync(McpHttpHarness harness)
    {
        string response = await harness.SendAsync("POST", "/token", "grant_type=client_credentials", FormField, AliceBasic);
        string body = BodyOf(response);
        int start = body.IndexOf("\"access_token\":\"", StringComparison.Ordinal) + 16;
        return body[start..body.IndexOf('"', start)];
    }
}
