using System.Text;

namespace Surl.Cli;

/// <summary>
/// <see cref="MailboxFileReader"/> reading <c>--mailbox-file</c> CSV (ADR-0101 decisions 4 to 6, BL-653).
/// </summary>
[TestClass]
public sealed class MailboxFileReaderTests
{
    private const string File = "m.csv";

    private static readonly SurlCommandLine WithDomain = new() { MailDomains = ["example.com"] };

    [TestMethod]
    public void Read_HeaderAndRecords_ReadsEachMailboxInFileOrder()
    {
        var mailboxes = Accepted("address,password,protocols\r\nann@example.com,secret,\"imap,POP3\"\r\nbob@example.com,pw,\r\n");

        Assert.HasCount(2, mailboxes);
        Assert.AreEqual(new ConfiguredMailbox("ann@example.com", "secret", mailboxes[0].Protocols), mailboxes[0]);
        CollectionAssert.AreEqual(new[] { "imap", "pop3" }, mailboxes[0].Protocols!.ToArray());
        Assert.AreEqual("bob@example.com", mailboxes[1].Address);
        Assert.IsNull(mailboxes[1].Protocols);
    }

    [TestMethod]
    public void Read_ColumnsInAnyOrderAndCase_AreMatchedByName()
    {
        var mailbox = Accepted("Password,ADDRESS\nsecret,ann@example.com").Single();

        Assert.AreEqual("ann@example.com", mailbox.Address);
        Assert.AreEqual("secret", mailbox.Password);
    }

    [TestMethod]
    public void Read_QuotedFields_HoldCommasQuotesAndLineEnds()
    {
        var mailbox = Accepted("address,password\n\"ann@example.com\",\"a,\"\"b\"\"\nc\"\n").Single();

        Assert.AreEqual("a,\"b\"\nc", mailbox.Password);
    }

    [TestMethod]
    public void Read_QuotedFieldClosingAtEndOfText_IsRead()
    {
        var mailbox = Accepted("address,password\nann@example.com,\"secret\"").Single();

        Assert.AreEqual("secret", mailbox.Password);
    }

    [TestMethod]
    public void Read_ByteOrderMarkAndBlankRecords_AreSkipped() =>
        Assert.HasCount(2, Accepted("﻿\naddress,password\n\nann@example.com,a\n\r\nbob@example.com,b"));

    [TestMethod]
    public void Read_FiveHundredRows_ReadsEveryMailbox()
    {
        var text = new StringBuilder("address,password,protocols\n");
        for (var index = 0; index < 500; index++)
        {
            text.Append("user").Append(index).Append("@example.com,secret").Append(index).Append(",imap\n");
        }

        var mailboxes = Accepted(text.ToString());

        Assert.HasCount(500, mailboxes);
        Assert.AreEqual("user499@example.com", mailboxes[499].Address);
    }

