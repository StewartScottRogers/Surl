using System.Formats.Asn1;
using System.Text;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decodes the BER of every <c>Filter</c> choice, built from RFC 4515's examples, and checks the
/// result by writing it back in RFC 4515's string form.
/// </summary>
[TestClass]
public sealed class LdapFilterDecoderTests
{
    [TestMethod]
    public void Decode_EqualityMatch_IsRead()
    {
        Assert.AreEqual("(cn=Babs Jensen)", FilterText(Equality("cn", "Babs Jensen")));
    }

    [TestMethod]
    public void Decode_Not_IsRead()
    {
        Assert.AreEqual("(!(cn=Tim Howes))", FilterText(Not(Equality("cn", "Tim Howes"))));
    }

    [TestMethod]
    public void Decode_AndOfEqualityAndOrWithInitialSubstring_IsRead()
    {
        var filter = And(
            Equality("objectClass", "Person"),
            Or(Equality("sn", "Jensen"), Substrings("cn", (0, "Babs J"))));

        Assert.AreEqual("(&(objectClass=Person)(|(sn=Jensen)(cn=Babs J*)))", FilterText(filter));
    }

    [TestMethod]
    public void Decode_SubstringsWithInitialAndTwoAny_IsRead()
    {
        Assert.AreEqual("(o=univ*of*mich*)", FilterText(Substrings("o", (0, "univ"), (1, "of"), (1, "mich"))));
    }

    [TestMethod]
    public void Decode_SubstringsWithFinalOnly_IsRead()
    {
        Assert.AreEqual("(cn=*Jensen)", FilterText(Substrings("cn", (2, "Jensen"))));
    }

    [TestMethod]
    public void Decode_SubstringsWithEveryPart_IsRead()
    {
        Assert.AreEqual("(cn=B*b*s)", FilterText(Substrings("cn", (0, "B"), (1, "b"), (2, "s"))));
    }

    [TestMethod]
    public void Decode_EqualityWithAnEmptyValue_IsRead()
    {
        Assert.AreEqual("(seeAlso=)", FilterText(Equality("seeAlso", string.Empty)));
    }

    [TestMethod]
    [DataRow(5, "(sn>=Jensen)")]
    [DataRow(6, "(sn<=Jensen)")]
    [DataRow(8, "(sn~=Jensen)")]
    public void Decode_OrderingAndApproxMatch_AreRead(int tagNumber, string expected)
    {
        Assert.AreEqual(expected, FilterText(Comparison(tagNumber, "sn", "Jensen")));
    }

    [TestMethod]
    public void Decode_Present_IsRead()
    {
        Assert.AreEqual("(objectClass=*)", FilterText(Present("objectClass")));
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithTypeAndRule_IsRead()
    {
        Assert.AreEqual("(cn:caseExactMatch:=Fred Flintstone)", FilterText(Extensible("caseExactMatch", "cn", "Fred Flintstone", null)));
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithTypeOnly_IsRead()
    {
        Assert.AreEqual("(cn:=Betty Rubble)", FilterText(Extensible(null, "cn", "Betty Rubble", null)));
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithTypeDnAndRule_IsRead()
    {
        Assert.AreEqual("(sn:dn:2.4.6.8.10:=Barney Rubble)", FilterText(Extensible("2.4.6.8.10", "sn", "Barney Rubble", true)));
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithRuleOnly_IsRead()
    {
        Assert.AreEqual("(:1.2.3:=Wilma Flintstone)", FilterText(Extensible("1.2.3", null, "Wilma Flintstone", false)));
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithDnAndRule_IsRead()
    {
        Assert.AreEqual("(:dn:2.4.6.8.10:=Dino)", FilterText(Extensible("2.4.6.8.10", null, "Dino", true)));
    }

    [TestMethod]
    public void Decode_EmptyAndAndOr_AreTheAbsoluteTrueAndFalseFilters()
    {
        Assert.AreEqual("(&)", FilterText(And()));
        Assert.AreEqual("(|)", FilterText(Or()));
    }

    [TestMethod]
    public void Decode_FilterNestedExactlyToTheBound_IsRead()
    {
        Assert.AreEqual("(!(!(cn=x)))", FilterText(Not(Not(Equality("cn", "x"))), maxFilterDepth: 3));
    }

    [TestMethod]
    [DataRow(3, DisplayName = "Three nots around an equality is depth 4")]
    [DataRow(1, DisplayName = "An and around an equality is depth 2")]
    public void Decode_FilterNestedPastTheBound_IsFilterTooDeepWithTheMessageId(int maxFilterDepth)
    {
        var filter = maxFilterDepth == 3 ? Not(Not(Not(Equality("cn", "x")))) : And(Equality("cn", "x"));

        var result = LdapMessageDecoder.Decode(Message(6, Search(filter)), maxFilterDepth);

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.FilterTooDeep, 6), result);
    }

    [TestMethod]
    public void Decode_DeepNestingPastTheBound_IsRefusedWithoutRecursingThroughIt()
    {
        Action<AsnWriter> filter = Equality("cn", "x");
        for (var level = 0; level < 500; level++)
        {
            filter = Not(filter);
        }

        var result = LdapMessageDecoder.Decode(Message(6, Search(filter)), 32);

        Assert.AreEqual(LdapDecodeOutcome.FilterTooDeep, result.Outcome);
    }

    [TestMethod]
    [DataRow("0400", DisplayName = "A universal OCTET STRING")]
    [DataRow("8a00", DisplayName = "Context tag 10")]
    public void Decode_FilterWithATagNoChoiceHas_IsUnexpectedTag(string hex)
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.UnexpectedTag, 6), DecodeSearch(Raw(hex)));
    }

