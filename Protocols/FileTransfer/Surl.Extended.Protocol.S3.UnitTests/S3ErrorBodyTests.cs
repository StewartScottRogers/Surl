namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The <c>Error</c> body of the "Error responses" page, and what of a client's bytes it echoes.
/// </summary>
[TestClass]
public sealed class S3ErrorBodyTests
{
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
