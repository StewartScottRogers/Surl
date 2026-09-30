using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosSecurityContext" /> (ADR-0057 decision 5): the AP-REP, and the RFC 4121
/// section 4.2 wrap and MIC tokens both ways, each checked here with the key usage the RFC gives
/// it.
/// </summary>
[TestClass]
public sealed class KerberosSecurityContextTests
{
    private static readonly byte[] Message = Encoding.ASCII.GetBytes("surl says hello");

    [TestMethod]
    [DataRow(true, DisplayName = "mutual-required")]
    [DataRow(false, DisplayName = "not mutual-required")]
    public void IsMutualAuthenticationRequested_FollowsMutualRequired(bool mutualRequired)
    {
        KerberosSecurityContext context = Accept(new ApRequestBuilder { MutualRequired = mutualRequired });

        Assert.AreEqual(mutualRequired, context.IsMutualAuthenticationRequested);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, DisplayName = "17")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, DisplayName = "20")]
    public void CreateApRepToken_DecryptsUnderKeyUsage12ToTheAuthenticatorsTimeAndTheRandomSequenceNumber(KerberosEncryptionType encryptionType)
    {
        ApRequestBuilder builder = new() { TicketEncryptionType = encryptionType, SubkeyTypeNumber = 17 };
        KerberosSecurityContext context = Accept(builder);

        byte[] token = context.CreateApRepToken();

        byte[] expectedPrefix = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02, 0x02, 0x00];
        byte[] content = ReadApplication0Content(token);
        CollectionAssert.AreEqual(expectedPrefix, content[..expectedPrefix.Length]);
        (int encryptionTypeNumber, byte[] cipherText) = ReadApRep(content[expectedPrefix.Length..]);
        Assert.AreEqual((int)encryptionType, encryptionTypeNumber);
        Assert.IsTrue(KerberosEncryptionProfile.For(encryptionType).TryDecrypt(builder.SessionKey, 12, cipherText, out byte[] encApRepPart));
        AsnReader fields = ReadApplicationSequence(encApRepPart, 27);
        Assert.AreEqual(ApRequestBuilder.Now, fields.ReadSequence(ContextTag(0)).ReadGeneralizedTime());
        Assert.IsTrue(fields.ReadSequence(ContextTag(1)).TryReadInt32(out int microseconds));
        Assert.AreEqual(123456, microseconds);
        Assert.IsTrue(fields.ReadSequence(ContextTag(3)).TryReadUInt32(out uint sequenceNumber));
        Assert.AreEqual(0xA0A1A2A3U, sequenceNumber);
        Assert.IsFalse(fields.HasData);
    }

    [TestMethod]
    public void CreateApRepToken_UsesTheInjectedConfounder()
    {
        ApRequestBuilder builder = new();
        KerberosSecurityContext context = Accept(builder);

        byte[] token = context.CreateApRepToken();

        (_, byte[] cipherText) = ReadApRep(ReadApplication0Content(token)[13..]);
        byte[] confounder = Enumerable.Range(0xA4, 16).Select(value => (byte)value).ToArray();
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196);
        KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196).TryDecrypt(builder.SessionKey, 12, cipherText, out byte[] plainText);
        CollectionAssert.AreEqual(profile.Encrypt(builder.SessionKey, 12, confounder, plainText), cipherText);
    }

    [TestMethod]
    public void CreateApRepToken_MutualNotRequested_Throws()
    {
        KerberosSecurityContext context = Accept(new ApRequestBuilder { MutualRequired = false });

        Assert.ThrowsExactly<InvalidOperationException>(() => context.CreateApRepToken());
    }

    [TestMethod]
    public void Wrap_Message_IsAnAcceptorWrapTokenWithoutConfidentialityChecksummedUnderKeyUsage22()
    {
        KerberosSecurityContext context = Accept(new ApRequestBuilder());
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196);

        byte[] token = context.Wrap(Message);

        byte[] header = InitiatorTokens.Header([0x05, 0x04], 0x01, 12, 0, 0xA0A1A2A3);
        CollectionAssert.AreEqual(header, token[..16]);
        CollectionAssert.AreEqual(Message, token[16..^12]);
        byte[] checkedHeader = InitiatorTokens.Header([0x05, 0x04], 0x01, 0, 0, 0xA0A1A2A3);
        CollectionAssert.AreEqual(profile.ComputeChecksum(new ApRequestBuilder().SessionKey, 22, [.. Message, .. checkedHeader]), token[^12..]);
    }

    [TestMethod]
    public void Wrap_TwoMessages_CountTheSequenceNumberUp()
    {
        KerberosSecurityContext context = Accept(new ApRequestBuilder());

        context.Wrap(Message);
        byte[] second = context.Wrap(Message);

        Assert.AreEqual(0xA0A1A2A4UL, BinaryPrimitives.ReadUInt64BigEndian(second.AsSpan(8)));
    }

    [TestMethod]
    public void Wrap_SubkeyInTheAuthenticator_ChecksumsUnderTheSubkey()
    {
        ApRequestBuilder builder = new() { SubkeyTypeNumber = (int)KerberosEncryptionType.Aes128CtsHmacSha256128 };
        KerberosSecurityContext context = Accept(builder);

        byte[] token = context.Wrap(Message);

        byte[] checkedHeader = InitiatorTokens.Header([0x05, 0x04], 0x01, 0, 0, 0xA0A1A2A3);
        byte[] expected = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes128CtsHmacSha256128)
            .ComputeChecksum(builder.Subkey, 22, [.. Message, .. checkedHeader]);
        CollectionAssert.AreEqual(expected, token[^16..]);
    }

    [TestMethod]
    public void GetMic_Message_IsAnAcceptorMicTokenUnderKeyUsage23()
    {
        ApRequestBuilder builder = new() { MutualRequired = false };
        KerberosSecurityContext context = Accept(builder);

        byte[] token = context.GetMic(Message);

        byte[] header = InitiatorTokens.Header([0x04, 0x04], 0x01, 0xFFFF, 0xFFFF, 0x01020304);
        CollectionAssert.AreEqual(header, token[..16]);
        byte[] expected = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196)
            .ComputeChecksum(builder.SessionKey, 23, [.. Message, .. header]);
        CollectionAssert.AreEqual(expected, token[16..]);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "RRC 0")]
    [DataRow(5, DisplayName = "RRC 5")]
    [DataRow(12, DisplayName = "RRC as long as the checksum")]
    [DataRow(1000, DisplayName = "RRC longer than the data")]
    public void TryUnwrap_ClientTokenWithoutConfidentiality_GivesTheMessage(int rightRotationCount)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(client.WrapSigned(Message, 0x01020304, (ushort)rightRotationCount), out byte[] message);

        Assert.IsTrue(isIntact);
        CollectionAssert.AreEqual(Message, message);
    }

    [TestMethod]
    [DataRow(0, 0, DisplayName = "EC 0, RRC 0")]
    [DataRow(0, 28, DisplayName = "EC 0, RRC 28")]
    [DataRow(3, 7, DisplayName = "EC 3, RRC 7")]
    public void TryUnwrap_ClientTokenWithConfidentiality_GivesTheMessage(int extraCount, int rightRotationCount)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(client.WrapSealed(Message, 0x01020304, (ushort)extraCount, (ushort)rightRotationCount), out byte[] message);

        Assert.IsTrue(isIntact);
        CollectionAssert.AreEqual(Message, message);
    }

    [TestMethod]
    public void TryUnwrap_EmptyMessageWithRotation_GivesAnEmptyMessage()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(client.WrapSigned([], 0x01020304, 3), out byte[] message);

        Assert.IsTrue(isIntact);
        Assert.IsEmpty(message);
    }

    [TestMethod]
    public void TryUnwrap_ConsecutiveTokens_EachNeedsTheClientsNextSequenceNumber()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool first = context.TryUnwrap(client.WrapSigned(Message, 0x01020304), out _);
        bool replayed = context.TryUnwrap(client.WrapSigned(Message, 0x01020304), out _);
        bool second = context.TryUnwrap(client.WrapSealed(Message, 0x01020305), out _);

        Assert.IsTrue(first);
        Assert.IsFalse(replayed);
        Assert.IsTrue(second);
    }

    [TestMethod]
    public void TryUnwrap_WrongSequenceNumber_IsFalse()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(client.WrapSigned(Message, 0x01020305), out byte[] message);

        Assert.IsFalse(isIntact);
        Assert.IsEmpty(message);
    }

    [TestMethod]
    [DataRow(0x01, DisplayName = "SentByAcceptor")]
    [DataRow(0x04, DisplayName = "AcceptorSubkey")]
    public void TryUnwrap_AcceptorFlaggedToken_IsFalse(int flags)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(client.WrapSigned(Message, 0x01020304, flags: (byte)flags), out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    public void TryUnwrap_AcceptorsOwnToken_IsFalse()
    {
        KerberosSecurityContext context = Accept(new ApRequestBuilder { MutualRequired = false });

        bool isIntact = context.TryUnwrap(context.Wrap(Message), out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    [DataRow(16, DisplayName = "the message")]
    [DataRow(40, DisplayName = "the checksum")]
    public void TryUnwrap_TamperedTokenWithoutConfidentiality_IsFalse(int index)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());
        byte[] token = client.WrapSigned(Message, 0x01020304);
        token[index] ^= 0x01;

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    public void TryUnwrap_TamperedTokenWithConfidentiality_IsFalse()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());
        byte[] token = client.WrapSealed(Message, 0x01020304);
        token[20] ^= 0x01;

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    [DataRow(2, DisplayName = "flags")]
    [DataRow(15, DisplayName = "sequence number")]
    public void TryUnwrap_SealedHeaderCopyDiffersFromTheClearHeader_IsFalse(int index)
    {
        ApRequestBuilder builder = new();
        (KerberosSecurityContext context, _) = AcceptWithClient(builder);
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196);
        byte[] header = InitiatorTokens.Header([0x05, 0x04], 0x02, 0, 0, 0x01020304);
        byte[] encryptedHeader = [.. header];
        encryptedHeader[index] ^= 0x08;
        byte[] token = [.. header, .. profile.Encrypt(builder.SessionKey, 24, ApRequestBuilder.Confounder, [.. Message, .. encryptedHeader])];

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    public void TryUnwrap_SealedWithExtraCountLongerThanThePlainText_IsFalse()
    {
        ApRequestBuilder builder = new();
        (KerberosSecurityContext context, _) = AcceptWithClient(builder);
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196);
        byte[] header = InitiatorTokens.Header([0x05, 0x04], 0x02, 200, 0, 0x01020304);
        byte[] token = [.. header, .. profile.Encrypt(builder.SessionKey, 24, ApRequestBuilder.Confounder, [.. Message, .. header])];

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    [DataRow(11, DisplayName = "EC one short of the checksum")]
    [DataRow(13, DisplayName = "EC one past the checksum")]
    public void TryUnwrap_ExtraCountNotTheChecksumLength_IsFalse(int extraCount)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());
        byte[] token = client.WrapSigned(Message, 0x01020304);
        BinaryPrimitives.WriteUInt16BigEndian(token.AsSpan(4), (ushort)extraCount);

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    public void TryUnwrap_HeaderOnlyToken_IsFalse()
    {
        (KerberosSecurityContext context, _) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(InitiatorTokens.Header([0x05, 0x04], 0x00, 12, 0, 0x01020304), out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x04, 0x00 }, DisplayName = "shorter than a header")]
    [DataRow(new byte[] { 0x04, 0x04, 0x00, 0xFF, 0x00, 0x0C, 0x00, 0x00, 0, 0, 0, 0, 1, 2, 3, 4 }, DisplayName = "a MIC's TOK_ID")]
    [DataRow(new byte[] { 0x05, 0x04, 0x00, 0xFE, 0x00, 0x0C, 0x00, 0x00, 0, 0, 0, 0, 1, 2, 3, 4 }, DisplayName = "filler not 0xFF")]
    public void TryUnwrap_NotAWrapToken_IsFalse(byte[] token)
    {
        (KerberosSecurityContext context, _) = AcceptWithClient(new ApRequestBuilder());

        bool isIntact = context.TryUnwrap(token, out _);

        Assert.IsFalse(isIntact);
    }

    [TestMethod]
    public void VerifyMic_ClientMicUnderKeyUsage25_IsTrueAndCountsTheSequenceNumberUp()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool first = context.VerifyMic(Message, client.Mic(Message, 0x01020304));
        bool second = context.VerifyMic(Message, client.Mic(Message, 0x01020305));

        Assert.IsTrue(first);
        Assert.IsTrue(second);
    }

    [TestMethod]
    public void VerifyMic_MicUnderTheAcceptorsKeyUsage_IsFalse()
    {
        ApRequestBuilder builder = new();
        (KerberosSecurityContext context, _) = AcceptWithClient(builder);
        byte[] header = InitiatorTokens.Header([0x04, 0x04], 0x00, 0xFFFF, 0xFFFF, 0x01020304);
        byte[] token = [.. header, .. KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196).ComputeChecksum(builder.SessionKey, 23, [.. Message, .. header])];

        bool isValid = context.VerifyMic(Message, token);

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void VerifyMic_OtherMessage_IsFalse()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isValid = context.VerifyMic([.. Message, 0x21], client.Mic(Message, 0x01020304));

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    [DataRow(0x01, 0x01020304UL, DisplayName = "SentByAcceptor")]
    [DataRow(0x00, 0x01020303UL, DisplayName = "wrong sequence number")]
    public void VerifyMic_AcceptorFlaggedOrOutOfSequence_IsFalse(int flags, ulong sequenceNumber)
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());

        bool isValid = context.VerifyMic(Message, client.Mic(Message, sequenceNumber, (byte)flags));

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void VerifyMic_FillerNotAll0xFF_IsFalse()
    {
        (KerberosSecurityContext context, InitiatorTokens client) = AcceptWithClient(new ApRequestBuilder());
        byte[] token = client.Mic(Message, 0x01020304);
        token[7] = 0x00;

        bool isValid = context.VerifyMic(Message, token);

        Assert.IsFalse(isValid);
    }

    private static KerberosSecurityContext Accept(ApRequestBuilder builder) =>
        KerberosAcceptorTests.CreateAcceptor().Accept(builder.Build(), "HTTP").Context!;

    private static (KerberosSecurityContext Context, InitiatorTokens Client) AcceptWithClient(ApRequestBuilder builder) =>
        (Accept(builder), new InitiatorTokens(builder.TicketEncryptionType, builder.SessionKey));

    private static Asn1Tag ContextTag(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    private static byte[] ReadApplication0Content(byte[] token)
    {
        Assert.AreEqual(0x60, token[0]);
        AsnDecoder.ReadEncodedValue(token, AsnEncodingRules.DER, out int contentOffset, out int contentLength, out int consumed);
        Assert.AreEqual(token.Length, consumed);
        return token.AsSpan(contentOffset, contentLength).ToArray();
    }

    private static AsnReader ReadApplicationSequence(byte[] bytes, int number)
    {
        AsnReader outer = new(bytes, AsnEncodingRules.DER);
        AsnReader fields = outer.ReadSequence(new Asn1Tag(TagClass.Application, number, isConstructed: true)).ReadSequence();
        Assert.IsFalse(outer.HasData);
        return fields;
    }

    // AP-REP ::= [APPLICATION 15] SEQUENCE { pvno [0] 5, msg-type [1] 15, enc-part [2] EncryptedData }
    private static (int EncryptionTypeNumber, byte[] CipherText) ReadApRep(byte[] apRep)
    {
        AsnReader fields = ReadApplicationSequence(apRep, 15);
        Assert.IsTrue(fields.ReadSequence(ContextTag(0)).TryReadInt32(out int protocolVersion));
        Assert.AreEqual(5, protocolVersion);
        Assert.IsTrue(fields.ReadSequence(ContextTag(1)).TryReadInt32(out int messageType));
        Assert.AreEqual(15, messageType);
        AsnReader encryptedData = fields.ReadSequence(ContextTag(2)).ReadSequence();
        Assert.IsTrue(encryptedData.ReadSequence(ContextTag(0)).TryReadInt32(out int encryptionTypeNumber));
        byte[] cipherText = encryptedData.ReadSequence(ContextTag(2)).ReadOctetString();
        Assert.IsFalse(encryptedData.HasData);
        return (encryptionTypeNumber, cipherText);
    }
}