    [TestMethod]
    public void Decode_SubstringsWithNoParts_IsInvalidValue()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 6), DecodeSearch(Substrings("cn")));
    }

    [TestMethod]
    [DataRow(1, 0, DisplayName = "An initial after an any")]
    [DataRow(2, 2, DisplayName = "Two finals")]
    [DataRow(0, 0, DisplayName = "Two initials")]
    public void Decode_SubstringsOutOfOrder_IsUnexpectedTag(int firstTag, int secondTag)
    {
        var result = DecodeSearch(Substrings("cn", (firstTag, "a"), (secondTag, "b")));

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.UnexpectedTag, 6), result);
    }

    [TestMethod]
    public void Decode_ExtensibleMatchWithNeitherRuleNorType_IsInvalidValue()
    {
        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.InvalidValue, 6), DecodeSearch(Extensible(null, null, "x", null)));
    }

    [TestMethod]
    public void Decode_NotHoldingTwoFilters_IsTrailingBytes()
    {
        var filter = Not(writer =>
        {
            Equality("cn", "x")(writer);
            Equality("cn", "y")(writer);
        });

        Assert.AreEqual(LdapDecodeResult.Malformed(LdapDecodeOutcome.TrailingBytes, 6), DecodeSearch(filter));
    }

    private static LdapDecodeResult DecodeSearch(Action<AsnWriter> filter) => LdapMessageDecoder.Decode(Message(6, Search(filter)), 32);

    private static string FilterText(Action<AsnWriter> filter, int maxFilterDepth = 32)
    {
        var result = LdapMessageDecoder.Decode(Message(6, Search(filter)), maxFilterDepth);
        Assert.AreEqual(LdapDecodeOutcome.Decoded, result.Outcome);

        return Describe(((LdapSearchRequest)result.Message!.Operation).Filter);
    }

    // RFC 4515's string form of a decoded filter, with no escaping: the tests use none.
    private static string Describe(LdapFilter filter) => filter switch
    {
        LdapAndFilter and => $"(&{string.Concat(and.Filters.Select(Describe))})",
        LdapOrFilter or => $"(|{string.Concat(or.Filters.Select(Describe))})",
        LdapNotFilter not => $"(!{Describe(not.Filter)})",
        LdapComparisonFilter comparison => $"({comparison.AttributeDescription}{Operator(comparison.Comparison)}{Utf8(comparison.AssertionValue)})",
        LdapSubstringsFilter substrings =>
            $"({substrings.AttributeDescription}={Utf8(substrings.Initial)}*{string.Concat(substrings.Any.Select(any => Utf8(any) + "*"))}{Utf8(substrings.Final)})",
        LdapPresentFilter present => $"({present.AttributeDescription}=*)",
        LdapExtensibleMatchFilter extensible =>
            $"({extensible.Type}{(extensible.DnAttributes ? ":dn" : string.Empty)}{(extensible.MatchingRule is null ? string.Empty : ":" + extensible.MatchingRule)}:={Utf8(extensible.MatchValue)})",
        _ => throw new AssertFailedException($"Unexpected filter {filter}."),
    };

    private static string Operator(LdapComparison comparison) => comparison switch
    {
        LdapComparison.EqualityMatch => "=",
        LdapComparison.GreaterOrEqual => ">=",
        LdapComparison.LessOrEqual => "<=",
        _ => "~=",
    };

    private static string Utf8(byte[]? bytes) => bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
}
