using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Parses DNs by RFC 4514 and compares them in ADR-0072 decision 1's normal form.
/// </summary>
[TestClass]
public sealed class LdapDistinguishedNameTests
{
    [TestMethod]
    [DataRow("CN=Alice,DC=Example,DC=COM")]
    [DataRow("cn=alice, dc=example, dc=com")]
    [DataRow("  cn = alice ,dc=example,  dc=com  ")]
    [DataRow("cn=  Alice  ,dc=example,dc=com")]
    [DataRow("cn=al\\69ce,dc=example,dc=com")]
    [DataRow("cn=ALI\\43E,dc=example,dc=com")]
    public void NormalForm_OfAnEqualDn_IsTheSame(string text)
    {
        Assert.AreEqual(Dn("cn=alice,dc=example,dc=com").NormalForm, Dn(text).NormalForm);
    }

    [TestMethod]
    public void NormalForm_OfAMultiValuedRdnInEitherOrder_IsTheSame()
    {
        Assert.AreEqual(Dn("cn=a+sn=b,o=x").NormalForm, Dn("SN=B + CN=A,o=x").NormalForm);
    }

    [TestMethod]
    public void NormalForm_OfDifferentValues_Differs()
    {
        Assert.AreNotEqual(Dn("cn=alice,dc=example").NormalForm, Dn("cn=alicia,dc=example").NormalForm);
    }

    [TestMethod]
    public void NormalForm_OfAnInnerRunOfSpaces_IsOneSpace()
    {
        Assert.AreEqual(Dn("cn=Carol Ann,o=x").NormalForm, Dn("cn=carol   ann,o=x").NormalForm);
    }

    [TestMethod]
    public void NormalForm_OfAnIntegerType_IsTheNumber()
    {
        Assert.AreEqual(Dn("uidNumber=0042,o=x").NormalForm, Dn("uidnumber=42,o=x").NormalForm);
    }

    [TestMethod]
    public void NormalForm_OfAnOctetStringType_KeepsCase()
    {
        Assert.AreNotEqual(Dn("userPassword=Secret,o=x").NormalForm, Dn("userPassword=secret,o=x").NormalForm);
    }

    [TestMethod]
    public void TryParse_EscapedSpecialCharacters_AreUnescaped()
    {
        var dn = Dn("cn=Smith\\, John\\+\\\"\\<\\>\\;\\\\\\=\\#\\ ,o=x");

        Assert.AreEqual("Smith, John+\"<>;\\=# ", System.Text.Encoding.UTF8.GetString(dn.RelativeDistinguishedNames[0][0].Value));
    }

    [TestMethod]
    public void TryParse_HexPairs_AreUtf8()
    {
        var dn = Dn("cn=caf\\C3\\A9,o=x");

        Assert.AreEqual("café", System.Text.Encoding.UTF8.GetString(dn.RelativeDistinguishedNames[0][0].Value));
        Assert.AreEqual(Dn("cn=CAFÉ,o=x").NormalForm, dn.NormalForm);
    }

    [TestMethod]
    public void TryParse_ACharacterOutsideTheBasicPlane_IsKept()
    {
        Assert.AreEqual("😀", System.Text.Encoding.UTF8.GetString(Dn("cn=😀").RelativeDistinguishedNames[0][0].Value));
    }

    [TestMethod]
    public void TryParse_AnEscapedTrailingSpace_IsKept()
    {
        Assert.AreEqual("a ", System.Text.Encoding.UTF8.GetString(Dn("userPassword=a\\  ").RelativeDistinguishedNames[0][0].Value));
    }

    [TestMethod]
    public void TryParse_ABerValue_IsReadAsBytesAndComparedAsBytes()
    {
        var dn = Dn("1.3.6.1.4.1.1466.0=#04024869 , o=x");

        Assert.IsTrue(dn.RelativeDistinguishedNames[0][0].IsBerEncoded);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x02, 0x48, 0x69 }, dn.RelativeDistinguishedNames[0][0].Value);
        Assert.AreEqual(Dn("1.3.6.1.4.1.1466.0=#04024869,o=x").NormalForm, dn.NormalForm);
        Assert.AreNotEqual(Dn("1.3.6.1.4.1.1466.0=#04024870,o=x").NormalForm, dn.NormalForm);
    }

    [TestMethod]
    public void TryParse_ALoneHighSurrogateAtTheEnd_IsReadAsTheReplacementCharacter()
    {
        Assert.AreEqual("�", System.Text.Encoding.UTF8.GetString(Dn("cn=\uD83D").RelativeDistinguishedNames[0][0].Value));
    }

    [TestMethod]
    public void TryParse_AnEmptyValue_IsAllowed()
    {
        Assert.IsEmpty(Dn("cn=,o=x").RelativeDistinguishedNames[0][0].Value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void TryParse_TheEmptyDn_IsTheRoot(string text)
    {
        Assert.IsTrue(Dn(text).IsRoot);
    }

    [TestMethod]
    [DataRow("cn")]
    [DataRow("cn=a,")]
    [DataRow("cn=a+")]
    [DataRow("=a")]
    [DataRow("1cn=a")]
    [DataRow("c.n=a")]
    [DataRow("1=a")]
    [DataRow("1..2=a")]
    [DataRow("01.2=a")]
    [DataRow("cn=a;o=b")]
    [DataRow("cn=a\"b")]
    [DataRow("cn=a<b")]
    [DataRow("cn=a>b")]
    [DataRow("cn=a\\")]
    [DataRow("cn=a\\4")]
    [DataRow("cn=a\\q")]
    [DataRow("cn=\\FF")]
    [DataRow("cn=#")]
    [DataRow("cn=#041")]
    [DataRow("cn=#0x")]
    [DataRow("cn=#04 x")]
    public void TryParse_TextThatIsNotADn_IsRefused(string text)
    {
        Assert.IsFalse(LdapDistinguishedName.TryParse(text, out _));
    }

    [TestMethod]
    public void TryParse_ANumericOidType_IsAccepted()
    {
        Assert.AreEqual(Dn("2.5.4.3=alice").NormalForm, Dn("2.5.4.3=ALICE").NormalForm);
    }

    [TestMethod]
    public void IsWithin_ByLevels_TellsChildrenFromDescendants()
    {
        var parent = Dn("dc=example,dc=com");

        Assert.IsTrue(Dn("cn=a,dc=example,dc=com").IsWithin(parent, levelsBelow: 1));
        Assert.IsFalse(Dn("cn=b,cn=a,dc=example,dc=com").IsWithin(parent, levelsBelow: 1));
        Assert.IsTrue(Dn("cn=b,cn=a,dc=example,dc=com").IsWithin(parent, levelsBelow: null));
        Assert.IsTrue(parent.IsWithin(parent, levelsBelow: null));
        Assert.IsFalse(Dn("dc=com").IsWithin(parent, levelsBelow: null));
        Assert.IsFalse(Dn("cn=a,dc=other,dc=com").IsWithin(parent, levelsBelow: null));
    }

    [TestMethod]
    public void SuperiorNormalForms_AreNearestFirst()
    {
        var superiors = Dn("cn=a,ou=b,dc=c").SuperiorNormalForms().ToArray();

        CollectionAssert.AreEqual(new[] { Dn("ou=b,dc=c").NormalForm, Dn("dc=c").NormalForm }, superiors);
    }

    [TestMethod]
    public void Text_IsTheDnAsWritten()
    {
        Assert.AreEqual("CN=Alice, DC=Example", Dn("CN=Alice, DC=Example").Text);
    }
}
