namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpReadRequestTests
{
    [TestMethod]
    public void TryParse_ReadRequestWithOptions_KeepsNamesAndValuesAsSent()
    {
        var datagram = TftpProtocolServerTests.ReadRequest("f", "TSize", "0", "x", "");

        Assert.IsTrue(TftpReadRequest.TryParse(datagram, out var request));

        Assert.AreEqual("octet", request!.Mode);
        CollectionAssert.AreEqual("f"u8.ToArray(), request.FileName);
        CollectionAssert.AreEqual(new[] { new TftpOption("TSize", "0"), new TftpOption("x", "") }, request.Options.ToArray());
    }

    [TestMethod]
    public void TryParse_WriteRequest_IsNotAReadRequest()
    {
        byte[] datagram = [0, 2, .. "f"u8, 0, .. "octet"u8, 0];

        Assert.IsFalse(TftpReadRequest.TryParse(datagram, out var request));

        Assert.IsNull(request);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 1 })]
    [DataRow(new byte[] { 0, 1, 0 })]
    public void TryParse_OpcodeWithNoFields_IsRefused(byte[] datagram)
    {
        Assert.IsFalse(TftpReadRequest.TryParse(datagram, out _));
    }
}
