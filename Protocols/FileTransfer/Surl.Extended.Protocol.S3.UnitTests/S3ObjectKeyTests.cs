namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The 1024-byte key bound of "Object key naming guidelines", counted after percent-decoding.
/// </summary>
[TestClass]
public sealed class S3ObjectKeyTests
{
    [TestMethod]
    public void IsTooLong_KeyOf1024Bytes_IsNot()
    {
        Assert.IsFalse(S3ObjectKey.IsTooLong(new string('k', 1024)));
    }

    [TestMethod]
    public void IsTooLong_KeyOf1025Bytes_Is()
    {
        Assert.IsTrue(S3ObjectKey.IsTooLong(new string('k', 1025)));
    }

    [TestMethod]
    public void IsTooLong_EncodedKey_CountsTheDecodedUtf8Bytes()
    {
        // 513 two-byte characters, sent as 3078 encoded characters, are 1026 UTF-8 bytes.
        Assert.IsTrue(S3ObjectKey.IsTooLong(string.Concat(Enumerable.Repeat("%C3%A9", 513))));
        Assert.IsFalse(S3ObjectKey.IsTooLong(string.Concat(Enumerable.Repeat("%C3%A9", 512))));
    }
}
