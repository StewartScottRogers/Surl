namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredMailboxReader"/> reading <c>&lt;address&gt;:&lt;password&gt;[;protocols=&lt;scheme&gt;,...]</c>
/// items (ADR-0101 decisions 3 to 5, ADR-0102 decisions 1.2 and 1.4).
/// </summary>
[TestClass]
public sealed partial class ConfiguredMailboxReaderTests
{
    private static readonly IReadOnlySet<string> Domains = new HashSet<string>(StringComparer.Ordinal) { "example.com" };

    private static readonly IReadOnlySet<string> NoUsers = new HashSet<string>(StringComparer.Ordinal);

    [TestMethod]
    public void Read_AddressAndPassword_SplitsAtTheFirstColonAndNormalisesTheDomain()
    {
        var mailbox = Accepted("Ann@Example.COM:pa:ss");

        Assert.AreEqual("Ann@example.com", mailbox.Address);
        Assert.AreEqual("pa:ss", mailbox.Password);
        Assert.IsNull(mailbox.Protocols);
    }

    [TestMethod]
    public void Read_Protocols_AreLowerCasedInOrder() =>
        CollectionAssert.AreEqual(new[] { "imap", "pop3" }, Accepted("ann@example.com:pw;protocols=IMAP,pop3").Protocols!.ToArray());

    [TestMethod]
    public void Read_ExtendedScheme_IsAcceptedBeforeTheRegisteredServersAreKnown() =>
        CollectionAssert.AreEqual(new[] { "coap" }, Accepted("ann@example.com:pw;protocols=coap").Protocols!.ToArray());

    [TestMethod]
    public void Read_PercentEncodedFields_AreDecodedAfterSplitting()
    {
        var mailbox = Accepted("a%25b%3Bc@example.com:p%3A%25%3B%2C");

        Assert.AreEqual("a%b;c@example.com", mailbox.Address);
        Assert.AreEqual("p:%;,", mailbox.Password);
    }

    [TestMethod]
    [DataRow("ann@example.com:pw;protocols=nosuch", "unknown scheme nosuch", DisplayName = "Unknown scheme")]
    [DataRow("ann@example.com:pw;protocols=imap%2Cpop3", "unknown scheme imap,pop3", DisplayName = "Encoded comma does not split")]
    [DataRow("ann@example.com:pw;protocols=imap,IMAP", "member imap is given twice", DisplayName = "Scheme twice")]
    [DataRow("ann@example.com:pw;protocols=im%09ap", "a control character", DisplayName = "Control character in a scheme")]
    [DataRow("a%3Ab@example.com:pw", "the address holds a colon", DisplayName = "Encoded colon in the address")]
    [DataRow("ann%09@example.com:pw", "a control character", DisplayName = "Control character in the address")]
    [DataRow("ann@example.com:a%00b", "a control character", DisplayName = "U+0000 in the password")]
    [DataRow("ann:pw", "not a mail address", DisplayName = "No @")]
    [DataRow("ann@exa_mple.com:pw", "not a domain name", DisplayName = "Not a domain")]
    [DataRow("ann@example.com:pw;quota=1", "unknown key quota", DisplayName = "quota, BL-654's")]
    public void Read_RefusedItem_GivesTheReasonAndAnEmptyMailbox(string argument, string reason)
    {
        var failure = ConfiguredMailboxReader.Read(argument, out var mailbox);

        Assert.AreEqual(reason, failure);
        Assert.AreSame(ConfiguredMailboxReader.Empty, mailbox);
    }

    [TestMethod]
    [DataRow("ann@example.com", DisplayName = "No password")]
    [DataRow("ann@example.com:", DisplayName = "Empty password")]
    [DataRow(":pw", DisplayName = "Empty address")]
    public void Read_MissingField_IsRefused(string argument) =>
        Assert.IsNotNull(ConfiguredMailboxReader.Read(argument, out _));

