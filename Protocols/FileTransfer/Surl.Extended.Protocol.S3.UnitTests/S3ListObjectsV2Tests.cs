using System.Text.RegularExpressions;
using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's ListObjectsV2, whose <c>ListBucketResult</c> bodies are written from the S3 API
/// Reference's "ListObjectsV2" examples (ADR-0108, decision 6).
/// </summary>
[TestClass]
public sealed partial class S3ListObjectsV2Tests
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">";

    // MD5("hello world").
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";

    [TestMethod]
    public async Task ServeAsync_ListObjectsV2_AnswersTheApiReferencesListBucketResult()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/my-image.jpg", "hello world");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        var body = Declaration
            + "<Name>bucket</Name><Prefix></Prefix><MaxKeys>1000</MaxKeys><KeyCount>1</KeyCount><IsTruncated>false</IsTruncated>"
            + $"<Contents><Key>my-image.jpg</Key><LastModified>2026-11-05T12:00:00.000Z</LastModified><ETag>{HelloWorldETag}</ETag><Size>11</Size><StorageClass>STANDARD</StorageClass></Contents>"
            + "</ListBucketResult>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Content-Type: application/xml\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_FiveKeysTwoAPage_PagesThroughWithContinuationTokens()
    {
        var harness = FiveKeys();

        var first = await harness.SendAsync("GET", "/bucket?list-type=2&max-keys=2");
        var second = await harness.SendAsync("GET", $"/bucket?list-type=2&max-keys=2&continuation-token={NextTokenOf(first)}");
        var third = await harness.SendAsync("GET", $"/bucket?list-type=2&max-keys=2&continuation-token={NextTokenOf(second)}");

        CollectionAssert.AreEqual(new[] { "a.txt", "b.txt" }, KeysOf(first));
        StringAssert.Contains(first, "<MaxKeys>2</MaxKeys><KeyCount>2</KeyCount><IsTruncated>true</IsTruncated><NextContinuationToken>Yi50eHQ</NextContinuationToken>");
        CollectionAssert.AreEqual(new[] { "c.txt", "photos/2026/x.jpg" }, KeysOf(second));
        StringAssert.Contains(second, "<IsTruncated>true</IsTruncated><ContinuationToken>Yi50eHQ</ContinuationToken><NextContinuationToken>");
        CollectionAssert.AreEqual(new[] { "photos/y.jpg" }, KeysOf(third));
        StringAssert.Contains(third, "<KeyCount>1</KeyCount><IsTruncated>false</IsTruncated><ContinuationToken>");
        Assert.DoesNotContain("<NextContinuationToken>", third);
    }

    [TestMethod]
    public async Task ServeAsync_DelimiterSlash_RollsKeysUpIntoCommonPrefixes()
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&delimiter=/");

        CollectionAssert.AreEqual(new[] { "a.txt", "b.txt", "c.txt" }, KeysOf(response));
        StringAssert.Contains(response, "<Prefix></Prefix><Delimiter>/</Delimiter><MaxKeys>1000</MaxKeys><KeyCount>4</KeyCount>");
        StringAssert.EndsWith(response, "</Contents><CommonPrefixes><Prefix>photos/</Prefix></CommonPrefixes></ListBucketResult>");
    }

    [TestMethod]
    public async Task ServeAsync_PrefixAndDelimiter_ListsOneLevelOfAFolder()
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&prefix=photos/&delimiter=/");

        CollectionAssert.AreEqual(new[] { "photos/y.jpg" }, KeysOf(response));
        StringAssert.Contains(response, "<CommonPrefixes><Prefix>photos/2026/</Prefix></CommonPrefixes>");
    }

    [TestMethod]
    public async Task ServeAsync_CommonPrefixesPagedOneAtATime_ResumeAfterTheCommonPrefix()
    {
        var harness = FiveKeys();
        harness.WriteFile("bucket/photos/z.jpg", "z");

        var first = await harness.SendAsync("GET", "/bucket?list-type=2&prefix=photos/&delimiter=/&max-keys=1");
        var second = await harness.SendAsync("GET", $"/bucket?list-type=2&prefix=photos/&delimiter=/&max-keys=1&continuation-token={NextTokenOf(first)}");

        StringAssert.Contains(first, "<CommonPrefixes><Prefix>photos/2026/</Prefix></CommonPrefixes>");
        CollectionAssert.AreEqual(new[] { "photos/y.jpg" }, KeysOf(second));
    }

    [TestMethod]
    public async Task ServeAsync_StartAfter_BeginsAfterThatKey()
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&start-after=b.txt");

        CollectionAssert.AreEqual(new[] { "c.txt", "photos/2026/x.jpg", "photos/y.jpg" }, KeysOf(response));
        StringAssert.Contains(response, "<IsTruncated>false</IsTruncated><StartAfter>b.txt</StartAfter><Contents>");
    }

    [TestMethod]
    public async Task ServeAsync_MaxKeysZero_IsAnEmptyUntruncatedPage()
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&max-keys=0");

        StringAssert.EndsWith(response, "<MaxKeys>0</MaxKeys><KeyCount>0</KeyCount><IsTruncated>false</IsTruncated></ListBucketResult>");
    }

    [TestMethod]
    public async Task ServeAsync_MaxKeysPast1000_IsServedAs1000()
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&max-keys=5000&fetch-owner");

        StringAssert.Contains(response, "<MaxKeys>1000</MaxKeys><KeyCount>5</KeyCount>");
    }

    [TestMethod]
    public async Task ServeAsync_EncodingTypeUrlAndFetchOwner_EncodesKeysAndNamesTheOwner()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a b&c.txt", "x");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&encoding-type=url&fetch-owner=true&prefix=a%20");

        StringAssert.Contains(response, "<Prefix>a%20</Prefix>");
        StringAssert.Contains(response, "<EncodingType>url</EncodingType><Contents><Key>a%20b%26c.txt</Key>");
        StringAssert.Contains(response, "<StorageClass>STANDARD</StorageClass><Owner><ID>AKIAIOSFODNN7EXAMPLE</ID><DisplayName>AKIAIOSFODNN7EXAMPLE</DisplayName></Owner></Contents>");
    }

    [TestMethod]
    public async Task ServeAsync_KeyWithXmlCharacters_IsEscaped()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a&<b>.txt", "x");

        var response = await harness.SendAsync("GET", "/bucket/?list-type=2");

        StringAssert.Contains(response, "<Key>a&amp;&lt;b&gt;.txt</Key>");
    }

    [TestMethod]
    public async Task ServeAsync_KeysInUtf8BinaryOrder_AreListedSo()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.CreateFolder("bucket/a");
        harness.WriteFile("bucket/a/x", "x");
        harness.WriteFile("bucket/a.txt", "x");
        harness.WriteFile("bucket/a0", "x");
        harness.WriteFile("bucket/Ａ", "x");
        harness.WriteFile("bucket/\U0001F600", "x");
        harness.CreateFolder("bucket/empty");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        CollectionAssert.AreEqual(new[] { "a.txt", "a/x", "a0", "Ａ", "\U0001F600" }, KeysOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ListingWithListingOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("bucket");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ListingAMissingBucket_Is404NoSuchBucket()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        StringAssert.Contains(response, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    [DataRow("max-keys=-1", "<ArgumentName>max-keys</ArgumentName><ArgumentValue>-1</ArgumentValue>", "Invalid Argument", DisplayName = "a negative max-keys")]
    [DataRow("max-keys=ten", "<ArgumentName>max-keys</ArgumentName><ArgumentValue>ten</ArgumentValue>", "Invalid Argument", DisplayName = "a max-keys that is no number")]
    [DataRow("encoding-type=base64", "<ArgumentName>encoding-type</ArgumentName><ArgumentValue>base64</ArgumentValue>", "Invalid Argument", DisplayName = "an encoding-type other than url")]
    [DataRow("continuation-token=%25%25", "<ArgumentName>continuation-token</ArgumentName><ArgumentValue>%%</ArgumentValue>", "The continuation token provided is incorrect", DisplayName = "a token that is not base64url")]
    [DataRow("continuation-token=_w", "<ArgumentName>continuation-token</ArgumentName><ArgumentValue>_w</ArgumentValue>", "The continuation token provided is incorrect", DisplayName = "a token that is not UTF-8")]
    public async Task ServeAsync_BadQueryValue_Is400InvalidArgument(string parameter, string elements, string message)
    {
        var harness = FiveKeys();

        var response = await harness.SendAsync("GET", $"/bucket?list-type=2&{parameter}");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, $"<Code>InvalidArgument</Code><Message>{message}</Message>{elements}<Resource>/bucket</Resource>");
    }

    [TestMethod]
    public async Task ServeAsync_FileWritenByAnotherProtocol_IsListedWithTheIndexDefaultsAndAPutObjectWithItsOwn()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/ftp.txt", "hello world");
        await harness.SendWithBodyAsync("PUT", "/bucket/s3.txt", "hello world", "Content-Type: text/plain");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        StringAssert.Contains(response, $"<Key>ftp.txt</Key><LastModified>2026-11-05T12:00:00.000Z</LastModified><ETag>{HelloWorldETag}</ETag><Size>11</Size>");
        StringAssert.Contains(response, $"<Key>s3.txt</Key><LastModified>2026-11-05T12:00:00.000Z</LastModified><ETag>{HelloWorldETag}</ETag><Size>11</Size>");
    }

    [TestMethod]
    public async Task ServeAsync_ListingAShareBucket_HashesItsFiles()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        S3ServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendAsync("GET", "/shared?list-type=2");

        StringAssert.Contains(response, $"<Key>a.txt</Key><LastModified>2026-11-05T12:00:00.000Z</LastModified><ETag>{HelloWorldETag}</ETag>");
    }

    [TestMethod]
    public async Task ServeAsync_FileWhoseNameTheStoreRefuses_IsLeftOut()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a.txt", "x");
        harness.WriteFile("bucket/b:c", "x");

        var response = await harness.SendAsync("GET", "/bucket?list-type=2");

        CollectionAssert.AreEqual(new[] { "a.txt" }, KeysOf(response));
    }

    [TestMethod]
    public void KeyOrder_NullKeys_SortAsEmpty()
    {
        Assert.AreEqual(0, S3KeyOrder.Instance.Compare(null, string.Empty));
        Assert.IsLessThan(0, S3KeyOrder.Instance.Compare(null, "a"));
        Assert.IsGreaterThan(0, S3KeyOrder.Instance.Compare("a", null));
    }

    // Five keys: three at the top, two in folders, and an empty folder that is no key.
    private static S3ServerHarness FiveKeys()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a.txt", "a");
        harness.WriteFile("bucket/b.txt", "b");
        harness.WriteFile("bucket/c.txt", "c");
        harness.CreateFolder("bucket/photos");
        harness.CreateFolder("bucket/photos/2026");
        harness.WriteFile("bucket/photos/2026/x.jpg", "x");
        harness.WriteFile("bucket/photos/y.jpg", "y");
        harness.CreateFolder("bucket/zz-empty");

        return harness;
    }

    private static string[] KeysOf(string response) =>
        [.. KeyElement().Matches(response).Select(match => match.Groups[1].Value)];

    private static string NextTokenOf(string response) => NextTokenElement().Match(response).Groups[1].Value;

    [GeneratedRegex("<Key>([^<]*)</Key>")]
    private static partial Regex KeyElement();

    [GeneratedRegex("<NextContinuationToken>([^<]*)</NextContinuationToken>")]
    private static partial Regex NextTokenElement();
}
