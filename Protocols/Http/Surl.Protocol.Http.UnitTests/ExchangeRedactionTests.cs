using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The http and https server hands the engine a redactor, so the exchange log shows
/// <c>Authorization: Basic ****</c> and never the credentials (ADR-0182, decision 7, BL-1422).
/// What is sent to the client is unchanged.
/// </summary>
[TestClass]
public sealed class ExchangeRedactionTests
{
    private static readonly ExchangeRedactionContext RedactionContext = new(1, "https");

    private static string Redacted(IReceivedBytesRedactor redactor, byte[] received)
    {
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(received, output);
        redactor.Flush(output);

        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    private static HttpProtocolServer NewServer() => Server();

    [TestMethod]
    public void CreateRedactor_BasicAuthorizedGet_LogsTheSchemeWordAndStars()
    {
        var redactor = NewServer().CreateRedactor(RedactionContext);
        var request = RecordedFixture.ReadRequestBytes("basic-401");

        var logged = Redacted(redactor, request);

        Assert.DoesNotContain("dGVzdGVyOnNlY3JldA==", logged);
        Assert.Contains("Authorization: Basic ****", logged);
    }

    [TestMethod]
    public void CreateRedactor_BearerRequest_LogsTheSchemeWordAndStars()
    {
        var logged = Redacted(NewServer().CreateRedactor(RedactionContext), Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\nHost: h\r\nAuthorization: Bearer tok123\r\n\r\n"));

        Assert.DoesNotContain("tok123", logged);
        Assert.Contains("Authorization: Bearer ****", logged);
    }

    [TestMethod]
    public void CreateRedactor_TwoRequestsOnOnePersistentConnection_RedactsBoth()
    {
        var logged = Redacted(NewServer().CreateRedactor(RedactionContext), Encoding.ASCII.GetBytes(
            "GET /a HTTP/1.1\r\nHost: h\r\nAuthorization: Basic QUFBQQ==\r\n\r\n"
            + "GET /b HTTP/1.1\r\nHost: h\r\nAuthorization: Bearer second-secret\r\n\r\n"));

        Assert.DoesNotContain("QUFBQQ==", logged);
        Assert.DoesNotContain("second-secret", logged);
        Assert.Contains("GET /b HTTP/1.1", logged);
    }

    [TestMethod]
    public void CreateRedactor_EachExchange_GetsItsOwnRedactor()
    {
        var server = NewServer();

        Assert.AreNotSame(server.CreateRedactor(RedactionContext), server.CreateRedactor(RedactionContext));
    }
}
