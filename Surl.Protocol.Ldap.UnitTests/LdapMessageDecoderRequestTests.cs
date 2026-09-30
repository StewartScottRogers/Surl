using System.Formats.Asn1;
using System.Text;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapMessageDecoderRequestTests
{
    [TestMethod]
    public void Decode_AnonymousSimpleBind_IsAVersion3BindWithAnEmptyNameAndPassword()
    {
        // messageID 1, BindRequest { version 3, name "", simple "" }.
        var message = Decoded(Hex("300c020101600702010304008000"));

        Assert.AreEqual(1, message.MessageId);
        var bind = (LdapBindRequest)message.Operation;
        Assert.AreEqual(3, bind.Version);
        Assert.AreEqual(string.Empty, bind.Name);
        Assert.IsEmpty(((LdapSimpleAuthentication)bind.Authentication).Password);
        Assert.IsEmpty(message.Controls);
    }

    [TestMethod]
    public void Decode_SimpleBindWithNameAndPassword_CarriesBoth()
    {
        var bind = (LdapBindRequest)Decoded(Message(7, SimpleBind(3, "cn=Babs Jensen,o=University of Michigan,c=US", "secret"))).Operation;

        Assert.AreEqual("cn=Babs Jensen,o=University of Michigan,c=US", bind.Name);
        Assert.AreEqual("secret", Encoding.UTF8.GetString(((LdapSimpleAuthentication)bind.Authentication).Password));
    }

    [TestMethod]
    public void Decode_SaslBindWithCredentials_CarriesTheMechanismAndCredentials()
    {
        var bind = (LdapBindRequest)Decoded(Message(2, writer =>
        {
            using var request = writer.PushSequence(LdapTags.BindRequest);
            writer.WriteInteger(3);
            Text(writer, string.Empty);
            using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
            Text(writer, "PLAIN");
            writer.WriteOctetString([0, 0x75, 0, 0x70]);
        })).Operation;

        var sasl = (LdapSaslAuthentication)bind.Authentication;
        Assert.AreEqual("PLAIN", sasl.Mechanism);
        CollectionAssert.AreEqual(new byte[] { 0, 0x75, 0, 0x70 }, sasl.Credentials);
    }

    [TestMethod]
    public void Decode_SaslBindWithoutCredentials_HasNullCredentials()
    {
        var bind = (LdapBindRequest)Decoded(Message(2, writer =>
        {
            using var request = writer.PushSequence(LdapTags.BindRequest);
            writer.WriteInteger(3);
            Text(writer, string.Empty);
            using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
            Text(writer, "EXTERNAL");
        })).Operation;

        Assert.AreEqual(new LdapSaslAuthentication("EXTERNAL", null), bind.Authentication);
    }

    [TestMethod]
    public void Decode_BindWithAReservedAuthenticationChoice_ReportsItsTag()
    {
        // [1], one of the two tags RFC 4511 reserves in AuthenticationChoice.
        var bind = (LdapBindRequest)Decoded(Hex("300c020103600702010304008100")).Operation;

        Assert.AreEqual(new LdapUnsupportedAuthentication(LdapTags.Context(1)), bind.Authentication);
    }

    [TestMethod]
    public void Decode_UnbindRequest_IsAnUnbind()
    {
        var message = Decoded(Hex("30050201034200"));

        Assert.AreEqual(3, message.MessageId);
        Assert.IsInstanceOfType<LdapUnbindRequest>(message.Operation);
    }

    [TestMethod]
    public void Decode_AbandonRequest_CarriesTheMessageIdToAbandon()
    {
        var message = Decoded(Hex("3006020105500103"));

        Assert.AreEqual(new LdapAbandonRequest(3), message.Operation);
    }

    [TestMethod]
    public void Decode_StartTlsExtendedRequest_HasTheNameAndNoValue()
    {
        var message = Decoded(Message(1, writer =>
        {
            using var request = writer.PushSequence(LdapTags.ExtendedRequest);
            Text(writer, "1.3.6.1.4.1.1466.20037", LdapTags.Context(0));
        }));

        Assert.AreEqual(new LdapExtendedRequest("1.3.6.1.4.1.1466.20037", null), message.Operation);
    }

    [TestMethod]
    public void Decode_ExtendedRequestWithAValue_CarriesTheValue()
    {
        var extended = (LdapExtendedRequest)Decoded(Message(1, writer =>
        {
            using var request = writer.PushSequence(LdapTags.ExtendedRequest);
            Text(writer, "1.3.6.1.4.1.4203.1.11.1", LdapTags.Context(0));
            writer.WriteOctetString([0x30, 0x00], LdapTags.Context(1));
        })).Operation;

        Assert.AreEqual("1.3.6.1.4.1.4203.1.11.1", extended.RequestName);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x00 }, extended.RequestValue);
    }

    [TestMethod]
    public void Decode_DelRequest_IsReportedByItsTag()
    {
        // DelRequest ::= [APPLICATION 10] LDAPDN, which Surl does not decode.
        var message = Decoded(Hex("300902010a4a04636e3d78"));

        Assert.AreEqual(new LdapUnrecognizedOperation(new Asn1Tag(TagClass.Application, 10)), message.Operation);
    }

    [TestMethod]
    public void Decode_UniversalTagInPlaceOfAnOperation_IsReportedByItsTag()
    {
        var message = Decoded(Hex("300602010a040100"));

        Assert.AreEqual(new LdapUnrecognizedOperation(Asn1Tag.PrimitiveOctetString), message.Operation);
    }

    [TestMethod]
    public void Decode_SearchRequest_CarriesEveryField()
    {
        var search = (LdapSearchRequest)Decoded(Message(2, Search(
            Equality("cn", "Babs Jensen"),
            baseObject: "o=University of Michigan,c=US",
            scope: 1,
            derefAliases: 3,
            sizeLimit: 10,
            timeLimit: 30,
            typesOnly: true,
            "cn",
            "mail"))).Operation;

        Assert.AreEqual("o=University of Michigan,c=US", search.BaseObject);
        Assert.AreEqual(LdapSearchScope.SingleLevel, search.Scope);
        Assert.AreEqual(LdapDerefAliases.DerefAlways, search.DerefAliases);
        Assert.AreEqual(10, search.SizeLimit);
        Assert.AreEqual(30, search.TimeLimit);
        Assert.IsTrue(search.TypesOnly);
        CollectionAssert.AreEqual(new[] { "cn", "mail" }, search.Attributes.ToArray());
        Assert.IsInstanceOfType<LdapComparisonFilter>(search.Filter);
    }

    [TestMethod]
    public void Decode_SearchWithNoAttributeSelection_HasNoAttributes()
    {
        var search = (LdapSearchRequest)Decoded(Message(2, Search(Present("objectClass"), scope: 0))).Operation;

        Assert.AreEqual(LdapSearchScope.BaseObject, search.Scope);
        Assert.AreEqual(LdapDerefAliases.NeverDerefAliases, search.DerefAliases);
        Assert.IsFalse(search.TypesOnly);
        Assert.IsEmpty(search.Attributes);
    }

    [TestMethod]
    public void Decode_ControlsWithAndWithoutCriticalityAndValue_AreEachRead()
    {
        var message = Decoded(Message(4, Search(Present("cn")), writer =>
        {
            using var controls = writer.PushSequence(LdapTags.Controls);
            using (writer.PushSequence())
            {
                Text(writer, "1.2.840.113556.1.4.319");
                writer.WriteBoolean(true);
                writer.WriteOctetString([0x30, 0x05, 0x02, 0x01, 0x0a, 0x04, 0x00]);
            }

            using (writer.PushSequence())
            {
                Text(writer, "2.16.840.1.113730.3.4.2");
            }
        }));

        Assert.HasCount(2, message.Controls);
        Assert.AreEqual("1.2.840.113556.1.4.319", message.Controls[0].ControlType);
        Assert.IsTrue(message.Controls[0].Criticality);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x05, 0x02, 0x01, 0x0a, 0x04, 0x00 }, message.Controls[0].ControlValue);
        Assert.AreEqual(new LdapControl("2.16.840.1.113730.3.4.2", false, null), message.Controls[1]);
    }

    [TestMethod]
    public void Decode_EmptyControls_HasNoControls()
    {
        // An UnbindRequest followed by controls [0] holding no Control.
        var message = Decoded(Hex("30070201034200a000"));

        Assert.IsEmpty(message.Controls);
    }

    [TestMethod]
    public void Decode_MaxFilterDepthBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LdapMessageDecoder.Decode(Hex("30050201034200"), 0));
    }

    private static LdapMessage Decoded(byte[] bytes)
    {
        var result = LdapMessageDecoder.Decode(bytes, 32);
        Assert.AreEqual(LdapDecodeOutcome.Decoded, result.Outcome);
        Assert.AreEqual(result.Message!.MessageId, result.MessageId);

        return result.Message;
    }
}
