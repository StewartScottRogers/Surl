using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Checks the logins of a request naming its <see cref="HttpAuthenticationRequest.AzureStorage"/>
/// (ADR-0109 decisions 3.1 to 3.4): an <c>Authorization: Bearer</c> token against the empty-name
/// accounts, an <c>Authorization: SharedKey</c> or <c>SharedKeyLite</c> signature, or a shared
/// access signature in the query, each signature against the account its name finds, keyed with
/// the account password's base64 text (ADR-0100 decision 4.1). An unknown account is checked
/// against a random key, so it costs the same. It holds no per-connection state.
/// </summary>
/// <param name="accounts">The configured accounts.</param>
/// <param name="timeProvider">The clock request dates and signed times are checked against.</param>
internal sealed class AzureStorageVerifier(AccountBook accounts, TimeProvider timeProvider)
{
    /// <summary>
    /// How far a Shared Key request's date, or a shared access signature's signed times, may be
    /// from the server's time (ADR-0109 decisions 3.2 and 3.3).
    /// </summary>
    public static readonly TimeSpan RequestTimeWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The login method a shared access signature is written as in a login note.
    /// </summary>
    public const string SharedAccessSignatureMethod = "SAS";

    internal const string BearerScheme = "Bearer";

    /// <summary>
    /// Checks the request's credentials.
    /// </summary>
    /// <param name="request">The request, with its <see cref="HttpAuthenticationRequest.AzureStorage"/> set.</param>
    /// <param name="isEncrypted">Whether the connection is under TLS, which a signature's <c>spr=https</c> needs.</param>
    /// <returns>
    /// <see langword="null"/> when the request carries no credentials; otherwise the check, with
    /// the method checked (<see langword="null"/> when nothing could be checked).
    /// </returns>
    public AzureStorageCheck? Verify(HttpAuthenticationRequest request, bool isEncrypted)
    {
        var scope = request.AzureStorage!;
        var target = AzureStorageTarget.Parse(request.Target);
        var signature = AzureSharedAccessSignature.Find(target);
        var authorization = FieldValue(request.Fields, "Authorization");
        if (authorization is null)
        {
            return signature is null ? null : VerifySharedAccessSignature(signature, scope, isEncrypted);
        }

        // A shared access signature with an Authorization as well is malformed (decision 3.3).
        return signature is null
            ? VerifyAuthorization(authorization, request, target)
            : Unchecked(HttpAuthenticationRefusal.MalformedCredentials);
    }

    private AzureStorageCheck VerifyAuthorization(string authorization, HttpAuthenticationRequest request, AzureStorageTarget target)
    {
        var space = authorization.IndexOf(' ', StringComparison.Ordinal);
        var scheme = space < 0 ? authorization : authorization[..space];
        var credentials = space < 0 ? string.Empty : authorization[(space + 1)..].Trim(' ');

        return scheme switch
        {
            _ when string.Equals(scheme, BearerScheme, StringComparison.OrdinalIgnoreCase) && credentials.Length > 0 => VerifyBearerToken(credentials),
            AzureSharedKeyAuthorization.SharedKeyScheme or AzureSharedKeyAuthorization.SharedKeyLiteScheme =>
                AzureSharedKeyAuthorization.TryParse(scheme == AzureSharedKeyAuthorization.SharedKeyLiteScheme, credentials) is { } sharedKey
                    ? VerifySharedKey(sharedKey, request, target)
                    : Unchecked(HttpAuthenticationRefusal.MalformedCredentials),
            _ => Unchecked(HttpAuthenticationRefusal.UnsupportedScheme),
        };
    }

