using System.Security.Cryptography;
using System.Text;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Get Blob's ranges, conditional headers and <c>Content-MD5</c>, written from "Get Blob",
/// "Specifying the range header for Blob service operations" and "Specifying conditional headers
/// for Blob service operations" (ADR-0109, decision 1).
/// </summary>
[TestClass]
public sealed class AzureBlobAnswerTests
{
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";

    [TestMethod]
    public async Task AnswerAsync_RangeField_Is206WithContentRange()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=0-4");

        StringAssert.StartsWith(response, "HTTP/1.1 206 Partial Content\r\n");
        StringAssert.Contains(response, "Content-Length: 5\r\nContent-Type: text/plain\r\nContent-Range: bytes 0-4/11\r\nContent-MD5: XrY7u+Ae7tCTyyK7j1rNww==\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello");
    }

    [TestMethod]
    public async Task AnswerAsync_XMsRangeAndRange_XMsRangeWins()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=0-4", "x-ms-range: bytes=6-");

        StringAssert.Contains(response, "Content-Range: bytes 6-10/11\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nworld");
    }

    [TestMethod]
    public async Task AnswerAsync_UnsatisfiableRange_Is416InvalidRange()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=20-30");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "416 Range Not Satisfiable", "InvalidRange", "The range specified is invalid for the current size of the resource."),
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_SeveralRanges_ServesTheWholeBlob()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "Range: bytes=0-1,4-5");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task AnswerAsync_RangeGetContentMd5_SendsTheRangesMd5()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "x-ms-range: bytes=0-4", "x-ms-range-get-content-md5: true");

        StringAssert.Contains(response, $"Content-MD5: {Convert.ToBase64String(MD5.HashData(Encoding.ASCII.GetBytes("hello")))}\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_RangeGetContentMd5WithoutARange_SendsTheBlobsMd5()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "x-ms-range-get-content-md5: true");

        StringAssert.Contains(response, "Content-MD5: XrY7u+Ae7tCTyyK7j1rNww==\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_RangeGetContentMd5OverFourMebibytes_Is400OutOfRangeInput()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/big.bin", new string('x', (4 * 1024 * 1024) + 1));

        var response = await harness.SendWithBearerAsync("GET", "/photos/big.bin", "x-ms-range: bytes=0-4194304", "x-ms-range-get-content-md5: true");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "OutOfRangeInput", "One of the request inputs is out of range."),
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_IfMatchThatFails_Is412ConditionNotMet()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "If-Match: \"other\"");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "412 Precondition Failed", "ConditionNotMet", "The condition specified using HTTP conditional header(s) is not met."),
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_IfNoneMatchThatMatches_Is304WithNoBody()
    {
        var harness = HelloWorldHarness();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", $"If-None-Match: {HelloWorldETag}");

        StringAssert.StartsWith(response, $"HTTP/1.1 304 Not Modified\r\n{AzureServerHarness.CommonFields(response)}Last-Modified: {AzureServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\n");
        StringAssert.EndsWith(response, "\r\n\r\n");
    }

    [TestMethod]
    [DataRow("\"5eb63bbbe01eeed093cb22bb8f5acdc3\"", "XrY7u+Ae7tCTyyK7j1rNww==", DisplayName = "the quoted MD5")]
    [DataRow("\"stored\"", null, DisplayName = "another tag")]
    [DataRow("5eb63bbbe01eeed093cb22bb8f5acdc3!!", null, DisplayName = "34 characters without quotes")]
    [DataRow("\"5eb63bbbe01eeed093cb22bb8f5acdc3", null, DisplayName = "no closing quote")]
    [DataRow("\"5eb63bbbe01eeed093cb22bb8f5acdcz\"", null, DisplayName = "a digit that is not hexadecimal")]
    public void StoredMd5_ETag_IsTheBase64DigestOnlyForAQuotedMd5(string eTag, string? expected)
    {
        var contentMd5 = AzureBlobAnswer.StoredMd5(eTag);

        Assert.AreEqual(expected, contentMd5);
    }

    private static AzureServerHarness HelloWorldHarness()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        return harness;
    }
}
