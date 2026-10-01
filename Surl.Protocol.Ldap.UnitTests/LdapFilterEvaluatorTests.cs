using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Evaluates every <c>Filter</c> choice against one entry, three-valued, by ADR-0072 decision 1's
/// matching rules.
/// </summary>
[TestClass]
public sealed class LdapFilterEvaluatorTests
{
    private static readonly LdapEntry Alice = Entry(
        "cn=Alice,ou=People,dc=example,dc=com",
        ("objectClass", "top"),
        ("objectClass", "person"),
        ("cn", "Alice  Smith"),
        ("cn;lang-fr", "Alicia"),
        ("sn", "Smith"),
        ("uidNumber", "1000"),
        ("gidNumber", "not a number"),
        ("userPassword", "Secret"),
        ("description", "café"));

    [TestMethod]
    [DataRow("objectClass", "True")]
    [DataRow("OBJECTCLASS", "True")]
    [DataRow("cn;lang-fr", "True")]
    [DataRow("sn;lang-fr", "False")]
    [DataRow("mail", "False")]
    public void Present_IsTrueOnlyForAnAttributeTheEntryHas(string description, string expected)
    {
        Assert.AreEqual(expected, EvaluateText(new LdapPresentFilter(description)));
    }

    [TestMethod]
    [DataRow("cn", "alice smith", "True")]
    [DataRow("cn", "  ALICE   SMITH ", "True")]
    [DataRow("cn", "alicia", "True")]
    [DataRow("cn;lang-fr", "alice smith", "False")]
    [DataRow("objectClass", "PERSON", "True")]
    [DataRow("description", "CAFÉ", "True")]
    [DataRow("sn", "Jones", "False")]
    [DataRow("mail", "alice@example.com", "False")]
    [DataRow("uidNumber", "01000", "True")]
    [DataRow("uidNumber", "1001", "False")]
    [DataRow("uidNumber", "ten", "Undefined")]
    [DataRow("gidNumber", "1", "Undefined")]
    [DataRow("userPassword", "Secret", "True")]
    [DataRow("userPassword", "secret", "False")]
    public void EqualityMatch_UsesTheTypesRule(string description, string value, string expected)
    {
        Assert.AreEqual(expected, EvaluateText(new LdapComparisonFilter(LdapComparison.EqualityMatch, description, Utf8(value))));
    }

    [TestMethod]
    [DataRow("cn", "ALICE SMITH", "True")]
    [DataRow("sn", "Smyth", "False")]
    public void ApproxMatch_IsEquality(string description, string value, string expected)
    {
        Assert.AreEqual(expected, EvaluateText(new LdapComparisonFilter(LdapComparison.ApproxMatch, description, Utf8(value))));
    }

    [TestMethod]
    [DataRow("GreaterOrEqual", "sn", "smith", "True")]
    [DataRow("GreaterOrEqual", "sn", "K", "True")]
    [DataRow("GreaterOrEqual", "sn", "T", "False")]
    [DataRow("LessOrEqual", "sn", "T", "True")]
    [DataRow("LessOrEqual", "sn", "K", "False")]
    [DataRow("GreaterOrEqual", "uidNumber", "999", "True")]
    [DataRow("GreaterOrEqual", "uidNumber", "-5", "True")]
    [DataRow("LessOrEqual", "uidNumber", "999", "False")]
    [DataRow("LessOrEqual", "uidNumber", "x", "Undefined")]
    [DataRow("GreaterOrEqual", "userPassword", "S", "True")]
    [DataRow("LessOrEqual", "userPassword", "S", "False")]
    [DataRow("GreaterOrEqual", "mail", "a", "False")]
    public void Ordering_UsesTheTypesOrderingRule(string comparison, string description, string value, string expected)
    {
        Assert.AreEqual(expected, EvaluateText(new LdapComparisonFilter(Enum.Parse<LdapComparison>(comparison), description, Utf8(value))));
    }

    [TestMethod]
    [DataRow("al", null, null, "True")]
    [DataRow("AL", new[] { "CE " }, "TH", "True")]
    [DataRow(null, new[] { "mi" }, null, "True")]
    [DataRow(null, null, "smith", "True")]
    [DataRow(null, new[] { "smith", "alice" }, null, "False")]
    [DataRow("bob", null, null, "False")]
    [DataRow(null, null, "jones", "False")]
    [DataRow("alice smi", null, "smith", "False")]
    [DataRow("alice smi", new[] { "x" }, "smith", "False")]
    [DataRow("alice", new[] { "smithy" }, null, "False")]
    [DataRow("alice", new[] { "ice" }, null, "False")]
    [DataRow("alice", new[] { "" }, "", "True")]
    public void Substrings_MatchInOrderWithoutOverlap(string? initial, string[]? any, string? final, string expected)
    {
        var filter = new LdapSubstringsFilter("cn", initial is null ? null : Utf8(initial), (any ?? []).Select(Utf8).ToArray(), final is null ? null : Utf8(final));

        Assert.AreEqual(expected, EvaluateText(filter));
    }