    // "Authorize with Microsoft Entra ID": an opaque token, compared with the empty-name accounts'
    // passwords in constant time, as BearerAuthenticationMethod compares it.
    private AzureStorageCheck VerifyBearerToken(string token)
    {
        var check = accounts.CheckBearerToken(Encoding.Latin1.GetBytes(token))
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, string.Empty, [], CheckedLogin.BearerTokenUser)
            : new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [], CheckedLogin.BearerTokenUser, Refusal: HttpAuthenticationRefusal.UnknownAccount);

        return new AzureStorageCheck(BearerScheme, check);
    }

    // The date first, so a stale request costs no HMAC; then the signature over the page's string.
    private AzureStorageCheck VerifySharedKey(AzureSharedKeyAuthorization sharedKey, HttpAuthenticationRequest request, AzureStorageTarget target)
    {
        var dateRefusal = RequestDateRefusal(request.Fields);
        if (dateRefusal is not null)
        {
            return Refused(sharedKey.Scheme, sharedKey.AccountName, dateRefusal.Value);
        }

        var stringToSign = AzureSharedKeyStringToSign.Build(sharedKey.IsLite, sharedKey.AccountName, request.Method, target, request.Fields);

        return CheckSignature(sharedKey.Scheme, sharedKey.AccountName, stringToSign, sharedKey.Signature, () => null);
    }

    private AzureStorageCheck VerifySharedAccessSignature(AzureSharedAccessSignature signature, AzureStorageScope scope, bool isEncrypted)
    {
        var accountName = scope.AccountName ?? string.Empty;
        if (signature.ReadingRefusal() is { } readingRefusal)
        {
            return Refused(SharedAccessSignatureMethod, accountName, readingRefusal);
        }

        var stringToSign = signature.BuildStringToSign(scope);
        if (stringToSign is null)
        {
            return Refused(SharedAccessSignatureMethod, accountName, HttpAuthenticationRefusal.UnservedSignatureKind);
        }

        return CheckSignature(
            SharedAccessSignatureMethod,
            accountName,
            stringToSign,
            signature.Signature,
            () => signature.ServingRefusal(scope, timeProvider.GetUtcNow(), RequestTimeWindow, isEncrypted));
    }

    // The HMAC-SHA256 of the string under the account's key, in base64, compared in constant time;
    // a match is then judged by servingRefusal.
    private AzureStorageCheck CheckSignature(
        string method, string accountName, string stringToSign, string sentSignature, Func<HttpAuthenticationRefusal?> servingRefusal)
    {
        var account = accounts.FindAzureStorageAccount(accountName);
        var expected = Convert.ToBase64String(HMACSHA256.HashData(account.Key, Encoding.UTF8.GetBytes(stringToSign)));
        var matches = accounts.SecretComparer.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.UTF8.GetBytes(sentSignature));
        if (!matches)
        {
            return Refused(method, accountName, account.AccountName is null ? HttpAuthenticationRefusal.UnknownAccount : HttpAuthenticationRefusal.BadSignature);
        }

        return servingRefusal() is { } refusal
            ? Refused(method, accountName, refusal)
            : new AzureStorageCheck(method, new HttpCredentialCheck(HttpCredentialOutcome.Accepted, account.AccountName, [], accountName));
    }

    // x-ms-date, or Date without it, as an RFC 1123 time within the window.
    private HttpAuthenticationRefusal? RequestDateRefusal(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var date = FieldValue(fields, "x-ms-date") ?? FieldValue(fields, "Date");
        if (!DateTimeOffset.TryParseExact(date, "r", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var time))
        {
            return HttpAuthenticationRefusal.MissingRequestDate;
        }

        return (time - timeProvider.GetUtcNow()).Duration() > RequestTimeWindow ? HttpAuthenticationRefusal.ClockSkew : null;
    }

    private static AzureStorageCheck Refused(string method, string accountName, HttpAuthenticationRefusal refusal) =>
        new(method, new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [], accountName, Refusal: refusal));

    private static AzureStorageCheck Unchecked(HttpAuthenticationRefusal refusal) =>
        new(null, new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [], Refusal: refusal));

    private static string? FieldValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
