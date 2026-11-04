using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Searches a directory of fixture entries with each scope, filter and attribute selection, and
/// checks the root DSE, the result codes and the bounds of ADR-0072 decision 1.
/// </summary>
[TestClass]
public sealed class LdapDirectoryTests
{
    [TestMethod]
    public void Search_BaseScope_ReturnsTheBaseEntryWithEveryAttribute()
    {
        var outcome = PeopleDirectory().Search(Search("cn=alice,dc=example,dc=com", LdapSearchScope.BaseObject));

        Assert.AreEqual(LdapResultCode.Success, outcome.Result.ResultCode);
        Assert.AreEqual("cn=alice,dc=example,dc=com", DnsOf(outcome));
        Assert.AreEqual(
            "objectClass=person;cn=alice;sn=Smith;mail=alice@example.com;uidNumber=1000;cn;lang-fr=alicia",
            AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_OneLevelScope_ReturnsTheImmediateSubordinatesInDirectoryOrder()
    {
        var outcome = PeopleDirectory().Search(Search("dc=example,dc=com", LdapSearchScope.SingleLevel));

        Assert.AreEqual("cn=alice,dc=example,dc=com|cn=bob,dc=example,dc=com|ou=staff,dc=example,dc=com", DnsOf(outcome));
    }

    [TestMethod]
    public void Search_SubtreeScope_ReturnsTheBaseAndEverythingBelowIt()
    {
        var outcome = PeopleDirectory().Search(Search("DC=Example, DC=Com", LdapSearchScope.WholeSubtree));

        Assert.AreEqual(
            "dc=example,dc=com|cn=alice,dc=example,dc=com|cn=bob,dc=example,dc=com|ou=staff,dc=example,dc=com|uid=carol,ou=staff,dc=example,dc=com",
            DnsOf(outcome));
    }

    [TestMethod]
    public void Search_ABaseWrittenDifferently_FindsTheEntryAndReturnsItsDnAsStored()
    {
        var outcome = PeopleDirectory().Search(Search("CN=Al\\49CE ,  dc=EXAMPLE,dc=com", LdapSearchScope.BaseObject));

        Assert.AreEqual("cn=alice,dc=example,dc=com", DnsOf(outcome));
    }

    [TestMethod]
    public void Search_TheAdrsFilter_ReturnsAliceOnly()
    {
        var filter = new LdapAndFilter(
        [
            new LdapComparisonFilter(LdapComparison.EqualityMatch, "objectClass", Utf8("person")),
            new LdapOrFilter(
            [
                new LdapSubstringsFilter("cn", Utf8("al"), [], null),
                new LdapComparisonFilter(LdapComparison.GreaterOrEqual, "sn", Utf8("K")),
            ]),
            new LdapNotFilter(new LdapSubstringsFilter("mail", null, [], Utf8("@other.example"))),
        ]);

        var outcome = PeopleDirectory().Search(Search("dc=example,dc=com", LdapSearchScope.WholeSubtree, filter, "cn"));

        Assert.AreEqual("cn=alice,dc=example,dc=com", DnsOf(outcome));
        Assert.AreEqual("cn=alice;cn;lang-fr=alicia", AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    [DataRow("LessOrEqual", "uidNumber", "100", "cn=bob,dc=example,dc=com")]
    [DataRow("ApproxMatch", "cn", "CAROL ANN", "uid=carol,ou=staff,dc=example,dc=com")]
    public void Search_AComparisonFilter_ReturnsTheEntriesItHolds(string comparison, string description, string value, string expected)
    {
        var outcome = PeopleDirectory().Search(
            Search("dc=example,dc=com", LdapSearchScope.WholeSubtree, new LdapComparisonFilter(Enum.Parse<LdapComparison>(comparison), description, Utf8(value))));

        Assert.AreEqual(expected, DnsOf(outcome));
    }

    [TestMethod]
    public void Search_AnExtensibleMatchOnDnAttributes_ReturnsEveryEntryUnderTheMatchingRdn()
    {
        var outcome = PeopleDirectory().Search(
            Search("dc=example,dc=com", LdapSearchScope.WholeSubtree, new LdapExtensibleMatchFilter(null, "ou", Utf8("STAFF"), DnAttributes: true)));

        Assert.AreEqual("ou=staff,dc=example,dc=com|uid=carol,ou=staff,dc=example,dc=com", DnsOf(outcome));
    }

    [TestMethod]
    public void Search_AFilterNothingMatches_AnswersSuccessWithNoEntries()
    {
        var outcome = PeopleDirectory().Search(
            Search("dc=example,dc=com", LdapSearchScope.WholeSubtree, new LdapComparisonFilter(LdapComparison.EqualityMatch, "cn", Utf8("nobody"))));

        Assert.IsEmpty(outcome.Entries);
        Assert.AreEqual(LdapResultCode.Success, outcome.Result.ResultCode);
    }

    [TestMethod]
    [DataRow(new string[0], "objectClass=person;cn=alice;sn=Smith;mail=alice@example.com;uidNumber=1000;cn;lang-fr=alicia")]
    [DataRow(new[] { "*" }, "objectClass=person;cn=alice;sn=Smith;mail=alice@example.com;uidNumber=1000;cn;lang-fr=alicia")]
    [DataRow(new[] { "1.1" }, "")]
    [DataRow(new[] { "+" }, "")]
    [DataRow(new[] { "1.1", "sn" }, "sn=Smith")]
    [DataRow(new[] { "MAIL", "SN", "nothing" }, "sn=Smith;mail=alice@example.com")]
    [DataRow(new[] { "cn;lang-fr" }, "cn;lang-fr=alicia")]
    [DataRow(new[] { "*", "+" }, "objectClass=person;cn=alice;sn=Smith;mail=alice@example.com;uidNumber=1000;cn;lang-fr=alicia")]
    public void Search_TheAttributeSelection_PicksTheAttributesReturned(string[] attributes, string expected)
    {
        var outcome = PeopleDirectory().Search(Search("cn=alice,dc=example,dc=com", LdapSearchScope.BaseObject, null, attributes));

        Assert.AreEqual(expected, AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_TypesOnly_ReturnsTheSelectedDescriptionsWithNoValues()
    {
        var request = Search("cn=alice,dc=example,dc=com", LdapSearchScope.BaseObject, null, "cn", "mail") with { TypesOnly = true };

        var entry = PeopleDirectory().Search(request).Entries[0];

        CollectionAssert.AreEqual(new[] { "cn", "mail", "cn;lang-fr" }, entry.Attributes.Select(attribute => attribute.Type).ToArray());
        Assert.IsTrue(entry.Attributes.All(attribute => attribute.Values.Count == 0));
    }

    [TestMethod]
    public void Search_ABaseTheDirectoryLacks_AnswersNoSuchObjectWithTheNearestSuperior()
    {
        var outcome = PeopleDirectory().Search(Search("cn=nobody,ou=Staff,dc=example,dc=com", LdapSearchScope.WholeSubtree));

        Assert.IsEmpty(outcome.Entries);
        Assert.AreEqual(new LdapResult(LdapResultCode.NoSuchObject, "ou=staff,dc=example,dc=com", string.Empty), outcome.Result);
    }

    [TestMethod]
    public void Search_ABaseWithNoSuperiorInTheDirectory_AnswersNoSuchObjectWithNoMatchedDn()
    {
        var outcome = PeopleDirectory().Search(Search("dc=nowhere", LdapSearchScope.BaseObject));

        Assert.AreEqual(new LdapResult(LdapResultCode.NoSuchObject, string.Empty, string.Empty), outcome.Result);
    }

    [TestMethod]
    public void Search_AnEmptyDirectory_AnswersNoSuchObject()
    {
        var outcome = new LdapDirectory([], TimeProvider.System).Search(Search("dc=example,dc=com", LdapSearchScope.BaseObject));

        Assert.AreEqual(LdapResultCode.NoSuchObject, outcome.Result.ResultCode);
    }

    [TestMethod]
    public void Search_ABaseThatIsNotADn_AnswersInvalidDnSyntax()
    {
        var outcome = PeopleDirectory().Search(Search("not a dn", LdapSearchScope.BaseObject));

        Assert.AreEqual(new LdapResult(LdapResultCode.InvalidDnSyntax, string.Empty, "the base is not an RFC 4514 DN"), outcome.Result);
    }

    [TestMethod]
    public void Search_TheRootDse_ReturnsObjectClassForAll()
    {
        var outcome = PeopleDirectory().Search(Search(string.Empty, LdapSearchScope.BaseObject), new LdapRootDseFacts(["NTLM"], IsStartTlsOffered: true));

        Assert.AreEqual(string.Empty, DnsOf(outcome));
        Assert.AreEqual("objectClass=top", AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_TheRootDseWithPlus_ReturnsEveryOperationalAttribute()
    {
        var outcome = PeopleDirectory().Search(
            Search(string.Empty, LdapSearchScope.BaseObject, null, "+"),
            new LdapRootDseFacts(["GSS-SPNEGO", "DIGEST-MD5"], IsStartTlsOffered: true));

        Assert.AreEqual(
            "namingContexts=dc=example,dc=com,o=other;supportedLDAPVersion=3;supportedSASLMechanisms=GSS-SPNEGO,DIGEST-MD5;supportedExtension=1.3.6.1.4.1.1466.20037",
            AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_TheRootDseOfAnEmptyDirectoryOfferingNothing_LeavesOutWhatHasNoValue()
    {
        var outcome = new LdapDirectory([], TimeProvider.System).Search(Search(string.Empty, LdapSearchScope.BaseObject, null, "*", "+"), NothingOffered);

        Assert.AreEqual("objectClass=top;supportedLDAPVersion=3", AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_TheRootDseByName_ReturnsTheNamedOperationalAttributes()
    {
        var outcome = PeopleDirectory().Search(
            Search(string.Empty, LdapSearchScope.BaseObject, new LdapPresentFilter("objectclass"), "supportedCapabilities", "SUPPORTEDSASLMECHANISMS"),
            new LdapRootDseFacts(["NTLM"], IsStartTlsOffered: false));

        Assert.AreEqual("supportedSASLMechanisms=NTLM", AttributesOf(outcome.Entries[0]));
    }

    [TestMethod]
    public void Search_TheRootDseWithAFilterItFails_ReturnsNothing()
    {
        var outcome = PeopleDirectory().Search(Search(string.Empty, LdapSearchScope.BaseObject, new LdapPresentFilter("cn")), NothingOffered);

        Assert.IsEmpty(outcome.Entries);
        Assert.AreEqual(LdapResultCode.Success, outcome.Result.ResultCode);
    }

    [TestMethod]
    [DataRow("SingleLevel")]
    [DataRow("WholeSubtree")]
    public void Search_BelowTheRootDse_FindsNoEntries(string scope)
    {
        var outcome = PeopleDirectory().Search(Search(string.Empty, Enum.Parse<LdapSearchScope>(scope)), NothingOffered);

        Assert.IsEmpty(outcome.Entries);
        Assert.AreEqual(LdapResultCode.Success, outcome.Result.ResultCode);
    }

    [TestMethod]
    public void Search_PastTheRequestsSizeLimit_AnswersSizeLimitExceededAfterTheLimit()
    {
        var request = Search("dc=example,dc=com", LdapSearchScope.WholeSubtree) with { SizeLimit = 2 };

        var outcome = PeopleDirectory().Search(request);

        Assert.AreEqual("dc=example,dc=com|cn=alice,dc=example,dc=com", DnsOf(outcome));
        Assert.AreEqual(LdapResultCode.SizeLimitExceeded, outcome.Result.ResultCode);
    }

    [TestMethod]
    public void Search_ExactlyTheSizeLimit_AnswersSuccess()
    {
        var request = Search("dc=example,dc=com", LdapSearchScope.SingleLevel) with { SizeLimit = 3 };

        Assert.AreEqual(LdapResultCode.Success, PeopleDirectory().Search(request).Result.ResultCode);
    }

    [TestMethod]
    public void Search_PastTheDirectorysSearchBound_AnswersSizeLimitExceeded()
    {
        var directory = new LdapDirectory(PeopleEntries(), TimeProvider.System, maxSearchEntries: 1);
        var request = Search("dc=example,dc=com", LdapSearchScope.WholeSubtree) with { SizeLimit = 5 };

        var outcome = directory.Search(request);

        Assert.AreEqual("dc=example,dc=com", DnsOf(outcome));
        Assert.AreEqual(LdapResultCode.SizeLimitExceeded, outcome.Result.ResultCode);
    }

    [TestMethod]
    public void Search_PastTheTimeLimit_AnswersTimeLimitExceededWithTheEntriesFoundSoFar()
    {
        var clock = new SteppingTimeProvider(TimeSpan.FromMilliseconds(400));
        var directory = new LdapDirectory(PeopleEntries(), clock);
        var request = Search("dc=example,dc=com", LdapSearchScope.WholeSubtree) with { TimeLimit = 1 };

        var outcome = directory.Search(request);

        Assert.AreEqual("dc=example,dc=com|cn=alice,dc=example,dc=com", DnsOf(outcome));
        Assert.AreEqual(LdapResultCode.TimeLimitExceeded, outcome.Result.ResultCode);
    }

    [TestMethod]
    public void Search_WithNoTimeLimit_IsNotCutShort()
    {
        var directory = new LdapDirectory(PeopleEntries(), new SteppingTimeProvider(TimeSpan.FromDays(1)));

        var outcome = directory.Search(Search("dc=example,dc=com", LdapSearchScope.WholeSubtree));

        Assert.AreEqual(LdapResultCode.Success, outcome.Result.ResultCode);
        Assert.HasCount(5, outcome.Entries);
    }

    [TestMethod]
    public void FindEntry_FindsByNormalForm()
    {
        var directory = PeopleDirectory();

        Assert.AreEqual("cn=bob,dc=example,dc=com", directory.FindEntry(Dn("CN=BOB,DC=EXAMPLE,DC=COM"))?.Dn.Text);
        Assert.IsNull(directory.FindEntry(Dn("cn=nobody,dc=example,dc=com")));
    }

    [TestMethod]
    public void NamingContexts_AreTheEntriesWithNoSuperiorInTheDirectory()
    {
        CollectionAssert.AreEqual(new[] { "dc=example,dc=com", "o=other" }, PeopleDirectory().NamingContexts.ToArray());
    }

    [TestMethod]
    public void Constructor_AnEntryWithNoAttribute_IsRefused()
    {
        AssertRefused(LdapDirectoryFault.EntryWithoutAttribute, 1, [Entry("o=x", ("o", "x")), new LdapEntry(Dn("cn=a,o=x"), [])]);
    }

    [TestMethod]
    public void Constructor_AnEntryNamedByTheEmptyDn_IsRefused()
    {
        AssertRefused(LdapDirectoryFault.RootDseEntry, 0, [Entry(string.Empty, ("objectClass", "top"))]);
    }

    [TestMethod]
    public void Constructor_ADuplicateDnInAnotherForm_IsRefused()
    {
        AssertRefused(LdapDirectoryFault.DuplicateDn, 1, [Entry("o=x", ("o", "x")), Entry("O = X", ("o", "x"))]);
    }

    [TestMethod]
    public void Constructor_AnEntryWhoseParentIsMissingUnderAnExistingSuperior_IsRefused()
    {
        AssertRefused(LdapDirectoryFault.ParentNotInDirectory, 1, [Entry("o=x", ("o", "x")), Entry("cn=a,ou=gone,o=x", ("cn", "a"))]);
    }

    [TestMethod]
    public void Constructor_AChildBeforeItsParent_IsAccepted()
    {
        var directory = new LdapDirectory([Entry("cn=a,o=x", ("cn", "a")), Entry("o=x", ("o", "x"))], TimeProvider.System);

        CollectionAssert.AreEqual(new[] { "o=x" }, directory.NamingContexts.ToArray());
    }

    [TestMethod]
    public void Constructor_PastTheEntryBound_IsRefused()
    {
        var exception = Assert.ThrowsExactly<LdapDirectoryException>(() => new LdapDirectory(PeopleEntries(), TimeProvider.System, maxEntries: 2));

        Assert.AreEqual(LdapDirectoryFault.PastBounds, exception.Fault);
        Assert.AreEqual(2, exception.EntryIndex);
    }

    [TestMethod]
    public void Constructor_PastTheTotalBytesBound_IsRefused()
    {
        // "o=x" (3) + "o" (1) + "x" (1) is 5 bytes; the second entry takes the total past 8.
        var exception = Assert.ThrowsExactly<LdapDirectoryException>(
            () => new LdapDirectory([Entry("o=x", ("o", "x")), Entry("cn=a,o=x", ("cn", "a"))], TimeProvider.System, maxTotalBytes: 8));

        Assert.AreEqual(LdapDirectoryFault.PastBounds, exception.Fault);
        Assert.AreEqual(1, exception.EntryIndex);
    }

    [TestMethod]
    public void Constructor_ExactlyAtTheTotalBytesBound_IsAccepted()
    {
        Assert.HasCount(1, new LdapDirectory([Entry("o=x", ("o", "x"))], TimeProvider.System, maxTotalBytes: 5).Entries);
    }

    [TestMethod]
    public void Constructor_AValuePastTheValueBound_IsRefused()
    {
        var entry = new LdapEntry(Dn("o=x"), [new LdapAttribute("jpegPhoto", [new byte[LdapDirectory.MaxValueBytes + 1]])]);

        AssertRefused(LdapDirectoryFault.PastBounds, 0, [entry]);
    }

    [TestMethod]
    public void Constructor_AValueAtTheValueBound_IsAccepted()
    {
        var entry = new LdapEntry(Dn("o=x"), [new LdapAttribute("jpegPhoto", [new byte[LdapDirectory.MaxValueBytes]])]);

        Assert.HasCount(1, new LdapDirectory([entry], TimeProvider.System).Entries);
    }

    [TestMethod]
    public async Task Search_FromManyThreadsAtOnce_AnswersEachTheSame()
    {
        var directory = PeopleDirectory();
        var request = Search("dc=example,dc=com", LdapSearchScope.WholeSubtree);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => DnsOf(directory.Search(request)))));

        Assert.AreEqual(1, outcomes.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertRefused(LdapDirectoryFault fault, int entryIndex, IReadOnlyList<LdapEntry> entries)
    {
        var exception = Assert.ThrowsExactly<LdapDirectoryException>(() => new LdapDirectory(entries, TimeProvider.System));

        Assert.AreEqual(fault, exception.Fault);
        Assert.AreEqual(entryIndex, exception.EntryIndex);
        Assert.AreEqual($"Entry {entryIndex}: {fault}.", exception.Message);
    }

    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            var current = now;
            now += step;
            return current;
        }
    }
}
