using System.Formats.Asn1;
using System.Text;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Every response the encoder writes is read back with <see cref="AsnReader"/> under BER rules,
/// field by field against RFC 4511 appendix B.
/// </summary>
[TestClass]
public sealed class LdapMessageEncoderTests
{
    [TestMethod]
    public void EncodeBindResponse_Success_IsTheRfc4511Encoding()
    {
        var bytes = LdapMessageEncoder.EncodeBindResponse(1, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty), null);

        CollectionAssert.AreEqual(Hex("300c02010161070a010004000400"), bytes);
    }

    [TestMethod]
    public void EncodeBindResponse_WithServerSaslCredentials_CarriesThemAfterTheResult()
    {
        var bytes = LdapMessageEncoder.EncodeBindResponse(
            3,
            new LdapResult(LdapResultCode.SaslBindInProgress, string.Empty, "continue"),
            [1, 2, 3]);

        var response = OpenResponse(bytes, 3, LdapTags.BindResponse);
        AssertResult(response, LdapResultCode.SaslBindInProgress, string.Empty, "continue");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, response.ReadOctetString(LdapTags.ServerSaslCredentials));
        Assert.IsFalse(response.HasData);
    }

    [TestMethod]
    public void EncodeSearchResultEntry_WritesTheDnAndEachAttributeWithItsValuesInOrder()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultEntry(2, "cn=Babs Jensen,o=University of Michigan,c=US",
        [
            new LdapPartialAttribute("mail", [Encoding.UTF8.GetBytes("babs@example.com"), Encoding.UTF8.GetBytes("bjensen@example.com")]),
            new LdapPartialAttribute("cn", []),
        ]);

        var entry = OpenResponse(bytes, 2, LdapTags.SearchResultEntry);
        Assert.AreEqual("cn=Babs Jensen,o=University of Michigan,c=US", ReadText(entry));
        var attributes = entry.ReadSequence();
        Assert.IsFalse(entry.HasData);

        var mail = attributes.ReadSequence();
        Assert.AreEqual("mail", ReadText(mail));
        var mailValues = mail.ReadSetOf();
        Assert.AreEqual("babs@example.com", ReadText(mailValues));
        Assert.AreEqual("bjensen@example.com", ReadText(mailValues));
        Assert.IsFalse(mailValues.HasData);

        var cn = attributes.ReadSequence();
        Assert.AreEqual("cn", ReadText(cn));
        Assert.IsFalse(cn.ReadSetOf().HasData);
        Assert.IsFalse(attributes.HasData);
    }

    [TestMethod]
    public void EncodeSearchResultDone_Success_IsTheRfc4511Encoding()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultDone(2, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty));

        CollectionAssert.AreEqual(Hex("300c02010265070a010004000400"), bytes);
    }

    [TestMethod]
    public void EncodeSearchResultDone_WithAReferral_WritesTheUrisAfterTheDiagnosticMessage()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultDone(
            4,
            new LdapResult(LdapResultCode.Referral, "o=Example", "see elsewhere", ["ldap://a.example.com/", "ldap://b.example.com/"]));

        var done = OpenResponse(bytes, 4, LdapTags.SearchResultDone);
        AssertResult(done, LdapResultCode.Referral, "o=Example", "see elsewhere");
        var referral = done.ReadSequence(LdapTags.Referral);
        Assert.AreEqual("ldap://a.example.com/", ReadText(referral));
        Assert.AreEqual("ldap://b.example.com/", ReadText(referral));
        Assert.IsFalse(referral.HasData);
        Assert.IsFalse(done.HasData);
    }

    [TestMethod]
    public void EncodeSearchResultDone_WithAnEmptyReferral_LeavesTheFieldOut()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultDone(2, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty, []));

        CollectionAssert.AreEqual(Hex("300c02010265070a010004000400"), bytes);
    }

    [TestMethod]
    public void EncodeSearchResultReference_WritesEachUri()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultReference(5, ["ldap://hostb/OU=People,DC=Example,DC=NET??sub"]);

        var reference = OpenResponse(bytes, 5, LdapTags.SearchResultReference);
        Assert.AreEqual("ldap://hostb/OU=People,DC=Example,DC=NET??sub", ReadText(reference));
        Assert.IsFalse(reference.HasData);
    }

    [TestMethod]
    public void EncodeSearchResultReference_NoUris_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LdapMessageEncoder.EncodeSearchResultReference(5, []));
    }

    [TestMethod]
    public void EncodeExtendedResponse_WithNameAndValue_WritesBothAfterTheResult()
    {
        var bytes = LdapMessageEncoder.EncodeExtendedResponse(
            6,
            new LdapResult(LdapResultCode.Success, string.Empty, string.Empty),
            "1.3.6.1.4.1.4203.1.11.3",
            [0x04, 0x00]);

        var response = OpenResponse(bytes, 6, LdapTags.ExtendedResponse);
        AssertResult(response, LdapResultCode.Success, string.Empty, string.Empty);
        Assert.AreEqual("1.3.6.1.4.1.4203.1.11.3", Encoding.UTF8.GetString(response.ReadOctetString(LdapTags.ResponseName)));
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x00 }, response.ReadOctetString(LdapTags.ResponseValue));
        Assert.IsFalse(response.HasData);
    }

    [TestMethod]
    public void EncodeExtendedResponse_WithNeitherNameNorValue_WritesTheResultAlone()
    {
        var bytes = LdapMessageEncoder.EncodeExtendedResponse(6, new LdapResult(LdapResultCode.UnwillingToPerform, string.Empty, "no"), null, null);

        var response = OpenResponse(bytes, 6, LdapTags.ExtendedResponse);
        AssertResult(response, LdapResultCode.UnwillingToPerform, string.Empty, "no");
        Assert.IsFalse(response.HasData);
    }

    [TestMethod]
    public void EncodeNoticeOfDisconnection_IsAnExtendedResponseWithMessageIdZeroAndTheNoticeName()
    {
        var bytes = LdapMessageEncoder.EncodeNoticeOfDisconnection(new LdapResult(LdapResultCode.ProtocolError, string.Empty, "malformed request"));

        var notice = OpenResponse(bytes, 0, LdapTags.ExtendedResponse);
        AssertResult(notice, LdapResultCode.ProtocolError, string.Empty, "malformed request");
        Assert.AreEqual("1.3.6.1.4.1.1466.20036", Encoding.UTF8.GetString(notice.ReadOctetString(LdapTags.ResponseName)));
        Assert.IsFalse(notice.HasData);
    }

    [TestMethod]
    public async Task EncodeSearchResultDone_ReadsBackThroughTheFrameReaderAsOneWholeMessage()
    {
        var bytes = LdapMessageEncoder.EncodeSearchResultDone(2, new LdapResult(LdapResultCode.NoSuchObject, "dc=example,dc=com", "no such entry"));
        var connection = new Surl.Protocol.Abstractions.InMemoryConnection([bytes]);

        var frame = await new LdapMessageFrameReader(connection, 0).ReadFrameAsync(CancellationToken.None);

        CollectionAssert.AreEqual(bytes, frame.Message);
    }

    // Reads the LDAPMessage envelope, checks its messageID and returns a reader over the response.
    private static AsnReader OpenResponse(byte[] bytes, int messageId, Asn1Tag responseTag)
    {
        var outer = new AsnReader(bytes, AsnEncodingRules.BER);
        var message = outer.ReadSequence();
        Assert.IsFalse(outer.HasData);
        Assert.IsTrue(message.TryReadInt32(out var readMessageId));
        Assert.AreEqual(messageId, readMessageId);
        var response = message.ReadSequence(responseTag);
        Assert.IsFalse(message.HasData);

        return response;
    }

    private static void AssertResult(AsnReader response, LdapResultCode resultCode, string matchedDn, string diagnosticMessage)
    {
        Assert.AreEqual(resultCode, response.ReadEnumeratedValue<LdapResultCode>());
        Assert.AreEqual(matchedDn, ReadText(response));
        Assert.AreEqual(diagnosticMessage, ReadText(response));
    }

    private static string ReadText(AsnReader reader) => Encoding.UTF8.GetString(reader.ReadOctetString());
}
