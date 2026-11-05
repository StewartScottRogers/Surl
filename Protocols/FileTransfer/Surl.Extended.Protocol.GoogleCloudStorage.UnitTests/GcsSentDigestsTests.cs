namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The digests a PUT Object sends, from "HTTP headers and common request headers" (ADR-0110, decision 1.2).
/// </summary>
[TestClass]
public sealed class GcsSentDigestsTests
{
    [TestMethod]
    public void Read_HashesInSeparateFields_ReadsEach()
    {
        KeyValuePair<string, string>[] fields = [new("X-Goog-Hash", "crc32c=yZRlqg=="), new("x-goog-hash", " md5=XrY7u+Ae7tCTyyK7j1rNww== , ")];

        var sent = GcsSentDigests.Read(fields, out var refusal);

        Assert.IsNull(refusal);
        Assert.IsNotNull(sent);
        Assert.IsNull(sent.ContentMd5);
        Assert.AreEqual("yZRlqg==", Convert.ToBase64String(sent.HashCrc32c!));
        Assert.AreEqual("XrY7u+Ae7tCTyyK7j1rNww==", Convert.ToBase64String(sent.HashMd5!));
    }

    [TestMethod]
    public void Read_ContentMd5TooLongToDecode_IsInvalidDigest()
    {
        KeyValuePair<string, string>[] fields = [new("Content-MD5", Convert.ToBase64String(new byte[100]))];

        Assert.IsNull(GcsSentDigests.Read(fields, out var refusal));
        Assert.AreEqual("InvalidDigest", refusal!.Code);
    }

    [TestMethod]
    public void Match_NothingSent_Matches()
    {
        var sent = new GcsSentDigests(null, null, null);

        Assert.IsTrue(sent.Match(new GcsObjectDigests(new byte[4], new byte[16])));
    }
}
