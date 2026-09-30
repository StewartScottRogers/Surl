using static Surl.Protocol.Smb.SmbTestBytes;

namespace Surl.Protocol.Smb;

[TestClass]
public sealed class SmbResponseEncoderTests
{
    [TestMethod]
    public void EncodeNegotiate_NtLm012Form_WritesEveryFieldTheChallengeAndTheDomain()
    {
        var response = new SmbNegotiateResponse(
            DialectIndex: 0,
            SecurityMode: 0x03,
            MaxMpxCount: 50,
            MaxNumberVirtualCircuits: 1,
            MaxBufferSize: 0x1104,
            MaxRawSize: 0x10000,
            SessionKey: 0x12345678,
            Capabilities: 0x1234,
            SystemTime: 0x01D0_0000_0000_0000,
            ServerTimeZone: -60,
            Challenge: Hex("0102030405060708"),
            DomainName: "WORKGROUP");

        var encoded = SmbResponseEncoder.EncodeNegotiate(RequestHeader(SmbCommand.Negotiate), response);

        var expected = Hex(
            "00000057" + ResponseHeaderHex(SmbCommand.Negotiate)
            + "11" + "0000" + "03" + "3200" + "0100" + "04110000" + "00000100" + "78563412" + "34120000" + "000000000000D001" + "C4FF" + "08"
            + "1200" + "0102030405060708" + TerminatedHex("WORKGROUP"));
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeNegotiate_ChallengeAndSessionKey_SitWhereCurlReadsThem()
    {
        var response = new SmbNegotiateResponse(0, 0, 0, 0, 0, 0, 0xCAFEF00D, 0, 0, 0, Hex("A1A2A3A4A5A6A7A8"), string.Empty);

        var encoded = SmbResponseEncoder.EncodeNegotiate(RequestHeader(SmbCommand.Negotiate), response);

        CollectionAssert.AreEqual(Hex("0DF0FECA"), encoded[52..56]);
        CollectionAssert.AreEqual(Hex("A1A2A3A4A5A6A7A8"), encoded[73..81]);
    }

    [TestMethod]
    public void EncodeSessionSetup_AssignsTheUserIdAndWritesTheStrings()
    {
        var encoded = SmbResponseEncoder.EncodeSessionSetup(RequestHeader(SmbCommand.SessionSetupAndX), 0x0064, 1, "Unix", "Surl", "WORKGROUP");

        var expected = Hex(
            "0000003D" + ResponseHeaderHex(SmbCommand.SessionSetupAndX, identifiersHex: "0201" + "1DD7" + "6400" + "0605")
            + "03" + "FF" + "00" + "0000" + "0100" + "1400" + TerminatedHex("Unix") + TerminatedHex("Surl") + TerminatedHex("WORKGROUP"));
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeTreeConnect_AssignsTheTreeIdAndWritesTheServiceAndFileSystem()
    {
        var encoded = SmbResponseEncoder.EncodeTreeConnect(RequestHeader(SmbCommand.TreeConnectAndX), 0x0007, 0x0001, "A:", "NTFS");

        var expected = Hex(
            "00000031" + ResponseHeaderHex(SmbCommand.TreeConnectAndX, identifiersHex: "0700" + "1DD7" + "0403" + "0605")
            + "03" + "FF" + "00" + "0000" + "0100" + "0800" + TerminatedHex("A:") + TerminatedHex("NTFS"));
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeNtCreate_WritesEveryField()
    {
        var response = new SmbNtCreateResponse(
            OplockLevel: 0,
            FileId: 0x4007,
            CreateAction: 1,
            CreationTime: 0x0101010101010101,
            LastAccessTime: 0x0202020202020202,
            LastWriteTime: 0x0303030303030303,
            LastChangeTime: 0x0404040404040404,
            ExtendedFileAttributes: 0x80,
            AllocationSize: 0x2000,
            EndOfFile: 0x1234,
            ResourceType: 0,
            NamedPipeStatus: 0,
            IsDirectory: false);

        var encoded = SmbResponseEncoder.EncodeNtCreate(RequestHeader(SmbCommand.NtCreateAndX), response);

        var expected = Hex(
            "00000067" + ResponseHeaderHex(SmbCommand.NtCreateAndX)
            + "22" + "FF" + "00" + "0000" + "00" + "0740" + "01000000" + "0101010101010101" + "0202020202020202" + "0303030303030303"
            + "0404040404040404" + "80000000" + "0020000000000000" + "3412000000000000" + "0000" + "0000" + "00" + "0000");
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeNtCreate_Directory_SetsTheDirectoryByte()
    {
        var response = new SmbNtCreateResponse(0, 0, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, IsDirectory: true);

        var encoded = SmbResponseEncoder.EncodeNtCreate(RequestHeader(SmbCommand.NtCreateAndX), response);

        Assert.AreEqual((byte)1, encoded[^3]);
    }

    [TestMethod]
    public void EncodeRead_WritesTheDataAfterAPadByteWithItsOffsetAndLength()
    {
        var encoded = SmbResponseEncoder.EncodeRead(RequestHeader(SmbCommand.ReadAndX), 0xFFFF, "hello"u8);

        var expected = Hex(
            "00000041" + ResponseHeaderHex(SmbCommand.ReadAndX)
            + "0C" + "FF" + "00" + "0000" + "FFFF" + "0000" + "0000" + "0500" + "3C00" + "0000" + "0000000000000000"
            + "0600" + "00" + "68656C6C6F");
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeRead_MessageOver65535Bytes_SetsTheNetBiosLengthExtensionBit()
    {
        var data = new byte[65534];

        var encoded = SmbResponseEncoder.EncodeRead(RequestHeader(SmbCommand.ReadAndX), 0, data);

        Assert.HasCount(4 + 32 + 1 + 24 + 2 + 1 + 65534, encoded);
        CollectionAssert.AreEqual(Hex("0001003A"), encoded[..4]);
    }

    [TestMethod]
    public void EncodeRead_65535BytesOfData_IsRefused()
    {
        var data = new byte[65535];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SmbResponseEncoder.EncodeRead(RequestHeader(SmbCommand.ReadAndX), 0, data));
    }

    [TestMethod]
    public void EncodeWrite_WritesTheCount()
    {
        var encoded = SmbResponseEncoder.EncodeWrite(RequestHeader(SmbCommand.WriteAndX), 3, 0xFFFF);

        var expected = Hex("0000002F" + ResponseHeaderHex(SmbCommand.WriteAndX) + "06" + "FF" + "00" + "0000" + "0300" + "FFFF" + "00000000" + "0000");
        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void EncodeClose_WritesAnEmptySuccess()
    {
        var encoded = SmbResponseEncoder.EncodeClose(RequestHeader(SmbCommand.Close));

        CollectionAssert.AreEqual(Hex("00000023" + ResponseHeaderHex(SmbCommand.Close) + "00" + "0000"), encoded);
    }

    [TestMethod]
    public void EncodeTreeDisconnect_WritesAnEmptySuccess()
    {
        var encoded = SmbResponseEncoder.EncodeTreeDisconnect(RequestHeader(SmbCommand.TreeDisconnect));

        CollectionAssert.AreEqual(Hex("00000023" + ResponseHeaderHex(SmbCommand.TreeDisconnect) + "00" + "0000"), encoded);
    }

    [TestMethod]
    public void EncodeError_WritesTheStatusWithNoParametersOrData()
    {
        var encoded = SmbResponseEncoder.EncodeError(RequestHeader(SmbCommand.NtCreateAndX), 0xC0000034);

        CollectionAssert.AreEqual(Hex("00000023" + ResponseHeaderHex(SmbCommand.NtCreateAndX, statusHex: "340000C0") + "00" + "0000"), encoded);
    }

    [TestMethod]
    public void EncodeError_DosErrorNoAccess_WritesClassAndCodeAsCurlReadsThem()
    {
        var encoded = SmbResponseEncoder.EncodeError(RequestHeader(SmbCommand.TreeConnectAndX), 0x00050001);

        CollectionAssert.AreEqual(Hex("01000500"), encoded[9..13]);
    }
}
