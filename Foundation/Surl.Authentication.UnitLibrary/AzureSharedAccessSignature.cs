using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// A shared access signature in a request's query, read and judged as "Create a service SAS" and
/// "Create an account SAS" give it for signed versions 2020-12-06 and later (ADR-0109 decision
/// 3.3). The signature is checked first; then the times, the address, the protocol and last the
/// permission, so only a holder of the account key learns why a signed token falls short.
/// </summary>
internal sealed class AzureSharedAccessSignature
{
    /// <summary>
    /// The earliest signed version whose string to sign is the one built here.
    /// </summary>
    public const string FirstServedVersion = "2020-12-06";

    private static readonly string[] TimeFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
    ];

    private readonly AzureStorageTarget target;

    private AzureSharedAccessSignature(AzureStorageTarget target, string signature)
    {
        this.target = target;
        Signature = signature;
    }

    /// <summary>
    /// The signature as sent, base64 after percent-decoding.
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// The shared access signature in <paramref name="target"/>'s query, or <see langword="null"/>
    /// when it carries no <c>sig</c>.
    /// </summary>
    /// <param name="target">The request target.</param>
    /// <returns>The signature's fields, or <see langword="null"/>.</returns>
    public static AzureSharedAccessSignature? Find(AzureStorageTarget target) =>
        target.Value("sig") is { } signature ? new AzureSharedAccessSignature(target, signature) : null;

    /// <summary>
    /// Why the signature cannot be checked before its key is used: a stored access policy or a
    /// user delegation key, a version before <see cref="FirstServedVersion"/>, or fields that do
    /// not read. <see langword="null"/> when it can be.
    /// </summary>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    public HttpAuthenticationRefusal? ReadingRefusal()
    {
        if (Field("si") is not null || Field("skoid") is not null)
        {
            return HttpAuthenticationRefusal.UnservedSignatureKind;
        }

        var version = Field("sv");
        if (version is null || !IsDate(version) || Field("sp") is null || !IsTime(Field("se")) || !IsAbsentOrTime(Field("st")))
        {
            return HttpAuthenticationRefusal.UnreadableSignatureFields;
        }

        return string.CompareOrdinal(version, FirstServedVersion) < 0 ? HttpAuthenticationRefusal.UnservedSignatureKind : null;
    }

    /// <summary>
    /// The string to sign for <paramref name="scope"/>'s request: an account signature's when the
    /// query carries <c>ss</c> or <c>srt</c>, a service signature's otherwise, or
    /// <see langword="null"/> when the signed resource is not one the request addresses.
    /// </summary>
    /// <param name="scope">What the server said of the request.</param>
    /// <returns>The string to sign, or <see langword="null"/>.</returns>
    public string? BuildStringToSign(AzureStorageScope scope) =>
        IsAccountSignature
            ? AccountStringToSign(scope.AccountName ?? string.Empty)
            : ServiceStringToSign(scope);

    /// <summary>
    /// Why a verified signature does not serve the request now, over a connection
    /// <paramref name="isEncrypted"/> or not, or <see langword="null"/> when it does.
    /// </summary>
    /// <param name="scope">What the server said of the request.</param>
    /// <param name="now">The injected clock's time.</param>
    /// <param name="window">The skew allowed either way of the signed times.</param>
    /// <param name="isEncrypted">Whether the connection is under TLS.</param>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    public HttpAuthenticationRefusal? ServingRefusal(AzureStorageScope scope, DateTimeOffset now, TimeSpan window, bool isEncrypted)
    {
        if (now - window > ParseTime(Field("se")!))
        {
            return HttpAuthenticationRefusal.SignatureExpired;
        }

        if (Field("st") is { } start && now + window < ParseTime(start))
        {
            return HttpAuthenticationRefusal.SignatureNotYetValid;
        }

        if (!AzureSignedAddressRange.Admits(Field("sip"), scope.PeerAddress))
        {
            return HttpAuthenticationRefusal.AddressNotAllowed;
        }

        if (!AdmitsProtocol(Field("spr"), isEncrypted))
        {
            return HttpAuthenticationRefusal.ProtocolNotAllowed;
        }

        return Grants(scope) ? null : HttpAuthenticationRefusal.PermissionNotGranted;
    }

    private bool IsAccountSignature => Field("ss") is not null || Field("srt") is not null;

    private string? Field(string name) => target.Value(name);

    private string Signed(string name) => Field(name) ?? string.Empty;

    // "Create an account SAS", version 2020-12-06 and later: the account, sp, ss, srt, st, se,
    // sip, spr, sv and ses, each line ending "\n".
    private string AccountStringToSign(string accountName) =>
        string.Join('\n', accountName, Signed("sp"), Signed("ss"), Signed("srt"), Signed("st"), Signed("se"), Signed("sip"), Signed("spr"), Signed("sv"), Signed("ses"), string.Empty);

    // "Create a service SAS" for the Blob service, version 2020-12-06 and later; the Data Lake
    // endpoint signs the same "/blob/" resource.
    private string? ServiceStringToSign(AzureStorageScope scope)
    {
        var resource = AzureSignedResource.Canonicalize(scope.AccountName ?? string.Empty, scope.ResourcePath, Field("sr"), Field("sdd"));

        return resource is null
            ? null
            : string.Join(
                '\n',
                Signed("sp"),
                Signed("st"),
                Signed("se"),
                resource,
                Signed("si"),
                Signed("sip"),
                Signed("spr"),
                Signed("sv"),
                Signed("sr"),
                Field("snapshot") ?? Signed("versionid"),
                Signed("ses"),
                Signed("rscc"),
                Signed("rscd"),
                Signed("rsce"),
                Signed("rscl"),
                Signed("rsct"));
    }

    // A permission letter the operation takes; an account signature must also name the Blob
    // service (which the Data Lake endpoint is part of) and the level the request addresses.
    private bool Grants(AzureStorageScope scope)
    {
        var grantsOperation = scope.GrantingPermissions.Any(Signed("sp").Contains);

        return IsAccountSignature
            ? grantsOperation && Signed("ss").Contains('b', StringComparison.Ordinal) && Signed("srt").Contains(ResourceTypeOf(scope.ResourcePath), StringComparison.Ordinal)
            : grantsOperation;
    }

    // s for the account, c for a container, o for a blob or path.
    private static char ResourceTypeOf(string resourcePath) =>
        resourcePath.Trim('/') switch
        {
            "" => 's',
            var path when !path.Contains('/', StringComparison.Ordinal) => 'c',
            _ => 'o',
        };

    private static bool AdmitsProtocol(string? signedProtocol, bool isEncrypted) =>
        signedProtocol switch
        {
            null or "https,http" or "http,https" => true,
            "https" => isEncrypted,
            _ => false,
        };

    private static bool IsDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsAbsentOrTime(string? text) => text is null || IsTime(text);

    private static bool IsTime(string? text) =>
        DateTimeOffset.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.ParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
