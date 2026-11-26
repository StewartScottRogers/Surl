using System.Buffers;
using System.Text;

namespace Surl.HttpMessage;

[TestClass]
public sealed class HttpRequestHeadBytesRedactorTests
{
    private const string Sample = "GET /a HTTP/1.1\r\nHost: h\r\nAuthorization: Basic YWxpY2U6czNjcmV0\r\nAccept: */*\r\n\r\n";
    private const string SampleRedacted = "GET /a HTTP/1.1\r\nHost: h\r\nAuthorization: Basic ****\r\nAccept: */*\r\n\r\n";

    [TestMethod]
    [DataRow("Authorization: Basic YWxpY2U6czNjcmV0", "Authorization: Basic ****")]
    [DataRow("authorization: Bearer abc", "authorization: Bearer ****")]
    [DataRow("PROXY-AUTHORIZATION: Basic YWxpY2U6czNjcmV0", "PROXY-AUTHORIZATION: Basic ****")]
    [DataRow("Proxy-Authorization:Bearer   tok", "Proxy-Authorization:Bearer ****")]
    [DataRow("Authorization:   Bearer tok  ", "Authorization:   Bearer ****")]
    [DataRow("Authorization : Bearer tok", "Authorization : Bearer ****")]
    [DataRow("Authorization: justatoken", "Authorization: ****")]
    [DataRow("Authorization: ", "Authorization: ")]
    [DataRow("X-Other: Bearer tok", "X-Other: Bearer tok")]
    [DataRow("No colon here", "No colon here")]
    public void Redact_MasksCredentialsAndKeepsTheSchemeWord(string field, string expected)
    {
        Assert.AreEqual($"GET / HTTP/1.1\r\n{expected}\r\n\r\n", Redact($"GET / HTTP/1.1\r\n{field}\r\n\r\n"));
    }

