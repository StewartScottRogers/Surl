using System.Reflection;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The <c>Error</c> body of the "Error responses" page, and what of a client's bytes it echoes.
/// </summary>
[TestClass]
public sealed class S3ErrorBodyTests
{
    [TestMethod]
    public void Render_SignatureMismatch_WritesStringToSignThenCanonicalRequestBetweenMessageAndResource()
    {
        var error = S3Error.SignatureDoesNotMatch with { SignatureMismatch = new AwsSigV4SignatureMismatch("GET\n/a\n\nhost:s3.test\n", "AWS4-HMAC-SHA256\n20261105T120000Z") };

        var body = S3ErrorBody.Render(error, "/a", "4442587FB7D0A2F9", []);

        Assert.AreEqual(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Error><Code>SignatureDoesNotMatch</Code><Message>The request signature we calculated does not match the signature you provided. Check your key and signing method.</Message>"
            + "<StringToSign>AWS4-HMAC-SHA256\n20261105T120000Z</StringToSign><CanonicalRequest>GET\n/a\n\nhost:s3.test\n</CanonicalRequest><Resource>/a</Resource><RequestId>4442587FB7D0A2F9</RequestId></Error>",
            Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void Render_CanonicalRequestHoldingMarkup_IsEscaped()
    {
        var error = S3Error.SignatureDoesNotMatch with { SignatureMismatch = new AwsSigV4SignatureMismatch("a=1&b=<2>", "<scope>&") };

        var body = Encoding.UTF8.GetString(S3ErrorBody.Render(error, "/a", "4442587FB7D0A2F9", []));

        Assert.Contains("<StringToSign>&lt;scope&gt;&amp;</StringToSign>", body);
        Assert.Contains("<CanonicalRequest>a=1&amp;b=&lt;2&gt;</CanonicalRequest>", body);
    }

    [TestMethod]
    public void Render_EveryErrorWithoutASignatureMismatch_HasNeitherElement()
    {
        var errors = typeof(S3Error).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => (S3Error)property.GetValue(null)!)
            .Append(S3Error.ChecksumMismatch("CRC32"))
            .ToList();

        foreach (var error in errors)
        {
            var body = Encoding.UTF8.GetString(S3ErrorBody.Render(error, "/a", "4442587FB7D0A2F9", []));

            Assert.DoesNotContain("<StringToSign>", body);
            Assert.DoesNotContain("<CanonicalRequest>", body);
        }

        CollectionAssert.Contains(errors, S3Error.SignatureDoesNotMatch);
    }

    [TestMethod]
    public void EchoedLines_SignedTextPastTheEchoLimit_IsNotCut()
    {
        var signed = new string('a', S3ErrorBody.MaxEchoedLength) + "\nmore";

        Assert.AreEqual(signed, S3ErrorBody.EchoedLines(signed));
    }

    [TestMethod]
    public void EchoedLines_AnythingButPrintableAsciiAndLineFeeds_IsReplaced()
    {
        Assert.AreEqual("a??\nb??", S3ErrorBody.EchoedLines("aé\r\nb\t\u0001"));
    }

    [TestMethod]
    public void Render_NoSuchKey_IsTheDeclarationThenOneLine()
    {
        var body = S3ErrorBody.Render(S3Error.NoSuchKey, "/photos/2026/a.jpg", "4442587FB7D0A2F9", [new("Key", "2026/a.jpg")]);

        Assert.AreEqual(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Error><Code>NoSuchKey</Code><Message>The specified key does not exist.</Message><Key>2026/a.jpg</Key><Resource>/photos/2026/a.jpg</Resource><RequestId>4442587FB7D0A2F9</RequestId></Error>",
            System.Text.Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    [DataRow("/a&b<c>", "/a&amp;b&lt;c&gt;", DisplayName = "markup escaped")]
    [DataRow("/aé\tb", "/a??b", DisplayName = "anything but printable ASCII replaced")]
    [DataRow("/a\nb", "/a?b", DisplayName = "a line feed replaced")]
    public void Echoed_ClientValue_IsPrintableAndEscaped(string value, string expected)
    {
        Assert.AreEqual(expected, S3ErrorBody.Echoed(value));
    }

    [TestMethod]
    public void Echoed_ValuePastTheLimit_IsCutBeforeEscaping()
    {
        var echoed = S3ErrorBody.Echoed(new string('a', S3ErrorBody.MaxEchoedLength) + "&more");

        Assert.AreEqual(new string('a', S3ErrorBody.MaxEchoedLength), echoed);
    }
}