    [TestMethod]
    [DataRow("", 1, "expected a header row with address and password", DisplayName = "Empty file")]
    [DataRow("\n\n", 3, "expected a header row with address and password", DisplayName = "Only blank lines")]
    [DataRow("address\nann@example.com\n", 1, "expected a header row with address and password", DisplayName = "No password column")]
    [DataRow("password,protocols\n", 1, "expected a header row with address and password", DisplayName = "No address column")]
    [DataRow("address,password,quota\n", 1, "unknown column quota", DisplayName = "quota, BL-654's")]
    [DataRow("address,password,Address\n", 1, "column address is given twice", DisplayName = "A column twice")]
    [DataRow("address,password\nann@example.com\n", 2, "expected 2 fields", DisplayName = "Too few fields")]
    [DataRow("address,password\n\nann@example.com,a,b\n", 3, "expected 2 fields", DisplayName = "Too many fields")]
    [DataRow("address,password\n\"ann@example.com,a\n", 2, "a quoted field has no closing quote", DisplayName = "No closing quote")]
    [DataRow("address,password\n\"ann\"x@example.com,a\n", 2, "text follows a quoted field", DisplayName = "Text after a quote")]
    [DataRow("address,password\n,a\n", 2, "an empty field", DisplayName = "Empty address")]
    [DataRow("address,password\nann@example.com,\n", 2, "an empty field", DisplayName = "Empty password")]
    [DataRow("address,password\nann\t@example.com,a\n", 2, "a control character", DisplayName = "Control character in the address")]
    [DataRow("address,password\nann@example.com,a\0b\n", 2, "a control character", DisplayName = "U+0000 in the password")]
    [DataRow("address,password\na:b@example.com,a\n", 2, "the address holds a colon", DisplayName = "Colon in the address")]
    [DataRow("address,password,protocols\nann@example.com,a,\"imap,\"\n", 2, "an empty field", DisplayName = "Empty scheme")]
    [DataRow("address,password,protocols\nann@example.com,a,\"im\tap\"\n", 2, "a control character", DisplayName = "Control character in a scheme")]
    [DataRow("address,password,protocols\nann@example.com,a,nosuch\n", 2, "unknown scheme nosuch", DisplayName = "Unknown scheme")]
    [DataRow("address,password\nann@example.org,a\n", 2, "mailbox ann@example.org is not in any --mail-domain", DisplayName = "Another domain")]
    [DataRow("address,password\nann@example.com,a\nbob@example.com,\"x\ny\"\nANN@Example.com,c\n", 5, "mailbox ANN@Example.com is given twice", DisplayName = "An address twice, line of the record")]
    public void Read_MalformedRecord_IsRefusedNamingFileAndLine(string text, int line, string reason) =>
        Assert.AreEqual($"(2) Mailbox file {File}, line {line}: {reason}", Refused(text));

    [TestMethod]
    public void Read_NotUtf8_IsRefusedAtItsPhysicalLine() =>
        Assert.AreEqual(
            $"(2) Mailbox file {File}, line 3: not UTF-8",
            Refused([.. "address,password\nann@example.com,a\nbob@example.com,"u8, 0xFF, (byte)'\n']));

    [TestMethod]
    public void Read_NoMailDomain_RefusesEveryMailboxAsNeedingOne()
    {
        var reader = new MailboxFileReader(new SurlCommandLine(), []);

        var failure = reader.Read(File, "address,password\nann@example.com,a\n"u8.ToArray(), out _);

        Assert.AreEqual($"(2) Mailbox file {File}, line 2: mailbox ann@example.com needs --mail-domain", failure);
    }

    [TestMethod]
    public void Read_AddressOfAMailboxOption_IsGivenTwice()
    {
        var reader = new MailboxFileReader(WithDomain with { Mailboxes = [new("ann@example.com", "a", null)] }, []);

        var failure = reader.Read(File, "address,password\nAnn@Example.COM,a\n"u8.ToArray(), out _);

        Assert.AreEqual($"(2) Mailbox file {File}, line 2: mailbox Ann@Example.COM is given twice", failure);
    }

    [TestMethod]
    public void Read_AddressOfAUser_IsAlsoAUserAccount()
    {
        var reader = new MailboxFileReader(WithDomain with { Accounts = [new("ann@example.com", "x")] }, []);

        var failure = reader.Read(File, "address,password\nann@example.com,a\n"u8.ToArray(), out _);

        Assert.AreEqual($"(2) Mailbox file {File}, line 2: mailbox ann@example.com is also a --user account", failure);
    }

    [TestMethod]
    public void Read_AddressInAnEarlierFile_IsGivenTwice()
    {
        var reader = new MailboxFileReader(WithDomain, []);
        Assert.IsNull(reader.Read("a.csv", "address,password\nann@example.com,a\n"u8.ToArray(), out _));

        var failure = reader.Read("b.csv", "address,password\nann@example.com,b\n"u8.ToArray(), out _);

        Assert.AreEqual("(2) Mailbox file b.csv, line 2: mailbox ann@example.com is given twice", failure);
    }

