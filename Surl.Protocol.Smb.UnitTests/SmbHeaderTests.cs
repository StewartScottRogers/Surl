using static Surl.Protocol.Smb.SmbTestBytes;

namespace Surl.Protocol.Smb;

[TestClass]
public sealed class SmbHeaderTests
{
    [TestMethod]
    public void Read_CurlsRequestHeader_ReadsEveryField()
    {
        var header = SmbHeader.Read(Hex(RequestHeaderHex(SmbCommand.ReadAndX)));

        Assert.AreEqual(RequestHeader(SmbCommand.ReadAndX), header);
    }

    [TestMethod]
    public void Write_Header_WritesTheLayoutOfMsCifs()
    {
        var destination = Enumerable.Repeat((byte)0xEE, 40).ToArray();
        var header = new SmbHeader(SmbCommand.Close, 0xC0000022, 0x98, 0xC801, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA);

        header.Write(destination);

        var expected = Hex("FF534D42" + "04" + "220000C0" + "98" + "01C8" + "2211" + "0000000000000000" + "0000" + "4433" + "6655" + "8877" + "AA99");
        CollectionAssert.AreEqual(expected, destination[..32]);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xEE, 8).ToArray(), destination[32..]);
    }

    [TestMethod]
    public void ToResponse_RequestHeader_AddsTheReplyFlagAndStatusAndKeepsTheIdentifiers()
    {
        var response = RequestHeader(SmbCommand.Negotiate).ToResponse(0xC000006D);

        Assert.AreEqual(RequestHeader(SmbCommand.Negotiate) with { Flags = 0x98, Status = 0xC000006D }, response);
    }
}
