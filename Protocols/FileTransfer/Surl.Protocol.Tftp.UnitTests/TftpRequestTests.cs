namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpRequestTests
{
    [TestMethod]
    public void TryParse_ReadRequestWithOptions_KeepsNamesAndValuesAsSent()
    {
        var datagram = TftpProtocolServerTests.ReadRequest("f", "TSize", "0", "x", "");

        Assert.IsTrue(TftpRequest.TryParse(datagram, out var request));

        Assert.IsFalse(request!.IsWrite);
        Assert.AreEqual("Read", request.Kind);
        Assert.AreEqual("octet", request.Mode);
        CollectionAssert.AreEqual("f"u8.ToArray(), request.FileName);
        CollectionAssert.AreEqual(new[] { new TftpOption("TSize", "0"), new TftpOption("x", "") }, request.Options.ToArray());
    }

    [TestMethod]
    public void TryParse_WriteRequest_IsAWriteRequest()
    {
        byte[] datagram = [0, 2, .. "f"u8, 0, .. "NetASCII"u8, 0, .. "tsize"u8, 0, .. "6"u8, 0];

        Assert.IsTrue(TftpRequest.TryParse(datagram, out var request));

        Assert.IsTrue(request!.IsWrite);
        Assert.AreEqual("Write", request.Kind);
        Assert.AreEqual("netascii", request.Mode);
        CollectionAssert.AreEqual(new[] { new TftpOption("tsize", "6") }, request.Options.ToArray());
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 1 })]
    [DataRow(new byte[] { 0, 1, 0 })]
    [DataRow(new byte[] { 0, 2, 0 })]
    [DataRow(new byte[] { 0, 3, 102, 0, 111, 99, 116, 101, 116, 0 })]
    public void TryParse_NotAWellFormedRequest_IsRefused(byte[] datagram)
    {
        Assert.IsFalse(TftpRequest.TryParse(datagram, out var request));

        Assert.IsNull(request);
    }
}
