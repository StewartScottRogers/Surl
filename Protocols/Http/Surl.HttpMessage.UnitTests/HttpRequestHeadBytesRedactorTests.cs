using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

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
    [DataRow("GET / HTTP/1.1\r\nHost: o\nX-Api-Key: s3cret\n\n", "GET / HTTP/1.1\r\nHost: o\nX-Api-Key: ****\n\n")]
    [DataRow("GET / HTTP/1.1\r\nHost: o\nAuthorization: Basic YWxpY2U6czNjcmV0\n\n", "GET / HTTP/1.1\r\nHost: o\nAuthorization: Basic ****\n\n")]
    [DataRow("\nGET / HTTP/1.1\nAuthorization: Bearer one\n two\nX-Api-Key: s3cret\r\n\n", "\nGET / HTTP/1.1\nAuthorization: Bearer ****\n ****\nX-Api-Key: ****\r\n\n")]
    [DataRow("GET / HTTP/1.1\nX-Api-Key: s3cret\n\r\n", "GET / HTTP/1.1\nX-Api-Key: ****\n\r\n")]
    public void Redact_HeadLinesEndedByBareLineFeeds_MasksEachSecretField(string received, string expected)
    {
        Assert.AreEqual(expected, Redact(received, new HttpRequestHeadBytesRedactor(["X-Api-Key"])));
        Assert.AreEqual(expected, RedactOneByteAtATime(received, new HttpRequestHeadBytesRedactor(["X-Api-Key"])));
    }

    [TestMethod]
    public void Redact_ABareLineFeed_EndsTheLineAsSoonAsItArrives()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact("GET / HTTP/1.1\nAuthorization: Bearer a\n"u8, output);

        Assert.AreEqual("GET / HTTP/1.1\nAuthorization: Bearer ****\n", Latin1(output));
        Assert.IsFalse(redactor.IsAtRequestStart);

        redactor.Redact("\n"u8, output);

        Assert.IsTrue(redactor.IsAtRequestStart);
    }

    [TestMethod]
    [DataRow("POST /u HTTP/1.1\nContent-Length: 20\n\n", "Authorization: x\n\nGE")]
    [DataRow("POST /u HTTP/1.1\nTransfer-Encoding: chunked\n\n", "12\nAuthorization: x\n\n\n0\n\n")]
    public void Redact_NextHeadAfterABareLineFeedHeadWithABody_IsRedacted(string head, string body)
    {
        const string next = "GET /b HTTP/1.1\nAuthorization: Basic YWxpY2U6czNjcmV0\n\n";
        const string nextRedacted = "GET /b HTTP/1.1\nAuthorization: Basic ****\n\n";

        Assert.AreEqual(head + body + nextRedacted + SampleRedacted, Redact(head + body + next + Sample));
        Assert.AreEqual(head + body + nextRedacted + SampleRedacted, RedactOneByteAtATime(head + body + next + Sample));
    }

    [TestMethod]
    public void Redact_ABodyEndingInACarriageReturn_ThenABareLineFeedBeforeTheNextHead_IsRedacted()
    {
        const string first = "POST /u HTTP/1.1\r\nContent-Length: 2\r\n\r\na\r";

        Assert.AreEqual(first + "\n" + SampleRedacted, Redact(first + "\n" + Sample));
    }

    [TestMethod]
    public void Redact_ALineTooLongToHoldEndedByABareLineFeed_IsMaskedAndTheNextLineIsJudged()
    {
        string tooLong = "X-Long: " + new string('a', LineBytesRedactor.DefaultMaxHeldBytes);

        Assert.AreEqual(
            "GET / HTTP/1.1\n****\nAuthorization: Bearer ****\n\n",
            Redact($"GET / HTTP/1.1\n{tooLong}\nAuthorization: Bearer a\n\n"));
    }

    [TestMethod]
    public void Redact_ABareLineFeedAfterAFlushThatEndedInACarriageReturn_EndsAnEmptyLine()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact("GET / HTTP/1.1\r"u8, output);
        redactor.Flush(output);

        redactor.Redact("\nGET /b HTTP/1.1\nAuthorization: Bearer a\n"u8, output);

        Assert.AreEqual("GET / HTTP/1.1\r\nGET /b HTTP/1.1\nAuthorization: Bearer ****\n", Latin1(output));
    }

    [TestMethod]
    public void Redact_LinesWithNoColonAfterASecretField_AreMaskedAsPartOfIt()
    {
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nAuthorization: Bearer ****\n****\n ****\n****\r\nX: a\nplain\n\n",
            Redact("GET / HTTP/1.1\r\nAuthorization: Bearer abc\ndef\n ghi\njkl\r\nX: a\nplain\n\n"));
    }

    [TestMethod]
    public void Redact_ASecretFieldAfterARequestLineTooLongToHold_IsMasked()
    {
        string tooLong = "GET /" + new string('a', LineBytesRedactor.DefaultMaxHeldBytes) + " HTTP/1.1";

        Assert.AreEqual(
            "****\nAuthorization: Bearer ****\n  ****\n\n" + SampleRedacted,
            Redact($"{tooLong}\nAuthorization: Bearer abc\n  def\n\n{Sample}"));
    }

    private const string ChunkedHead = "POST /u HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n";

    [TestMethod]
    [DataRow("Authorization: Basic YWxpY2U6czNjcmV0\r\n\r\n", "Authorization: Basic ****\r\n\r\n")]
    [DataRow("x-api-key: s3cret\r\n\r\n", "x-api-key: ****\r\n\r\n")]
    [DataRow("Trailer: v\r\nProxy-Authorization: Bearer one\r\n two\r\nthree\r\nX-Api-Key: s3cret\r\nX: a\r\n b\r\n\r\n", "Trailer: v\r\nProxy-Authorization: Bearer ****\r\n ****\r\n****\r\nX-Api-Key: ****\r\nX: a\r\n b\r\n\r\n")]
    [DataRow("X-Api-Key: s3cret\nAuthorization: Bearer a\n\n", "X-Api-Key: ****\nAuthorization: Bearer ****\n\n")]
    public void Redact_SecretFieldInChunkedTrailers_IsMasked(string trailers, string expected)
    {
        string received = ChunkedHead + "2\r\nab\r\n0\r\n" + trailers;
        string redacted = ChunkedHead + "2\r\nab\r\n0\r\n" + expected;

        Assert.AreEqual(redacted, Redact(received, new HttpRequestHeadBytesRedactor(["X-Api-Key"])));
        Assert.AreEqual(redacted, RedactOneByteAtATime(received, new HttpRequestHeadBytesRedactor(["X-Api-Key"])));
    }

    [TestMethod]
    [DataRow("0\r\nAuthorization: Bearer a\r\n\r\n", "0\r\nAuthorization: Bearer ****\r\n\r\n")]
    [DataRow("0\r\nContent-Length: 500\r\nTransfer-Encoding: chunked\r\n\r\n", "0\r\nContent-Length: 500\r\nTransfer-Encoding: chunked\r\n\r\n")]
    [DataRow("0\n\n", "0\n\n")]
    [DataRow("0\r\n\r\r\nAuthorization: Bearer a\r\n\r\n", "0\r\n\r\r\nAuthorization: Bearer ****\r\n\r\n")]
    public void Redact_RequestAfterAChunkedBodysTrailers_IsRedacted(string body, string expectedBody)
    {
        string received = ChunkedHead + body + Sample;
        string redacted = ChunkedHead + expectedBody + SampleRedacted;

        Assert.AreEqual(redacted, Redact(received));
        Assert.AreEqual(redacted, RedactOneByteAtATime(received));
    }

    [TestMethod]
    public void IsAtRequestStart_IsFalseInChunkedTrailersUntilTheirEmptyLine()
    {
        var redactor = new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(Encoding.Latin1.GetBytes(ChunkedHead + "0\r\n"), output);
        Assert.IsFalse(redactor.IsAtRequestStart);

        redactor.Redact("Authorization: Bearer a\r\n"u8, output);
        Assert.IsFalse(redactor.IsAtRequestStart);

        redactor.Redact("\r\n"u8, output);
        Assert.IsTrue(redactor.IsAtRequestStart);
    }

    [TestMethod]
    public void Flush_WritesAnUnfinishedTrailerLineRedacted()
    {
        Assert.AreEqual(
            ChunkedHead + "0\r\nAuthorization: Bearer ****",
            Redact(ChunkedHead + "0\r\nAuthorization: Bearer abc"));
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

    private static string RedactOneByteAtATime(string received, HttpRequestHeadBytesRedactor? redactor = null)
    {
        redactor ??= new HttpRequestHeadBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (byte next in Encoding.Latin1.GetBytes(received))
        {
            redactor.Redact([next], output);
        }

        redactor.Flush(output);
        return Latin1(output);
    }

    private static string Latin1(ArrayBufferWriter<byte> output) => Encoding.Latin1.GetString(output.WrittenSpan);
}
