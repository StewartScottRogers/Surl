using System.Buffers;
using System.Text;

namespace Surl.HttpServing;

/// <summary>
/// The redactor the HTTP-carried extended servers hand the engine masks <c>Authorization</c>,
/// <c>Proxy-Authorization</c> and API-key fields across chunk boundaries and leaves the rest alone
/// (ADR-0182, decision 7; BL-1465).
/// </summary>
[TestClass]
public sealed class HttpExchangeRedactionTests
{
    private const string Request =
        "PUT /bucket/key HTTP/1.1\r\nHost: h\r\nAuthorization: AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20261120/us-east-1/s3/aws4_request, Signature=deadbeef\r\n"
        + "X-Amz-Security-Token: sessiontoken123\r\nX-Api-Key: key-456\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public void CreateRedactor_RequestSplitIntoTinyChunks_WithholdsEverySecretAndKeepsTheRest()
    {
        var redactor = HttpExchangeRedaction.CreateRedactor();
        var output = new ArrayBufferWriter<byte>();
        var bytes = Encoding.ASCII.GetBytes(Request);
        for (var index = 0; index < bytes.Length; index += 3)
        {
            redactor.Redact(bytes.AsSpan(index, Math.Min(3, bytes.Length - index)), output);
        }

        redactor.Flush(output);
        var logged = Encoding.ASCII.GetString(output.WrittenSpan);

        Assert.DoesNotContain("deadbeef", logged);
        Assert.DoesNotContain("sessiontoken123", logged);
        Assert.DoesNotContain("key-456", logged);
        Assert.Contains("Authorization: AWS4-HMAC-SHA256 ****", logged);
        Assert.Contains("PUT /bucket/key HTTP/1.1\r\nHost: h\r\n", logged);
        Assert.EndsWith("Content-Length: 5\r\n\r\nhello", logged);
    }

    [TestMethod]
    public void CreateRedactor_ServerAnswer_IsNotChangedByObservingIt()
    {
        var redactor = HttpExchangeRedaction.CreateRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.ObserveSent("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8);
        redactor.Redact("GET / HTTP/1.1\r\nHost: h\r\n\r\n"u8, output);

        Assert.AreEqual("GET / HTTP/1.1\r\nHost: h\r\n\r\n", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void CreateRedactor_EachCall_ReturnsItsOwnRedactor()
    {
        Assert.AreNotSame(HttpExchangeRedaction.CreateRedactor(), HttpExchangeRedaction.CreateRedactor());
    }

    [TestMethod]
    public void ApiKeyFieldNames_AreTheFieldsMaskedBesidesAuthorization()
    {
        CollectionAssert.Contains(HttpExchangeRedaction.ApiKeyFieldNames.ToList(), "X-Api-Key");
    }
}
