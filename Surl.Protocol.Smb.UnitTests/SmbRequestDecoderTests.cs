using static Surl.Protocol.Smb.SmbTestBytes;

namespace Surl.Protocol.Smb;

[TestClass]
public sealed class SmbRequestDecoderTests
{
    // SMB_COM_SESSION_SETUP_ANDX parameters as curl sends them: no AndX command, MaxBufferSize
    // 0x9000, MaxMpxCount 1, VcNumber 1, SessionKey 0x12345678, 24-byte LM and NT responses,
    // capabilities SMB_CAP_LARGE_FILES.
    private const string SessionSetupParametersHex =
        "0D" + "FF" + "00" + "0000" + "0090" + "0100" + "0100" + "78563412" + "1800" + "1800" + "00000000" + "08000000";

    private static readonly string LmResponseHex = string.Concat(Enumerable.Repeat("11", 24));
    private static readonly string NtResponseHex = string.Concat(Enumerable.Repeat("22", 24));

    [TestMethod]
    public void Decode_Negotiate_ReadsItsDialects()
    {
        var message = Request(SmbCommand.Negotiate, "00" + "0C00" + "02" + TerminatedHex("NT LM 0.12"));

        var decoding = SmbRequestDecoder.Decode(message);

        var request = (SmbNegotiateRequest)decoding.Request!;
        Assert.AreEqual(SmbRequestFault.None, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.Negotiate), decoding.Header);
        Assert.AreEqual(RequestHeader(SmbCommand.Negotiate), request.Header);
        CollectionAssert.AreEqual(new[] { "NT LM 0.12" }, request.Dialects.ToArray());
    }

    [TestMethod]
    public void Decode_NegotiateWithTwoDialectsAndPadding_ReadsBothAndIgnoresThePadding()
    {
        var message = Request(SmbCommand.Negotiate, "00" + "0F00" + "02" + TerminatedHex("PC NETWORK") + "02" + TerminatedHex("X") + "EEEE");

        var request = (SmbNegotiateRequest)SmbRequestDecoder.Decode(message).Request!;

        CollectionAssert.AreEqual(new[] { "PC NETWORK", "X" }, request.Dialects.ToArray());
    }

    [TestMethod]
    public void Decode_SessionSetup_ReadsItsParametersResponsesAndStrings()
    {
        var bytesHex = LmResponseHex + NtResponseHex + TerminatedHex("user") + TerminatedHex("DOMAIN") + TerminatedHex("OS") + TerminatedHex("curl");
        var message = Request(SmbCommand.SessionSetupAndX, SessionSetupParametersHex + "4400" + bytesHex);

        var request = (SmbSessionSetupRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.SessionSetupAndX), request.Header);
        Assert.AreEqual((ushort)0x9000, request.MaxBufferSize);
        Assert.AreEqual((ushort)1, request.MaxMpxCount);
        Assert.AreEqual((ushort)1, request.VirtualCircuitNumber);
        Assert.AreEqual(0x12345678u, request.SessionKey);
        Assert.AreEqual(0x08u, request.Capabilities);
        CollectionAssert.AreEqual(Hex(LmResponseHex), request.LmResponse);
        CollectionAssert.AreEqual(Hex(NtResponseHex), request.NtResponse);
        Assert.AreEqual("user", request.AccountName);
        Assert.AreEqual("DOMAIN", request.PrimaryDomain);
        Assert.AreEqual("OS", request.NativeOperatingSystem);
        Assert.AreEqual("curl", request.NativeLanManager);
    }

    [TestMethod]
    public void Decode_TreeConnect_ReadsThePathAndService()
    {
        var message = Request(SmbCommand.TreeConnectAndX, "04" + "FF" + "00" + "0000" + "0000" + "0000" + "1300" + TerminatedHex(@"\\host\share") + TerminatedHex("?????"));

        var request = (SmbTreeConnectRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.TreeConnectAndX), request.Header);
        Assert.AreEqual((ushort)0, request.Flags);
        Assert.IsEmpty(request.Password);
        Assert.AreEqual(@"\\host\share", request.Path);
        Assert.AreEqual("?????", request.Service);
    }

    [TestMethod]
    public void Decode_TreeConnectWithAPassword_ReadsThePasswordBeforeThePath()
    {
        var message = Request(SmbCommand.TreeConnectAndX, "04" + "FF" + "00" + "0000" + "0800" + "0100" + "0800" + "00" + TerminatedHex(@"\\h\s") + "00");

        var request = (SmbTreeConnectRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual((ushort)8, request.Flags);
        CollectionAssert.AreEqual(new byte[] { 0 }, request.Password);
        Assert.AreEqual(@"\\h\s", request.Path);
        Assert.AreEqual(string.Empty, request.Service);
    }

    [TestMethod]
    public void Decode_NtCreate_ReadsEveryField()
    {
        var parametersHex = "18" + "FF" + "00" + "0000" + "00" + "0800" + "16000000" + "0A000000" + "000000C0" + "0010000000000000"
            + "80000000" + "07000000" + "05000000" + "40000000" + "02000000" + "03";
        var message = Request(SmbCommand.NtCreateAndX, parametersHex + "0900" + TerminatedHex("file.txt"));

        var request = (SmbNtCreateRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.NtCreateAndX), request.Header);
        Assert.AreEqual(0x16u, request.Flags);
        Assert.AreEqual(0x0Au, request.RootDirectoryFileId);
        Assert.AreEqual(0xC0000000u, request.DesiredAccess);
        Assert.AreEqual(0x1000L, request.AllocationSize);
        Assert.AreEqual(0x80u, request.ExtendedFileAttributes);
        Assert.AreEqual(0x07u, request.ShareAccess);
        Assert.AreEqual(0x05u, request.CreateDisposition);
        Assert.AreEqual(0x40u, request.CreateOptions);
        Assert.AreEqual(0x02u, request.ImpersonationLevel);
        Assert.AreEqual((byte)0x03, request.SecurityFlags);
        Assert.AreEqual("file.txt", request.FileName);
    }

    [TestMethod]
    public void Decode_NtCreateWhoseNameLengthCountsTheTerminator_DropsTheTerminator()
    {
        var parametersHex = "18" + "FF" + "00" + "0000" + "00" + "0200" + string.Concat(Enumerable.Repeat("00", 41));
        var message = Request(SmbCommand.NtCreateAndX, parametersHex + "0200" + TerminatedHex("a"));

        var request = (SmbNtCreateRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual("a", request.FileName);
    }

    [TestMethod]
    public void Decode_ReadWithTheHighOffset_ReadsA64BitOffset()
    {
        var message = Request(SmbCommand.ReadAndX, "0C" + "FF" + "00" + "0000" + "0740" + "00100000" + "0080" + "0070" + "05000000" + "0600" + "01000000" + "0000");

        var request = (SmbReadRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.ReadAndX), request.Header);
        Assert.AreEqual((ushort)0x4007, request.FileId);
        Assert.AreEqual(0x1_0000_1000L, request.Offset);
        Assert.AreEqual((ushort)0x8000, request.MaxCount);
        Assert.AreEqual((ushort)0x7000, request.MinCount);
        Assert.AreEqual(5u, request.Timeout);
        Assert.AreEqual((ushort)6, request.Remaining);
    }

    [TestMethod]
    public void Decode_ReadWithoutTheHighOffset_ReadsA32BitOffset()
    {
        var message = Request(SmbCommand.ReadAndX, "0A" + "FF" + "00" + "0000" + "0740" + "FFFFFFFF" + "0080" + "0080" + "00000000" + "0000" + "0000");

        var request = (SmbReadRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(0xFFFF_FFFFL, request.Offset);
    }

    [TestMethod]
    public void Decode_Write_ReadsTheDataWhereTheOffsetPoints()
    {
        var parametersHex = "0E" + "FF" + "00" + "0000" + "0740" + "00200000" + "07000000" + "0100" + "0200" + "0000" + "0300" + "4000" + "02000000";
        var message = Request(SmbCommand.WriteAndX, parametersHex + "0400" + "00" + "616263");

        var request = (SmbWriteRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.WriteAndX), request.Header);
        Assert.AreEqual((ushort)0x4007, request.FileId);
        Assert.AreEqual(0x2_0000_2000L, request.Offset);
        Assert.AreEqual(7u, request.Timeout);
        Assert.AreEqual((ushort)1, request.WriteMode);
        Assert.AreEqual((ushort)2, request.Remaining);
        CollectionAssert.AreEqual("abc"u8.ToArray(), request.Data);
    }

    [TestMethod]
    public void Decode_WriteWithoutTheHighOffset_ReadsA32BitOffset()
    {
        var parametersHex = "0C" + "FF" + "00" + "0000" + "0740" + "00200000" + "00000000" + "0000" + "0000" + "0000" + "0100" + "3C00";
        var message = Request(SmbCommand.WriteAndX, parametersHex + "0200" + "00" + "7A");

        var request = (SmbWriteRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(0x2000L, request.Offset);
        CollectionAssert.AreEqual("z"u8.ToArray(), request.Data);
    }

    [TestMethod]
    public void Decode_Close_ReadsTheFileIdAndTime()
    {
        var message = Request(SmbCommand.Close, "03" + "0740" + "FFFFFFFF" + "0000");

        var request = (SmbCloseRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.Close), request.Header);
        Assert.AreEqual((ushort)0x4007, request.FileId);
        Assert.AreEqual(0xFFFFFFFFu, request.LastTimeModified);
    }

    [TestMethod]
    public void Decode_TreeDisconnect_ReadsTheHeader()
    {
        var message = Request(SmbCommand.TreeDisconnect, "00" + "0000");

        var request = (SmbTreeDisconnectRequest)SmbRequestDecoder.Decode(message).Request!;

        Assert.AreEqual(RequestHeader(SmbCommand.TreeDisconnect), request.Header);
    }

    [TestMethod]
    public void Decode_ShorterThanTheHeader_IsTruncatedWithNoHeader()
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.Negotiate, string.Empty)[..31]);

        Assert.AreEqual(SmbRequestFault.Truncated, decoding.Fault);
        Assert.IsNull(decoding.Header);
        Assert.IsNull(decoding.Request);
    }

    [TestMethod]
    [DataRow("", DisplayName = "no word count")]
    [DataRow("00", DisplayName = "no byte count")]
    [DataRow("0D FF000000", DisplayName = "parameters cut short")]
    [DataRow("00 00", DisplayName = "byte count cut short")]
    public void Decode_EndingInsideItsBlocks_IsTruncatedWithItsHeader(string bodyHex)
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.SessionSetupAndX, bodyHex));

        Assert.AreEqual(SmbRequestFault.Truncated, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.SessionSetupAndX), decoding.Header);
    }

    [TestMethod]
    public void Decode_Smb2Signature_IsABadSignature()
    {
        var message = Request(SmbCommand.Negotiate, "00" + "0000");
        message[0] = 0xFE;

        var decoding = SmbRequestDecoder.Decode(message);

        Assert.AreEqual(SmbRequestFault.BadSignature, decoding.Fault);
        Assert.IsNull(decoding.Header);
    }

    [TestMethod]
    [DataRow(SmbCommand.Negotiate, 1, DisplayName = "negotiate with a word")]
    [DataRow(SmbCommand.SessionSetupAndX, 12, DisplayName = "session setup with 12 words")]
    [DataRow(SmbCommand.TreeConnectAndX, 3, DisplayName = "tree connect with 3 words")]
    [DataRow(SmbCommand.NtCreateAndX, 1, DisplayName = "NT create with 1 word")]
    [DataRow(SmbCommand.ReadAndX, 11, DisplayName = "read with 11 words")]
    [DataRow(SmbCommand.WriteAndX, 13, DisplayName = "write with 13 words")]
    [DataRow(SmbCommand.Close, 2, DisplayName = "close with 2 words")]
    [DataRow(SmbCommand.TreeDisconnect, 1, DisplayName = "tree disconnect with a word")]
    public void Decode_WordCountTheCommandDoesNotHave_IsAnInconsistentWordCount(byte command, int wordCount)
    {
        var bodyHex = wordCount.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + "FF" + string.Concat(Enumerable.Repeat("00", (2 * wordCount) - 1)) + "0000";

        var decoding = SmbRequestDecoder.Decode(Request(command, bodyHex));

        Assert.AreEqual(SmbRequestFault.InconsistentWordCount, decoding.Fault);
        Assert.AreEqual(RequestHeader(command), decoding.Header);
        Assert.IsNull(decoding.Request);
    }

    [TestMethod]
    public void Decode_ByteCountPastTheMessage_IsAnInconsistentByteCount()
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.Negotiate, "00" + "0D00" + "02" + TerminatedHex("NT LM 0.12")));

        Assert.AreEqual(SmbRequestFault.InconsistentByteCount, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.Negotiate), decoding.Header);
    }

    [TestMethod]
    public void Decode_SessionSetupResponsesPastTheByteCount_IsAnInconsistentByteCount()
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.SessionSetupAndX, SessionSetupParametersHex + "2F00" + LmResponseHex + NtResponseHex[..^2]));

        Assert.AreEqual(SmbRequestFault.InconsistentByteCount, decoding.Fault);
    }

    [TestMethod]
    public void Decode_TreeConnectPasswordPastTheByteCount_IsAnInconsistentByteCount()
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.TreeConnectAndX, "04 FF000000 0000 0300 0200 0000"));

        Assert.AreEqual(SmbRequestFault.InconsistentByteCount, decoding.Fault);
    }

    [TestMethod]
    public void Decode_NtCreateNamePastTheByteCount_IsAnInconsistentByteCount()
    {
        var parametersHex = "18" + "FF" + "00" + "0000" + "00" + "0900" + string.Concat(Enumerable.Repeat("00", 41));

        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.NtCreateAndX, parametersHex + "0800" + "66696C652E747874"));

        Assert.AreEqual(SmbRequestFault.InconsistentByteCount, decoding.Fault);
    }

    [TestMethod]
    [DataRow("00 0B00 01" + "4E54204C4D20302E313200", DisplayName = "dialect without its buffer format byte")]
    [DataRow("00 0B00 02" + "4E54204C4D20302E3132", DisplayName = "dialect without its terminator")]
    public void Decode_NegotiateWithAMalformedDialect_IsAMalformedString(string bodyHex)
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.Negotiate, bodyHex));

        Assert.AreEqual(SmbRequestFault.MalformedString, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.Negotiate), decoding.Header);
    }

    [TestMethod]
    public void Decode_SessionSetupWithAnUnterminatedString_IsAMalformedString()
    {
        var bytesHex = LmResponseHex + NtResponseHex + TerminatedHex("user") + TerminatedHex("DOMAIN") + TerminatedHex("OS") + "6375726C";

        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.SessionSetupAndX, SessionSetupParametersHex + "4300" + bytesHex));

        Assert.AreEqual(SmbRequestFault.MalformedString, decoding.Fault);
    }

    [TestMethod]
    public void Decode_TreeConnectWithAnUnterminatedService_IsAMalformedString()
    {
        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.TreeConnectAndX, "04 FF000000 0000 0000 0400" + TerminatedHex("p") + "4131"));

        Assert.AreEqual(SmbRequestFault.MalformedString, decoding.Fault);
    }

    [TestMethod]
    [DataRow("4000", "0400", DisplayName = "data running past the message")]
    [DataRow("2000", "0100", DisplayName = "data inside the header")]
    [DataRow("3E00", "0100", DisplayName = "data inside the byte count")]
    public void Decode_WriteDataOutsideTheMessage_IsAnOffsetOutsideTheMessage(string dataOffsetHex, string dataLengthHex)
    {
        var parametersHex = "0E" + "FF" + "00" + "0000" + "0740" + "00000000" + "00000000" + "0000" + "0000" + "0000" + dataLengthHex + dataOffsetHex + "00000000";

        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.WriteAndX, parametersHex + "0400" + "00" + "616263"));

        Assert.AreEqual(SmbRequestFault.OffsetOutsideMessage, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.WriteAndX), decoding.Header);
    }

    [TestMethod]
    public void Decode_SessionSetupChainingATreeConnect_IsAChainedAndXCommand()
    {
        var parametersHex = "0D" + "75" + SessionSetupParametersHex[4..];
        var bytesHex = LmResponseHex + NtResponseHex + TerminatedHex("user") + TerminatedHex("DOMAIN") + TerminatedHex("OS") + TerminatedHex("curl");

        var decoding = SmbRequestDecoder.Decode(Request(SmbCommand.SessionSetupAndX, parametersHex + "4400" + bytesHex));

        Assert.AreEqual(SmbRequestFault.ChainedAndXCommand, decoding.Fault);
        Assert.AreEqual(RequestHeader(SmbCommand.SessionSetupAndX), decoding.Header);
        Assert.IsNull(decoding.Request);
    }

    [TestMethod]
    public void Decode_CommandCurlNeverSends_IsAnUnsupportedCommand()
    {
        var decoding = SmbRequestDecoder.Decode(Request(0x25, "00" + "0000"));

        Assert.AreEqual(SmbRequestFault.UnsupportedCommand, decoding.Fault);
        Assert.AreEqual(RequestHeader(0x25), decoding.Header);
    }
}