    [TestMethod]
    public void Redact_MasksAFoldedContinuationOfASecretField()
    {
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nAuthorization: Bearer ****\r\n  ****\r\nX: a\r\n b\r\n\r\n",
            Redact("GET / HTTP/1.1\r\nAuthorization: Bearer one\r\n  two\r\nX: a\r\n b\r\n\r\n"));
    }

    [TestMethod]
    public void Redact_MasksAnAdditionalSecretFieldWhole()
    {
        var redactor = new HttpRequestHeadBytesRedactor(["X-Api-Key"]);

        Assert.AreEqual("GET / HTTP/1.1\r\nx-api-key: ****\r\n\r\n", Redact("GET / HTTP/1.1\r\nx-api-key: k e y\r\n\r\n", redactor));
    }

    [TestMethod]
    public void Redact_SkipsBlankLinesBeforeTheRequestLine()
    {
        Assert.AreEqual("\r\n" + SampleRedacted, Redact("\r\n" + Sample));
    }

    [TestMethod]
    public void Redact_Http10Head_IsRedactedAndHasNoBody()
    {
        Assert.AreEqual(
            "GET / HTTP/1.0\r\nAuthorization: Bearer ****\r\n\r\n" + SampleRedacted,
            Redact("GET / HTTP/1.0\r\nAuthorization: Bearer a\r\n\r\n" + Sample));
    }

    [TestMethod]
    public void Redact_ContentLengthBody_PassesThroughAndTheNextHeadIsRedacted()
    {
        const string body = "Authorization: x\r\n\r\nPOST";
        string first = $"POST /u HTTP/1.1\r\nContent-Length: {body.Length}\r\n\r\n";

        Assert.AreEqual(first + body + SampleRedacted, Redact(first + body + Sample));
    }

    [TestMethod]
    public void Redact_ChunkedBody_PassesThroughAndTheNextHeadIsRedacted()
    {
        const string body = "5;ext=1\r\nAuth\r\n1A\r\norization: x\r\n\r\nAuthoriz\r\n\r\n0\r\nTrailer: v\r\n\r\n";
        const string first = "POST /u HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n";

        Assert.AreEqual(first + body + SampleRedacted, Redact(first + body + Sample));
    }

    [TestMethod]
    public void Redact_ChunkedBodyWithLowerCaseHexAndNoTrailers_PassesThrough()
    {
        const string body = "a\r\n0123456789\r\n0\r\n\r\n";
        const string first = "POST /u HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n";

        Assert.AreEqual(first + body + SampleRedacted, Redact(first + body + Sample));
    }

    [TestMethod]
    public void Redact_UnreadableFraming_ReadsWhatFollowsAsAHead()
    {
        const string first = "POST / HTTP/1.1\r\nContent-Length: abc\r\n\r\n";

        Assert.AreEqual(first + SampleRedacted, Redact(first + Sample));
    }

    [TestMethod]
    public void Redact_ABodyInManyReads_PassesThrough()
    {
        string all = "POST / HTTP/1.1\r\nContent-Length: 6\r\n\r\nabcdef" + Sample;
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (char c in all)
        {
            redactor.Redact([(byte)c], output);
        }

        redactor.Flush(output);

        Assert.AreEqual("POST / HTTP/1.1\r\nContent-Length: 6\r\n\r\nabcdef" + SampleRedacted, Latin1(output));
    }

    [TestMethod]
    public void Redact_TheSampleHeadSplitAtEveryByte_IsRedactedTheSame()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(Sample);
        for (int split = 0; split <= bytes.Length; split++)
        {
            var redactor = new HttpRequestHeadBytesRedactor();
            var output = new ArrayBufferWriter<byte>();
            redactor.Redact(bytes.AsSpan(0, split), output);
            redactor.Redact(bytes.AsSpan(split), output);
            redactor.Flush(output);

            Assert.AreEqual(SampleRedacted, Latin1(output), $"split at {split}");
        }
    }

    [TestMethod]
    public void Flush_WritesAnUnfinishedLineRedacted()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact("GET / HTTP/1.1\r\nAuthorization: Basic abc"u8, output);
        redactor.ObserveSent("401"u8);
        redactor.Flush(output);

        Assert.AreEqual("GET / HTTP/1.1\r\nAuthorization: Basic ****", Latin1(output));
    }

    [TestMethod]
    public void IsAtRequestStart_IsTrueOnlyBetweenRequests()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        Assert.IsTrue(redactor.IsAtRequestStart);

        redactor.Redact("POST / HTTP/1.1\r\nContent-Length: 2\r\n"u8, output);
        Assert.IsFalse(redactor.IsAtRequestStart);

        redactor.Redact("\r\na"u8, output);
        Assert.IsFalse(redactor.IsAtRequestStart);

        redactor.Redact("b"u8, output);
        Assert.IsTrue(redactor.IsAtRequestStart);

        redactor.Redact("GE"u8, output);
        Assert.IsFalse(redactor.IsAtRequestStart);
    }

    [TestMethod]
    public void IsAtRequestStart_RightAfterAHeadDeclaringContentLengthZero_IsTrue()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact("POST / HTTP/1.1\r\nContent-Length: 0\r\n\r\n"u8, output);

        Assert.IsTrue(redactor.IsAtRequestStart);
    }

    [TestMethod]
    public void Redact_NextHeadAfterAContentLengthZeroHead_MasksItsAuthorization()
    {
        const string first = "POST /u HTTP/1.1\r\nContent-Length: 0\r\n\r\n";
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Encoding.Latin1.GetBytes(first), output);
        redactor.Redact(Encoding.Latin1.GetBytes(Sample), output);
        redactor.Flush(output);

        Assert.AreEqual(first + SampleRedacted, Latin1(output));
    }

    [TestMethod]
    public void Redact_AndFlush_RefuseANullOutput()
    {
        var redactor = new HttpRequestHeadBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    private static string Redact(string received, HttpRequestHeadBytesRedactor? redactor = null)
    {
        redactor ??= new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Encoding.Latin1.GetBytes(received), output);
        redactor.Flush(output);
        return Latin1(output);
    }

    private static string Latin1(ArrayBufferWriter<byte> output) => Encoding.Latin1.GetString(output.WrittenSpan);
}
