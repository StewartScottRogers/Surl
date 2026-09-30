using System.Formats.Asn1;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Each way an <c>LDAPMessage</c> can be malformed is reported as its own outcome, with the
/// <c>messageID</c> whenever it could be read, so the server can answer <c>protocolError</c>.
/// </summary>
[TestClass]
public sealed class LdapMessageDecoderMalformedTests
{
    [TestMethod]
    public void Decode_IndefiniteLengthMessage_IsIndefiniteLengthWithNoMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.IndefiniteLength, null), Decode("308002010242000000"));
    }

    [TestMethod]
    public void Decode_IndefiniteLengthInsideTheMessage_IsIndefiniteLengthWithTheMessageId()
    {
        // messageID 2, then a BindRequest in the indefinite form.
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.IndefiniteLength, 2), Decode("300e020102608002010304008000 0000"));
    }

    [TestMethod]
    public void Decode_OperationLengthPastTheMessage_IsMalformedTagOrLengthWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.MalformedTagOrLength, 4), Decode("3006020104600502"));
    }

    [TestMethod]
    public void Decode_MessageLengthPastTheBytes_IsMalformedTagOrLengthWithNoMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.MalformedTagOrLength, null), Decode("30050201"));
    }

    [TestMethod]
    public void Decode_SetInPlaceOfTheMessageSequence_IsUnexpectedTagWithNoMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.UnexpectedTag, null), Decode("31050201034200"));
    }

    [TestMethod]
    public void Decode_OctetStringInPlaceOfTheMessageId_IsUnexpectedTagWithNoMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.UnexpectedTag, null), Decode("30050401034200"));
    }

    [TestMethod]
    public void Decode_BindWithAnIntegerInPlaceOfTheName_IsUnexpectedTagWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.UnexpectedTag, 1), Decode("300d020101600802010302010080 00"));
    }

    [TestMethod]
    public void Decode_MessageWithNoOperation_IsMissingElementWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.MissingElement, 9), Decode("3003020109"));
    }

    [TestMethod]
    public void Decode_BindWithNoAuthentication_IsMissingElementWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.MissingElement, 1), Decode("300a02010160050201030400"));
    }

    [TestMethod]
    public void Decode_BindWithAnElementAfterTheAuthentication_IsTrailingBytesWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.TrailingBytes, 1), Decode("300f020101600a020103040080000101ff"));
    }

    [TestMethod]
    public void Decode_ElementOtherThanControlsAfterTheOperation_IsTrailingBytesWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.TrailingBytes, 3), Decode("3007020103420004 00"));
    }

    [TestMethod]
    public void Decode_BytesAfterTheMessage_AreTrailingBytesWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.TrailingBytes, 3), Decode("30050201034200 00"));
    }

    [TestMethod]
    [DataRow(3, 0, DisplayName = "Scope 3")]
    [DataRow(-1, 0, DisplayName = "Scope -1")]
    [DataRow(2, 4, DisplayName = "derefAliases 4")]
    public void Decode_SearchWithAnEnumerationOutOfRange_IsEnumerationOutOfRangeWithTheMessageId(int scope, int derefAliases)
    {
        var result = LdapMessageDecoder.Decode(Message(5, Search(Present("cn"), scope: scope, derefAliases: derefAliases)), 32);

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.EnumerationOutOfRange, 5), result);
    }

    [TestMethod]
    public void Decode_EnumeratedWithNoContent_IsInvalidValue()
    {
        var result = LdapMessageDecoder.Decode(Message(5, writer =>
        {
            using var search = writer.PushSequence(LdapTags.SearchRequest);
            Text(writer, string.Empty);
            writer.WriteEncodedValue(Hex("0a00"));
        }), 32);

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 5), result);
    }

    [TestMethod]
    [DataRow("30050201ff4200", DisplayName = "Negative message ID")]
    [DataRow("30090205008000000042 00", DisplayName = "Message ID past 2147483647")]
    [DataRow("300402004200", DisplayName = "Integer with no content")]
    public void Decode_MessageIdNotAValidMessageId_IsInvalidValueWithNoMessageId(string hex)
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, null), Decode(hex));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Version 0")]
    [DataRow(128, DisplayName = "Version 128")]
    public void Decode_BindVersionOutOfRange_IsInvalidValueWithTheMessageId(int version)
    {
        var result = LdapMessageDecoder.Decode(Message(1, SimpleBind(version, string.Empty, string.Empty)), 32);

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 1), result);
    }

    [TestMethod]
    public void Decode_NameThatIsNotUtf8_IsInvalidValueWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 1), Decode("300d020101600802010304 01ff 8000"));
    }

    [TestMethod]
    public void Decode_ConstructedNameHoldingAnInteger_IsInvalidValueWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 1), Decode("300f020101600a020103240302010080 00"));
    }

    [TestMethod]
    public void Decode_UnbindWithContent_IsInvalidValueWithTheMessageId()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 3), Decode("3006020103420100"));
    }

    [TestMethod]
    public void Decode_BooleanOfTwoBytes_IsInvalidValueWithTheMessageId()
    {
        var result = LdapMessageDecoder.Decode(Message(5, writer =>
        {
            using var search = writer.PushSequence(LdapTags.SearchRequest);
            Text(writer, string.Empty);
            writer.WriteEnumeratedValue(LdapSearchScope.BaseObject);
            writer.WriteEnumeratedValue(LdapDerefAliases.NeverDerefAliases);
            writer.WriteInteger(0);
            writer.WriteInteger(0);
            writer.WriteEncodedValue(Hex("01020000"));
        }), 32);

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 5), result);
    }

    [TestMethod]
    public void Decode_PrimitiveInPlaceOfTheBindSequence_IsInvalidValueWithTheMessageId()
    {
        // [APPLICATION 0] sent primitive, where BindRequest is a SEQUENCE.
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 1), Decode("300502010140 00"));
    }

    private static LdapDecodeResult Decode(string hex) => LdapMessageDecoder.Decode(Hex(hex), 32);
}