    [TestMethod]
    public void Build_EmptyFields_AreRefusedAsEmpty()
    {
        Assert.AreEqual("an empty field", ConfiguredMailboxReader.Build(string.Empty, "pw", null, null, null, ConfiguredMailboxReader.IsAnySurlScheme, out _));
        Assert.AreEqual("an empty field", ConfiguredMailboxReader.Build("ann@example.com", string.Empty, null, null, null, ConfiguredMailboxReader.IsAnySurlScheme, out _));
        Assert.AreEqual("an empty field", ConfiguredMailboxReader.Build("ann@example.com", "pw", [string.Empty], null, null, ConfiguredMailboxReader.IsAnySurlScheme, out _));
    }

    [TestMethod]
    public void Build_PasswordWithOtherControlCharacters_IsAccepted()
    {
        var failure = ConfiguredMailboxReader.Build("ann@example.com", "a\tb", [], null, null, ConfiguredMailboxReader.IsAnySurlScheme, out var mailbox);

        Assert.IsNull(failure);
        Assert.AreEqual("a\tb", mailbox.Password);
        Assert.IsEmpty(mailbox.Protocols!);
    }

    [TestMethod]
    public void IsServedScheme_ExtendedScheme_NeedsARegisteredServer()
    {
        Assert.IsTrue(ConfiguredMailboxReader.IsServedScheme("imap", []));
        Assert.IsTrue(ConfiguredMailboxReader.IsServedScheme("coap", ["coap"]));
        Assert.IsFalse(ConfiguredMailboxReader.IsServedScheme("coap", []));
        Assert.IsFalse(ConfiguredMailboxReader.IsAnySurlScheme("nosuch"));
    }

    [TestMethod]
    public void FindConflict_MailboxInAConfiguredDomain_HasNone() =>
        Assert.IsNull(ConfiguredMailboxReader.FindConflict("ann@Example.com", Accepted("ann@Example.com:pw"), Domains, NoUsers));

    [TestMethod]
    public void FindConflict_NoMailDomain_NeedsOne() =>
        Assert.AreEqual(
            "mailbox ann@example.com needs --mail-domain",
            ConfiguredMailboxReader.FindConflict("ann@example.com", Accepted("ann@example.com:pw"), new HashSet<string>(), NoUsers));

    [TestMethod]
    public void FindConflict_OtherDomain_IsNotInAnyMailDomain() =>
        Assert.AreEqual(
            "mailbox ann@example.org is not in any --mail-domain",
            ConfiguredMailboxReader.FindConflict("ann@example.org", Accepted("ann@example.org:pw"), Domains, NoUsers));

    [TestMethod]
    public void FindConflict_AddressOfAUser_IsAlsoAUserAccount() =>
        Assert.AreEqual(
            "mailbox ann@EXAMPLE.com is also a --user account",
            ConfiguredMailboxReader.FindConflict(
                "ann@EXAMPLE.com", Accepted("ann@EXAMPLE.com:pw"), Domains, new HashSet<string>(StringComparer.Ordinal) { "ann@example.com" }));

    [TestMethod]
    public void FindConflict_NullArgument_Throws()
    {
        var mailbox = Accepted("ann@example.com:pw");

        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMailboxReader.FindConflict("a", null!, Domains, NoUsers));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMailboxReader.FindConflict("a", mailbox, null!, NoUsers));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMailboxReader.FindConflict("a", mailbox, Domains, null!));
    }

    [TestMethod]
    public void MailboxGivenTwice_NamesTheAddress() =>
        Assert.AreEqual("mailbox ann@example.com is given twice", ConfiguredMailboxReader.MailboxGivenTwice("ann@example.com"));

    [TestMethod]
    public void Reading_IsNamedByTheItemShape()
    {
        Assert.AreEqual("<address>:<password>[;protocols=<scheme>,...]", ConfiguredMailboxReader.Reading.Type.Name);
        Assert.IsNull(ConfiguredMailboxReader.Reading.Read("ann@example.com:pw", out _));
    }

    [TestMethod]
    public void ToString_NeverShowsThePassword()
    {
        var text = Accepted("ann@example.com:hunter2").ToString();

        Assert.AreEqual("ConfiguredMailbox { Address = ann@example.com }", text);
        Assert.DoesNotContain("hunter2", text);
    }

    private static ConfiguredMailbox Accepted(string argument)
    {
        var failure = ConfiguredMailboxReader.Read(argument, out var mailbox);

        Assert.IsNull(failure);
        return mailbox;
    }
}
