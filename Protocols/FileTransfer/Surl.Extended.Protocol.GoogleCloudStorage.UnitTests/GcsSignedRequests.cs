using System.Security.Cryptography;
using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>GOOG4-HMAC-SHA256</c> requests written from Cloud Storage's "V4 signing process" and
/// "Canonical requests" pages (ADR-0110 decision 3.3), with the made-up HMAC key
/// <c>Surl.Authentication</c>'s <c>GoogleV4HmacAuthenticationMethodTests</c> uses (access ID
/// <see cref="AccessId"/>, secret <c>surl-test-secret-not-a-real-hmac-key</c>).
/// </summary>
internal static class GcsSignedRequests
{
    public const string AccessId = "SURL-TEST-ACCESS-ID";

    /// <summary>
    /// BL-430's hand-worked GET's signature (<c>GoogleV4HmacAuthenticationMethodTests.WorkedSignature</c>);
    /// its canonical request hashes to <c>926f3db0…f2bd1761</c>, every step in that test's comments.
    /// </summary>
    public const string WorkedGetSignature = "05f28f31a518575b2719182d31d951002697cb0023c10e9a77ea66ca7fb79773";

    public const string WorkedGetAuthorization =
        $"GOOG4-HMAC-SHA256 Credential={AccessId}/20191201/us-central1/storage/goog4_request, SignedHeaders=host;x-goog-content-sha256;x-goog-date, Signature={WorkedGetSignature}";

    /// <summary>
    /// BL-430's hand-worked GET, as its bytes arrive.
    /// </summary>
    public const string WorkedGet =
        "GET /example-bucket/cat-pics/tabby.jpeg HTTP/1.1\r\n"
        + "Host: storage.googleapis.com\r\n"
        + $"Authorization: {WorkedGetAuthorization}\r\n"
        + "x-goog-content-sha256: UNSIGNED-PAYLOAD\r\n"
        + "x-goog-date: 20191201T190859Z\r\n"
        + "Connection: close\r\n\r\n";

    /// <summary>
    /// The SHA-256 of <c>hello world</c>, the signed PUT's body.
    /// </summary>
    public const string HelloWorldSha256 = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

    // The signed PUT of "hello world" to /example-bucket/hello.txt at the harness's clock, worked by
    // hand in PowerShell (SHA256.HashData, HMACSHA256.HashData), as BL-430's GET was:
    //
    // 1. Canonical request:
    //      PUT
    //      /example-bucket/hello.txt
    //
    //      host:storage.googleapis.com
    //      x-goog-content-sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9
    //      x-goog-date:20261105T120000Z
    //
    //      host;x-goog-content-sha256;x-goog-date
    //      b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9
    //    Its SHA-256: 89a7c218096df33c7d0fad020e11af959f1831b051f16e24860e7eda5a1826d2
    //
    // 2. String to sign: GOOG4-HMAC-SHA256, 20261105T120000Z, 20261105/auto/storage/goog4_request, the hash above.
    //
    // 3. Signing key chained from "GOOG4" + secret:
    //      date     = df2c026f370e9d2ea4cdd2740cbbde4de3d0449bcd0675a068052d1efae656c5
    //      location = ce1dff6dc94998eaac7d86fef20e940aedcb9917443fe73f1f8d7c334e488221 ("auto")
    //      service  = 7ada6ebc188eb2b44ea39e3aa7707678e7b2f3c4767285cd49a1888cf6f67e3a
    //      signing  = eb807a2f768ec354d8be01050515a2d9fe65403620c719c06da69b37a288c1cd
    //
    // 4. Signature:
    public const string PutSignature = "1ef686217a189b78c5ffa3effaec3d93a2277fb7c3eecf67e35419e85285b258";

    public const string PutAuthorization =
        $"GOOG4-HMAC-SHA256 Credential={AccessId}/20261105/auto/storage/goog4_request, SignedHeaders=host;x-goog-content-sha256;x-goog-date, Signature={PutSignature}";

    /// <summary>
    /// A request to <c>/example-bucket/hello.txt</c> with <see cref="PutAuthorization"/> and the
    /// signed PUT's fields, carrying <paramref name="body"/> - the signed one, <c>hello world</c>,
    /// or another, which no longer hashes to the signed <c>x-goog-content-sha256</c>.
    /// </summary>
    public static string Request(string method, string body, string path = "/example-bucket/hello.txt", string contentSha256 = HelloWorldSha256) =>
        $"{method} {path} HTTP/1.1\r\n"
        + "Host: storage.googleapis.com\r\n"
        + $"Authorization: {PutAuthorization}\r\n"
        + $"x-goog-content-sha256: {contentSha256}\r\n"
        + "x-goog-date: 20261105T120000Z\r\n"
        + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n"
        + $"Connection: close\r\n\r\n{body}";

    public static string Sha256Hex(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