    [TestMethod]
    public void Read_ExtendedScheme_IsAccepted()
    {
        var reader = new MailboxFileReader(WithDomain, ["coap"]);

        var failure = reader.Read(File, "address,password,protocols\nann@example.com,a,coap\n"u8.ToArray(), out var mailboxes);

        Assert.IsNull(failure);
        Assert.AreEqual("coap", mailboxes.Single().Protocols!.Single());
    }

    [TestMethod]
    public void Read_ExtendedSchemeNoRegisteredServerClaims_IsRefused() =>
        Assert.AreEqual(
            $"(2) Mailbox file {File}, line 2: unknown scheme coap",
            Refused("address,password,protocols\nann@example.com,a,coap\n"));

    [TestMethod]
    public void Read_AddressAndDomains_AreComparedNormalised()
    {
        var reader = new MailboxFileReader(new SurlCommandLine { MailDomains = ["Bücher.Example.", "not a domain"] }, []);

        var failure = reader.Read(File, "address,password\nAnn@BÜCHER.example,a\n"u8.ToArray(), out var mailboxes);

        Assert.IsNull(failure);
        Assert.AreEqual("Ann@xn--bcher-kva.example", mailboxes.Single().Address);
    }

    [TestMethod]
    public void Read_ProtocolsPastTheMemberBound_IsRefused() =>
        Assert.AreEqual(
            $"(2) Mailbox file {File}, line 2: more than 1000 members",
            Refused("address,password,protocols\nann@example.com,a,\"" + string.Join(',', Enumerable.Repeat("imap", 1001)) + "\"\n"));

    [TestMethod]
    public void Read_FileLargerThanSixteenMebibytes_IsRefused() =>
        Assert.AreEqual($"(2) Mailbox file {File} is larger than 16777216 bytes", Refused(new byte[MailboxFileReader.MaxFileBytes + 1]));

    [TestMethod]
    public void Read_MoreThanTenThousandMailboxes_IsRefused()
    {
        var text = new StringBuilder("address,password\n");
        for (var index = 0; index <= MailboxFileReader.MaxMailboxes; index++)
        {
            text.Append('u').Append(index).Append("@example.com,s\n");
        }

        Assert.AreEqual($"(2) Mailbox file {File} holds more than 10000 mailboxes", Refused(text.ToString()));
    }

    [TestMethod]
    public void Read_MailboxesAcrossFilesPastTenThousand_IsRefused()
    {
        var reader = new MailboxFileReader(
            WithDomain with { Mailboxes = [.. Enumerable.Range(0, MailboxFileReader.MaxMailboxes).Select(index => new ConfiguredMailbox($"m{index}@example.com", "s", null))] },
            []);

        var failure = reader.Read(File, "address,password\nann@example.com,a\n"u8.ToArray(), out _);

        Assert.AreEqual($"(2) Mailbox file {File} holds more than 10000 mailboxes", failure);
    }

    [TestMethod]
    public void Read_Refusal_NeverHoldsThePassword() =>
        Assert.DoesNotContain("hunter2", Refused("address,password,protocols\nann@example.com,hunter2,nosuch\n"));

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailboxFileReader(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailboxFileReader(WithDomain, null!));
    }

    [TestMethod]
    public void Read_NullArgument_Throws()
    {
        var reader = new MailboxFileReader(WithDomain, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => reader.Read(null!, [], out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => reader.Read(File, null!, out _));
    }

    private static IReadOnlyList<ConfiguredMailbox> Accepted(string text)
    {
        var failure = new MailboxFileReader(WithDomain, []).Read(File, Encoding.UTF8.GetBytes(text), out var mailboxes);

        Assert.IsNull(failure);
        return mailboxes;
    }

    private static string Refused(string text) => Refused(Encoding.UTF8.GetBytes(text));

    private static string Refused(byte[] content)
    {
        var failure = new MailboxFileReader(WithDomain, []).Read(File, content, out var mailboxes);

        Assert.IsNotNull(failure);
        Assert.IsEmpty(mailboxes);
        return failure;
    }
}
