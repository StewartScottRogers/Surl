namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Which region a request's Signature Version 4 credential scope names ("Elements of an AWS API
/// request signature": <c>&lt;key&gt;/&lt;date&gt;/&lt;region&gt;/&lt;service&gt;/aws4_request</c>), read from the
/// <c>Authorization</c> field or a presigned URL's <c>X-Amz-Credential</c> (BL-1698).
/// </summary>
[TestClass]
public sealed class S3CredentialScopeTests
{
    private const string Presigned = "/hello.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=";

    private static KeyValuePair<string, string>[] Fields(params string?[] authorizations) =>
    [
        new("Host", "photos.s3.us-east-1.amazonaws.com"),
        .. authorizations.OfType<string>().Select(authorization => new KeyValuePair<string, string>("Authorization", authorization)),
    ];

    [TestMethod]
    [DataRow(
        "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/eu-west-1/s3/aws4_request,SignedHeaders=host,Signature=00",
        "eu-west-1",
        DisplayName = "as upstream curl writes it")]
    [DataRow(
        "aws4-hmac-sha256 Credential=key/20130524/eu-west-1/s3/aws4_request",
        "eu-west-1",
        DisplayName = "the scheme in another case")]
    [DataRow(
        "AWS4-HMAC-SHA256 SignedHeaders=host, Signature=00, Credential=key/20130524/eu-west-1/s3/aws4_request",
        "eu-west-1",
        DisplayName = "the credential last, spaces after the commas")]
    [DataRow(
        "AWS4-HMAC-SHA256 Credential=key/20130524/eu-west-1/s3/aws4_request,Credential=key/20130524/us-east-1/s3/aws4_request",
        "eu-west-1",
        DisplayName = "the first of two credentials")]
    [DataRow(
        "AWS4-HMAC-SHA256 Credential=a/b/20130524/eu-west-1/s3/aws4_request",
        "eu-west-1",
        DisplayName = "an access key holding a slash")]
    [DataRow(
        "AWS4-HMAC-SHA256 Credential=key/20130524/EU-West-1/s3/aws4_request",
        "EU-West-1",
        DisplayName = "the region's case is kept")]
    [DataRow(
        "AWS4-HMAC-SHA256 Credential=key/20130524//s3/aws4_request",
        "",
        DisplayName = "an empty region part")]
    public void RegionOf_AuthorizationCredential_IsTheScopesRegion(string authorization, string region)
    {
        Assert.AreEqual(region, S3CredentialScope.RegionOf("/hello.txt", Fields(authorization)));
    }

    [TestMethod]
    public void RegionOf_AuthorizationCredential_IsReadFromTheFirstAuthorizationFieldNamedInAnyCase()
    {
        KeyValuePair<string, string>[] fields =
        [
            new("authorization", "AWS4-HMAC-SHA256 Credential=key/20130524/eu-west-1/s3/aws4_request"),
            new("Authorization", "AWS4-HMAC-SHA256 Credential=key/20130524/us-east-1/s3/aws4_request"),
        ];

        Assert.AreEqual("eu-west-1", S3CredentialScope.RegionOf("/hello.txt", fields));
    }

    [TestMethod]
    [DataRow(Presigned + "key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request", "eu-west-1", DisplayName = "percent-encoded")]
    [DataRow(Presigned + "key/20130524/eu-west-1/s3/aws4_request", "eu-west-1", DisplayName = "not encoded")]
    [DataRow(Presigned + "a%2Fb%2F20130524%2Feu-west-1%2Fs3%2Faws4_request", "eu-west-1", DisplayName = "an access key holding a slash")]
    [DataRow(
        Presigned + "key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request&X-Amz-Credential=key%2F20130524%2Fus-east-1%2Fs3%2Faws4_request",
        "eu-west-1",
        DisplayName = "the first of two credentials")]
    [DataRow(
        "/a?X-Amz-Credential=key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request&X-Amz-Algorithm=AWS4-HMAC-SHA256",
        "eu-west-1",
        DisplayName = "the algorithm after the credential")]
    public void RegionOf_PresignedCredential_IsReadDecodedOnce(string target, string region)
    {
        Assert.AreEqual(region, S3CredentialScope.RegionOf(target, Fields()));
    }

    [TestMethod]
    [DataRow("/hello.txt", null, DisplayName = "no credential at all")]
    [DataRow("/hello.txt", "Basic dXNlcjpwYXNz", DisplayName = "another scheme")]
    [DataRow("/hello.txt", "AWS4-HMAC-SHA256X Credential=key/20130524/eu-west-1/s3/aws4_request", DisplayName = "a longer scheme")]
    [DataRow("/hello.txt", "AWS4-HMAC-SHA256", DisplayName = "the scheme alone")]
    [DataRow("/hello.txt", "", DisplayName = "an empty Authorization")]
    [DataRow("/hello.txt", "AWS4-HMAC-SHA256 SignedHeaders=host,Signature=00", DisplayName = "no Credential parameter")]
    [DataRow("/hello.txt", "AWS4-HMAC-SHA256 Credential=20130524/eu-west-1/s3/aws4_request", DisplayName = "fewer than five parts")]
    [DataRow("/hello.txt", "AWS4-HMAC-SHA256 Credential=", DisplayName = "an empty credential")]
    [DataRow("/hello.txt?X-Amz-Credential=key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request", null, DisplayName = "a query credential with no algorithm")]
    [DataRow("/hello.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256", null, DisplayName = "an algorithm with no credential")]
    [DataRow(
        "/hello.txt?X-Amz-Algorithm=aws4-hmac-sha256&X-Amz-Credential=key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request",
        null,
        DisplayName = "the algorithm in another case")]
    [DataRow(
        "/hello.txt?X-Amz-Algorithm=AWS4-ECDSA-P256-SHA256&X-Amz-Credential=key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request",
        null,
        DisplayName = "another algorithm")]
    [DataRow(
        "/hello.txt?X-Amz-Algorithm=other",
        "AWS4-HMAC-SHA256 Credential=key/20130524/eu-west-1/s3/aws4_request",
        DisplayName = "another algorithm beside an Authorization")]
    [DataRow(
        Presigned + "key%2F20130524%2Feu-west-1%2Fs3%2Faws4_request",
        "AWS4-HMAC-SHA256 Credential=key/20130524/eu-west-1/s3/aws4_request",
        DisplayName = "a query signature and an Authorization together")]
    [DataRow(Presigned + "20130524%2Feu-west-1%2Fs3%2Faws4_request", null, DisplayName = "a query credential of fewer than five parts")]
    [DataRow(Presigned + "key%252F20130524%252Feu-west-1%252Fs3%252Faws4_request", null, DisplayName = "a query credential encoded twice")]
    public void RegionOf_NoCredentialToJudge_IsNull(string target, string? authorization)
    {
        Assert.IsNull(S3CredentialScope.RegionOf(target, Fields(authorization)));
    }
}
