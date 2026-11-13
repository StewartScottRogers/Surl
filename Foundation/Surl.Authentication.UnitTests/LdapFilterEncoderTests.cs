namespace Surl.Authentication;

/// <summary>
/// <see cref="LdapFilterEncoder"/> against the BER RFC 4511 section 4.5.1.7 gives each filter form,
/// written out by hand.
/// </summary>
[TestClass]
public sealed class LdapFilterEncoderTests
{
    [TestMethod]
    [DataRow("(a=b)", "A3 06 04 01 61 04 01 62", DisplayName = "equality")]
    [DataRow("(cn=*)", "87 02 63 6E", DisplayName = "presence")]
    [DataRow("(a>=b)", "A5 06 04 01 61 04 01 62", DisplayName = "greater or equal")]
    [DataRow("(a<=b)", "A6 06 04 01 61 04 01 62", DisplayName = "less or equal")]
    [DataRow("(a~=b)", "A8 06 04 01 61 04 01 62", DisplayName = "approximate")]
    [DataRow("(!(a=b))", "A2 08 A3 06 04 01 61 04 01 62", DisplayName = "not")]
    [DataRow("(&(a=b)(c=d))", "A0 10 A3 06 04 01 61 04 01 62 A3 06 04 01 63 04 01 64", DisplayName = "and")]
    [DataRow("(|(a=b))", "A1 08 A3 06 04 01 61 04 01 62", DisplayName = "or")]
    [DataRow("(cn=a*b*c)", "A4 0F 04 02 63 6E 30 09 80 01 61 81 01 62 82 01 63", DisplayName = "substrings")]
    [DataRow("(cn=*b)", "A4 09 04 02 63 6E 30 03 82 01 62", DisplayName = "final only")]
    [DataRow("(cn=*b*)", "A4 09 04 02 63 6E 30 03 81 01 62", DisplayName = "any only")]
    [DataRow("(a=\\2a)", "A3 06 04 01 61 04 01 2A", DisplayName = "escaped star")]
    [DataRow("(a=ü)", "A3 07 04 01 61 04 02 C3 BC", DisplayName = "non-ASCII as UTF-8")]
    [DataRow("(cn;lang-en=x)", "A3 0F 04 0A 63 6E 3B 6C 61 6E 67 2D 65 6E 04 01 78", DisplayName = "attribute options")]
    [DataRow("(memberOf:dn:1.2.3:=x)", "A9 17 81 05 31 2E 32 2E 33 82 08 6D 65 6D 62 65 72 4F 66 83 01 78 84 01 FF", DisplayName = "extensible with dn and rule")]
    [DataRow("(:1.2.3:=x)", "A9 0A 81 05 31 2E 32 2E 33 83 01 78", DisplayName = "extensible rule only")]
    [DataRow("(a:dn:dn:=x)", "A9 0D 81 02 64 6E 82 01 61 83 01 78 84 01 FF", DisplayName = "a rule named dn after dn")]
    [DataRow("(a:=x)", "A9 06 82 01 61 83 01 78", DisplayName = "extensible attribute only")]
    public void TryEncode_WellFormedFilter_IsTheBerOfRfc4511(string filter, string expectedHex)
    {
        var encoded = LdapFilterEncoder.TryEncode(filter);

        Assert.IsNotNull(encoded);
        Assert.AreEqual(expectedHex.Replace(" ", string.Empty, StringComparison.Ordinal), Convert.ToHexString(encoded));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("(", DisplayName = "an open parenthesis")]
    [DataRow("a=b", DisplayName = "no parentheses")]
    [DataRow("(a=b", DisplayName = "not closed")]
    [DataRow("(a=b))", DisplayName = "extra close")]
    [DataRow("(a=b)(c=d)", DisplayName = "two filters")]
    [DataRow("(&)", DisplayName = "empty and")]
    [DataRow("(&(a=b)", DisplayName = "unclosed and")]
    [DataRow("(&(a=b)x)", DisplayName = "and holding a non-filter")]
    [DataRow("(&(a))", DisplayName = "and holding a bad filter")]
    [DataRow("(!)", DisplayName = "empty not")]
    [DataRow("(!(a=b)(c=d))", DisplayName = "not of two")]
    [DataRow("(=b)", DisplayName = "no attribute")]
    [DataRow("(a)", DisplayName = "no operator")]
    [DataRow("(a b=c)", DisplayName = "bad attribute")]
    [DataRow("(~=a)", DisplayName = "no attribute before ~=")]
    [DataRow("(a=\\)", DisplayName = "a lone backslash")]
    [DataRow("(a=\\4)", DisplayName = "a short escape")]
    [DataRow("(a=\\zz)", DisplayName = "a non-hex escape")]
    [DataRow("(a>=\\q1)", DisplayName = "a bad escape after >=")]
    [DataRow("(a=b**c)", DisplayName = "adjacent stars")]
    [DataRow("(a=b*\\)", DisplayName = "a bad escape in a substring")]
    [DataRow("(:dn:=x)", DisplayName = "extensible with no attribute or rule")]
    [DataRow("(a:r:s:=x)", DisplayName = "two rules")]
    [DataRow("(a:b c:=x)", DisplayName = "a bad rule")]
    [DataRow("(a b:r:=x)", DisplayName = "a bad extensible attribute")]
    [DataRow("(a:r:=\\)", DisplayName = "a bad extensible value")]
    public void TryEncode_MalformedFilter_IsNull(string filter)
    {
        Assert.IsNull(LdapFilterEncoder.TryEncode(filter));
    }

    [TestMethod]
    public void TryEncode_SixteenLevelsOfNesting_IsTheLimit()
    {
        var within = string.Concat(Enumerable.Repeat("(!", 15)) + "(a=b)" + new string(')', 15);
        var beyond = string.Concat(Enumerable.Repeat("(!", 16)) + "(a=b)" + new string(')', 16);

        Assert.IsNotNull(LdapFilterEncoder.TryEncode(within));
        Assert.IsNull(LdapFilterEncoder.TryEncode(beyond));
    }
}