    [TestMethod]
    [DataRow("userPassword")]
    [DataRow("uidNumber")]
    public void Substrings_OnATypeWithoutASubstringsRule_IsUndefined(string description)
    {
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapSubstringsFilter(description, Utf8("1"), [], null)));
    }

    [TestMethod]
    public void Substrings_WithAPartThatIsNotUtf8_IsUndefined()
    {
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapSubstringsFilter("cn", null, [[0xFF]], null)));
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapSubstringsFilter("cn", [0xFF], [], null)));
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapSubstringsFilter("cn", null, [], [0xFF])));
    }

    [TestMethod]
    public void Substrings_AgainstAValueThatIsNotUtf8_IsUndefined()
    {
        var entry = new LdapEntry(Dn("cn=x"), [new LdapAttribute("cn", [[0xFF]])]);

        Assert.AreEqual(LdapFilterResult.Undefined, LdapFilterEvaluator.Evaluate(new LdapSubstringsFilter("cn", Utf8("a"), [], null), entry.Dn, entry.Attributes));
    }

    [TestMethod]
    public void EqualityMatch_WithAnAssertionThatIsNotUtf8_IsUndefined()
    {
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapComparisonFilter(LdapComparison.EqualityMatch, "sn", [0xC3])));
    }

    [TestMethod]
    public void And_IsTrueOnlyWhenEveryFilterIs()
    {
        Assert.AreEqual(LdapFilterResult.True, Evaluate(new LdapAndFilter([Equality("sn", "smith"), Present("cn")])));
        Assert.AreEqual(LdapFilterResult.False, Evaluate(new LdapAndFilter([Equality("sn", "smith"), Present("mail")])));
        Assert.AreEqual(LdapFilterResult.False, Evaluate(new LdapAndFilter([Undefined(), Present("mail")])));
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapAndFilter([Undefined(), Present("cn")])));
    }

    [TestMethod]
    public void Or_IsTrueWhenAnyFilterIs()
    {
        Assert.AreEqual(LdapFilterResult.True, Evaluate(new LdapOrFilter([Present("mail"), Present("cn")])));
        Assert.AreEqual(LdapFilterResult.True, Evaluate(new LdapOrFilter([Undefined(), Present("cn")])));
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapOrFilter([Undefined(), Present("mail")])));
        Assert.AreEqual(LdapFilterResult.False, Evaluate(new LdapOrFilter([Present("mail"), Present("uid")])));
    }

    [TestMethod]
    public void EmptyAndAndOr_AreTheAbsoluteTrueAndFalseFilters()
    {
        Assert.AreEqual(LdapFilterResult.True, Evaluate(new LdapAndFilter([])));
        Assert.AreEqual(LdapFilterResult.False, Evaluate(new LdapOrFilter([])));
    }

    [TestMethod]
    public void Not_SwapsTrueAndFalseAndKeepsUndefined()
    {
        Assert.AreEqual(LdapFilterResult.False, Evaluate(new LdapNotFilter(Present("cn"))));
        Assert.AreEqual(LdapFilterResult.True, Evaluate(new LdapNotFilter(Present("mail"))));
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new LdapNotFilter(Undefined())));
    }

    [TestMethod]
    [DataRow(null, "sn", "SMITH", false, "True")]
    [DataRow("caseIgnoreMatch", "sn", "SMITH", false, "True")]
    [DataRow("2.5.13.2", null, "alicia", false, "True")]
    [DataRow("caseExactMatch", "sn", "SMITH", false, "False")]
    [DataRow("2.5.13.5", "sn", "Smith", false, "True")]
    [DataRow("octetStringMatch", "sn", "Smith", false, "True")]
    [DataRow("2.5.13.17", "sn", "smith", false, "False")]
    [DataRow("integerMatch", "uidNumber", "1000", false, "True")]
    [DataRow("2.5.13.14", "sn", "1000", false, "Undefined")]
    [DataRow("1.2.3.4", "sn", "Smith", false, "Undefined")]
    [DataRow("caseIgnoreMatch", "mail", "x", false, "False")]
    [DataRow(null, "ou", "people", false, "False")]
    [DataRow(null, "ou", "PEOPLE", true, "True")]
    [DataRow("caseIgnoreMatch", null, "example", true, "True")]
    [DataRow(null, "ou", "staff", true, "False")]
    public void ExtensibleMatch_AppliesTheNamedRuleOrTheTypesOwn(string? rule, string? type, string value, bool dnAttributes, string expected)
    {
        Assert.AreEqual(expected, EvaluateText(new LdapExtensibleMatchFilter(rule, type, Utf8(value), dnAttributes)));
    }

    [TestMethod]
    public void AFilterChoiceTheEvaluatorDoesNotKnow_IsUndefined()
    {
        Assert.AreEqual(LdapFilterResult.Undefined, Evaluate(new UnknownFilter()));
    }

    private static LdapFilterResult Evaluate(LdapFilter filter) => LdapFilterEvaluator.Evaluate(filter, Alice.Dn, Alice.Attributes);

    private static string EvaluateText(LdapFilter filter) => Evaluate(filter).ToString();

    private static LdapFilter Equality(string description, string value) =>
        new LdapComparisonFilter(LdapComparison.EqualityMatch, description, Utf8(value));

    private static LdapFilter Present(string description) => new LdapPresentFilter(description);

    private static LdapFilter Undefined() => Equality("uidNumber", "ten");

    private sealed record UnknownFilter : LdapFilter;
}
