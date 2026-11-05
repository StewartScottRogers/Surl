using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>ADR-0101 decisions 3, 5 (rows 10 to 13) and 6: the layer-2 item grammar.</summary>
[TestClass]
public sealed class ConfigurationItemReaderTests
{
    private const string MailboxArgument = "<address>:<password>[;protocols=<scheme>,...][;quota=<bytes>][;folders=<name>,...]";
    private const string ShareAccessArgument = "<share>=<member>:<r|rw>,...";

    private static readonly ConfigurationItemShape Mailbox = new(
        MailboxArgument,
        ':',
        ConfigurationItemValueKind.Text,
        new Dictionary<string, ConfigurationItemAttributeKind>
        {
            ["protocols"] = ConfigurationItemAttributeKind.List,
            ["quota"] = ConfigurationItemAttributeKind.Value,
            ["folders"] = ConfigurationItemAttributeKind.List,
            ["readonly"] = ConfigurationItemAttributeKind.Flag,
        },
        []);

    private static readonly ConfigurationItemShape Topic = new(
        "<filter>[;retain][;qos=<0|1|2>]",
        null,
        ConfigurationItemValueKind.None,
        new Dictionary<string, ConfigurationItemAttributeKind>
        {
            ["retain"] = ConfigurationItemAttributeKind.Flag,
            ["qos"] = ConfigurationItemAttributeKind.Value,
        },
        []);

    private static readonly ConfigurationItemShape CatchAll =
        new("<domain>=<address>", '=', ConfigurationItemValueKind.Name, FrozenDictionary<string, ConfigurationItemAttributeKind>.Empty, []);

    private static readonly ConfigurationItemShape Alias =
        new("<address>=<address>,...", '=', ConfigurationItemValueKind.List, FrozenDictionary<string, ConfigurationItemAttributeKind>.Empty, []);

    private static readonly ConfigurationItemShape Group =
        new("<name>=<member>,...", '=', ConfigurationItemValueKind.Members, FrozenDictionary<string, ConfigurationItemAttributeKind>.Empty, []);

    private static readonly ConfigurationItemShape ShareAccess =
        new(ShareAccessArgument, '=', ConfigurationItemValueKind.Grants, FrozenDictionary<string, ConfigurationItemAttributeKind>.Empty, ["r", "rw"]);

