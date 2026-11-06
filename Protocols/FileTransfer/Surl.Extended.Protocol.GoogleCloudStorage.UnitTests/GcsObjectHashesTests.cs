using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// CRC-32C (Castagnoli) and MD5 as <c>x-goog-hash</c> carries them ("HTTP headers and common
/// request headers"): the CRC big-endian, both in base64.
/// </summary>
[TestClass]
public sealed class GcsObjectHashesTests
{
    [TestMethod]
    [DataRow("", "AAAAAA==", DisplayName = "no bytes")]
    [DataRow("123456789", "4waSgw==", DisplayName = "the CRC-32C check value 0xE3069283")]
    [DataRow("hello world", "yZRlqg==", DisplayName = "hello world, 0xC99465AA")]
    public void Finish_Bytes_IsTheBigEndianCrc32c(string text, string base64)
    {
        using var hashes = new GcsObjectHashes();

        hashes.Append(Encoding.ASCII.GetBytes(text));

        Assert.AreEqual(base64, Convert.ToBase64String(hashes.Finish().Crc32c));
    }

    [TestMethod]
    public void Finish_BytesInSeveralAppends_IsTheHashOfThemAll()
    {
        using var hashes = new GcsObjectHashes();

        hashes.Append(Encoding.ASCII.GetBytes("hello"));
        hashes.Append(Encoding.ASCII.GetBytes(" world"));
        var digests = hashes.Finish();

        Assert.AreEqual("crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==", digests.HashField);
        Assert.AreEqual("\"5eb63bbbe01eeed093cb22bb8f5acdc3\"", digests.ETag);
    }
}
