namespace Surl.Cryptography.Srp;

/// <summary>
/// Checks <see cref="GnuTlsSrpBase64.Decode" /> against GnuTLS's SRP base64 (<c>lib/auth/srp_sb64.c</c>):
/// its alphabet, its left-aligned leading group, and GnuTLS's own <c>tpasswd.conf</c> line for RFC
/// 5054's 1024-bit prime.
/// </summary>
[TestClass]
public sealed class GnuTlsSrpBase64Tests
{
    // GnuTLS's tpasswd.conf, group 1 (src/srptool.c's default group list): RFC 5054's 1024-bit prime.
    private const string GnuTlsGroup1Prime =
        "Ewl2hcjiutMd3Fu2lgFnUXWSc67TVyy2vwYCKoS9MLsrdJVT9RgWTCuEqWJrfB6uE3LsE9GkOlaZabS7M29sj5TnzUqOLJMjiwEzArfiLr9WbMRANlF68N5AVLcPWvNx6Zjl3m5Scp0BzJBz9TkgfhzKJZ.WtP3Mv/67I/0wmRZ";

    [TestMethod]
    public void Decode_GnuTlsGroup1Prime_IsRfc5054s1024BitPrime()
    {
        var bytes = GnuTlsSrpBase64.Decode(GnuTlsGroup1Prime);

        CollectionAssert.AreEqual(Srp6a.ToUnsignedBigEndian(SrpGroup.Rfc5054Group1024.Prime), bytes);
    }

    [TestMethod]
    [DataRow("2", "02")]
    [DataRow("z", "3D")]
    [DataRow("10", "40")]
    [DataRow("3/", "FF")]
    [DataRow("3//", "3FFF")]
    [DataRow("0001", "000001")]
    [DataRow("////", "FFFFFF")]
    [DataRow("1////", "01FFFFFF")]
    public void Decode_ValidText_IsItsBytes(string text, string expectedHex)
    {
        var bytes = GnuTlsSrpBase64.Decode(text);

        Assert.AreEqual(expectedHex, Convert.ToHexString(bytes!));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("=")]
    [DataRow("ab+c")]
    [DataRow("G0")]
    [DataRow("///")]
    public void Decode_InvalidText_IsNull(string text)
    {
        var bytes = GnuTlsSrpBase64.Decode(text);

        Assert.IsNull(bytes);
    }

    [TestMethod]
    public void Decode_EncodedSalt_RoundTrips()
    {
        var salt = Rfc5054AppendixB.Salt;

        var bytes = GnuTlsSrpBase64.Decode(UnitTestSrpBase64Encoder.Encode(salt));

        CollectionAssert.AreEqual(salt, bytes);
    }

    [TestMethod]
    public void Decode_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GnuTlsSrpBase64.Decode(null!));
    }
}