    [TestMethod]
    public void Read_NullShapeOrArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigurationItemReader.Read(null!, "a", out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigurationItemReader.Read(Topic, null!, out _));
    }

    [TestMethod]
    public void Read_BareName_IsTheNameAndNothingElse()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Topic, "sensors/+/temp", out var item));

        Assert.AreEqual("sensors/+/temp", item.Name);
        Assert.IsNull(item.Value);
        Assert.IsEmpty(item.List);
        Assert.IsEmpty(item.Members);
        Assert.IsEmpty(item.Grants);
        Assert.IsEmpty(item.Flags);
        Assert.IsEmpty(item.Values);
        Assert.IsEmpty(item.Lists);
    }

    [TestMethod]
    public void Read_NoSeparatorInTheShape_KeepsEqualsSignsInTheName()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Topic, "a=b", out var item));

        Assert.AreEqual("a=b", item.Name);
    }

    [TestMethod]
    public void Read_NameAndText_SplitsAtTheFirstSeparator()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, "ann@example.com:pa:ss", out var item));

        Assert.AreEqual("ann@example.com", item.Name);
        Assert.AreEqual("pa:ss", item.Value);
    }

    [TestMethod]
    public void Read_TextValue_MayHoldAControlCharacterButNotNul()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, "ann:pa%09ss", out var item));
        Assert.AreEqual("pa\tss", item.Value);

        Assert.AreEqual(ConfigurationItemReader.ControlCharacter, ConfigurationItemReader.Read(Mailbox, "ann:pa%00ss", out _));
    }

    [TestMethod]
    public void Read_NameValue_IsTheFieldAfterTheSeparator()
    {
        Assert.IsNull(ConfigurationItemReader.Read(CatchAll, "example.com=postmaster@example.com", out var item));

        Assert.AreEqual("example.com", item.Name);
        Assert.AreEqual("postmaster@example.com", item.Value);
        Assert.AreEqual(ConfigurationItemReader.ControlCharacter, ConfigurationItemReader.Read(CatchAll, "example.com=a%09b", out _));
    }

    [TestMethod]
    public void Read_Flags_AreLowerCasedAndMatchedInAnyCase()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Topic, "t;RETAIN", out var item));

        CollectionAssert.AreEquivalent(new[] { "retain" }, item.Flags.ToArray());
    }

    [TestMethod]
    public void Read_KeyValuePairsAndListKeys_AreReadByKey()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, "ann:pw;Quota=10m;protocols=imap,pop3;readonly", out var item));

        Assert.AreEqual("10m", item.Values["quota"]);
        CollectionAssert.AreEqual(new[] { "imap", "pop3" }, item.Lists["protocols"].ToArray());
        CollectionAssert.AreEquivalent(new[] { "readonly" }, item.Flags.ToArray());
    }

    [TestMethod]
    public void Read_CommaList_IsEachMemberInOrder()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Alias, "sales@example.com=ann@example.com,bob@example.com", out var item));

        Assert.AreEqual("sales@example.com", item.Name);
        CollectionAssert.AreEqual(new[] { "ann@example.com", "bob@example.com" }, item.List.ToArray());
    }

    [TestMethod]
    public void Read_MemberList_ReadsAccountsAndAtGroups()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Group, "staff=ann,@admins,%40bob", out var item));

        CollectionAssert.AreEqual(
            new[] { new ConfiguredMember("ann", false), new ConfiguredMember("admins", true), new ConfiguredMember("@bob", false) },
            item.Members.ToArray());
    }

    [TestMethod]
    public void Read_GrantList_SplitsEachMemberAtItsLastColonAndLowerCasesTheAccess()
    {
        Assert.IsNull(ConfigurationItemReader.Read(ShareAccess, "docs=ann:r,@staff:RW,a:b:rw", out var item));

        CollectionAssert.AreEqual(
            new[]
            {
                new ConfiguredGrant(new("ann", false), "r"),
                new ConfiguredGrant(new("staff", true), "rw"),
                new ConfiguredGrant(new("a:b", false), "rw"),
            },
            item.Grants.ToArray());
    }

    [TestMethod]
    [DataRow("docs=ann", "expected " + ShareAccessArgument)]
    [DataRow("docs=ann:x", OptionArgumentReader.BadlyUsed)]
    [DataRow("docs=:r", ConfigurationItemReader.EmptyField)]
    [DataRow("docs=@:r", ConfigurationItemReader.EmptyField)]
    [DataRow("docs=ann:", ConfigurationItemReader.EmptyField)]
    public void Read_MalformedGrant_IsRefused(string argument, string reason) =>
        Assert.AreEqual(reason, ConfigurationItemReader.Read(ShareAccess, argument, out _));

    [TestMethod]
    [DataRow("ann:p%3Bw", "p;w")]
    [DataRow("ann:p%3bw", "p;w")]
    [DataRow("ann:100%25", "100%")]
    [DataRow("ann:C:\\data\\x", "C:\\data\\x")]
    [DataRow("ann:caf%C3%A9", "café")]
    [DataRow("ann:ä%2Cö", "ä,ö")]
    public void Read_PercentEscapes_DecodeAfterSplitting(string argument, string password)
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, argument, out var item));

        Assert.AreEqual(password, item.Value);
    }

    [TestMethod]
    public void Read_EncodedDelimiters_NeverSplit()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, "a%3Ab:pw;folders=x%2Cy,z;quota=1%3D2", out var item));

        Assert.AreEqual("a:b", item.Name);
        CollectionAssert.AreEqual(new[] { "x,y", "z" }, item.Lists["folders"].ToArray());
        Assert.AreEqual("1=2", item.Values["quota"]);
    }

    [TestMethod]
    [DataRow("ann:pw%")]
    [DataRow("ann:pw%4")]
    [DataRow("ann:pw%ZZ")]
    [DataRow("ann:pw%+1")]
    public void Read_MalformedPercentEscape_IsRefused(string argument) =>
        Assert.AreEqual(ConfigurationItemReader.MalformedPercentEscape, ConfigurationItemReader.Read(Mailbox, argument, out _));

    [TestMethod]
    public void Read_DecodedBytesNotUtf8_AreRefused() =>
        Assert.AreEqual(ConfigurationItemReader.NotUtf8AfterDecoding, ConfigurationItemReader.Read(Mailbox, "ann:%FFpw", out _));

    [TestMethod]
    [DataRow("", ConfigurationItemReader.NameIsEmpty)]
    [DataRow(";retain", ConfigurationItemReader.NameIsEmpty)]
    [DataRow("a%01b", ConfigurationItemReader.ControlCharacter)]
    [DataRow("a%7Fb", ConfigurationItemReader.ControlCharacter)]
    [DataRow("a%ZZ", ConfigurationItemReader.MalformedPercentEscape)]
    [DataRow("t;;retain", ConfigurationItemReader.EmptyField)]
    [DataRow("t;retain;", ConfigurationItemReader.EmptyField)]
    [DataRow("t;=1", ConfigurationItemReader.EmptyField)]
    [DataRow("t;re%01tain", ConfigurationItemReader.ControlCharacter)]
    [DataRow("t;qos", "key qos needs a value")]
    [DataRow("t;qos=", "key qos needs a value")]
    [DataRow("t;qos=%01", ConfigurationItemReader.ControlCharacter)]
    [DataRow("t;retain=yes", "flag retain takes no value")]
    [DataRow("t;qos=1;QOS=2", "key qos is given twice")]
    [DataRow("t;retain;retain", "key retain is given twice")]
    [DataRow("t;durable", "unknown flag durable")]
    [DataRow("t;Priority=1", "unknown key priority")]
    public void Read_MalformedTopic_IsRefusedInTheAdrsWords(string argument, string reason) =>
        Assert.AreEqual(reason, ConfigurationItemReader.Read(Topic, argument, out _));

    [TestMethod]
    [DataRow("ann", "expected " + MailboxArgument)]
    [DataRow(":pw", ConfigurationItemReader.NameIsEmpty)]
    [DataRow("ann:", ConfigurationItemReader.EmptyField)]
    [DataRow("ann:pw;folders=a,,b", ConfigurationItemReader.EmptyField)]
    [DataRow("ann:pw;folders=a,", ConfigurationItemReader.EmptyField)]
    public void Read_MalformedMailbox_IsRefusedInTheAdrsWords(string argument, string reason) =>
        Assert.AreEqual(reason, ConfigurationItemReader.Read(Mailbox, argument, out _));

    [TestMethod]
    [DataRow("sales=a,,b")]
    [DataRow("sales=a,")]
    [DataRow("sales=")]
    public void Read_EmptyListMember_IsRefused(string argument) =>
        Assert.AreEqual(ConfigurationItemReader.EmptyField, ConfigurationItemReader.Read(Alias, argument, out _));

    [TestMethod]
    public void Read_EmptyGroupName_IsRefused() =>
        Assert.AreEqual(ConfigurationItemReader.EmptyField, ConfigurationItemReader.Read(Group, "staff=ann,@", out _));

    [TestMethod]
    public void Read_ItemAtTheLengthBound_IsAcceptedAndOnePastItIsRefused()
    {
        var name = new string('a', ConfigurationItemReader.MaxItemLength);

        Assert.IsNull(ConfigurationItemReader.Read(Topic, name, out _));
        Assert.AreEqual(
            "the item is longer than 4096 characters",
            ConfigurationItemReader.Read(Topic, name + "a", out var item));
        Assert.AreSame(ConfigurationItem.Empty, item);
    }

    [TestMethod]
    public void Read_ListAtTheCountBound_IsAcceptedAndOnePastItIsRefused()
    {
        var members = string.Join(',', Enumerable.Repeat("m", ConfigurationItemReader.MaxListMembers));

        Assert.IsNull(ConfigurationItemReader.Read(Alias, "a=" + members, out var item));
        Assert.HasCount(ConfigurationItemReader.MaxListMembers, item.List);
        Assert.AreEqual("more than 1000 members", ConfigurationItemReader.Read(Alias, "a=" + members + ",m", out _));
        Assert.AreEqual("more than 1000 members", ConfigurationItemReader.Read(Mailbox, "a:p;folders=" + members + ",m", out _));
    }

    [TestMethod]
    public void Bounds_AreTheAdrsNumbersAndTexts()
    {
        Assert.AreEqual(10000, ConfigurationItemReader.MaxOccurrences);
        Assert.AreEqual("given more than 10000 times", ConfigurationItemReader.GivenTooOften);
    }

    [TestMethod]
    [DataRow("ann:hunter2%ZZ")]
    [DataRow("ann:hunter2%FF")]
    [DataRow("ann:hunter2%00")]
    public void Read_MalformedItemHoldingASecret_NeverEchoesIt(string argument)
    {
        var reason = ConfigurationItemReader.Read(Mailbox, argument, out var item);

        Assert.IsNotNull(reason);
        Assert.DoesNotContain("hunter2", reason);
        Assert.AreSame(ConfigurationItem.Empty, item);
    }

    [TestMethod]
    public void ToString_NamesTheItemWithoutItsPassword()
    {
        Assert.IsNull(ConfigurationItemReader.Read(Mailbox, "ann:hunter2", out var item));

        Assert.AreEqual("ConfigurationItem { Name = ann }", item.ToString());
    }

    [TestMethod]
    public void Reading_ReadsByTheShapeAndIsTypedByItsArgument()
    {
        Assert.IsNull(Mailbox.Reading.Read("ann:pw", out var item));

        Assert.AreEqual("ann", item.Name);
        Assert.AreEqual(MailboxArgument, Mailbox.Reading.Type.Name);
        Assert.AreEqual(
            "keys and flags folders=<name>,..., protocols=<name>,..., quota=<value>, readonly; " + OptionArgumentType.ItemSyntax,
            Mailbox.Reading.Type.AllowedValues);
        Assert.AreEqual("no keys or flags; " + OptionArgumentType.ItemSyntax, Alias.Reading.Type.AllowedValues);
    }
}
