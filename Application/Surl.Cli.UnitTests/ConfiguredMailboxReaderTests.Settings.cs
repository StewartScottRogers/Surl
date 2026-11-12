namespace Surl.Cli;

public sealed partial class ConfiguredMailboxReaderTests
{
    [TestMethod]
    public void Read_NoSettings_HasNoQuotaAndNoFolders()
    {
        var mailbox = AcceptedWithSettings("ann@example.com:pw");

        Assert.IsNull(mailbox.QuotaBytes);
        Assert.IsEmpty(mailbox.Folders);
        Assert.AreEqual(new ConfiguredMailbox("ann@example.com", "pw", null), mailbox);
    }

    [TestMethod]
    [DataRow("1", 1L, DisplayName = "One byte")]
    [DataRow("10M", 10L * 1024 * 1024, DisplayName = "Suffix")]
    [DataRow("1.5k", 1536L, DisplayName = "Decimal")]
    public void Read_Quota_IsReadAsBytes(string quota, long bytes) =>
        Assert.AreEqual(bytes, AcceptedWithSettings($"ann@example.com:pw;quota={quota}").QuotaBytes);

    [TestMethod]
    public void Read_Folders_AreKeptInOrderAsWritten() =>
        CollectionAssert.AreEqual(
            new[] { "Archive", "Work/2026", "INBOX", "a,b" },
            AcceptedWithSettings("ann@example.com:pw;folders=Archive,Work/2026,INBOX,a%2Cb").Folders.ToArray());

    [TestMethod]
    public void Read_EveryKey_IsReadInAnyOrderAndCase()
    {
        var mailbox = AcceptedWithSettings("Ann@Example.com:pw;FOLDERS=Sent;Quota=2k;protocols=imap");

        Assert.AreEqual("Ann@example.com", mailbox.Address);
        Assert.AreEqual(2048L, mailbox.QuotaBytes);
        CollectionAssert.AreEqual(new[] { "Sent" }, mailbox.Folders.ToArray());
        CollectionAssert.AreEqual(new[] { "imap" }, mailbox.Protocols!.ToArray());
    }

    [TestMethod]
    [DataRow("ann@example.com:pw;quota=0", "key quota needs a value above 0", DisplayName = "Quota 0")]
    [DataRow("ann@example.com:pw;quota=0.5", "key quota needs a value above 0", DisplayName = "Quota below one byte")]
    [DataRow("ann@example.com:pw;quota=lots", OptionArgumentReader.NotANumber, DisplayName = "Quota not a number")]
    [DataRow("ann@example.com:pw;quota=99999999999999999999", OptionArgumentReader.TooLarge, DisplayName = "Quota too large")]
    [DataRow("ann@example.com:pw;quota=", "key quota needs a value", DisplayName = "Quota empty")]
    [DataRow("ann@example.com:pw;quota=1;quota=2", "key quota is given twice", DisplayName = "Quota twice")]
    [DataRow("ann@example.com:pw;folders=Sent,Sent", "member Sent is given twice", DisplayName = "Folder twice")]
    [DataRow("ann@example.com:pw;folders=Sent,", "an empty field", DisplayName = "Empty folder")]
    [DataRow("ann@example.com:pw;folders=a%09b", "a control character", DisplayName = "Control character in a folder")]
    [DataRow("ann@example.com:pw;folders=a;nosuch=1", "unknown key nosuch", DisplayName = "Unknown key")]
    public void Read_RefusedSettingsItem_GivesTheReasonAndAnEmptyMailbox(string argument, string reason)
    {
        var failure = ConfiguredMailboxReader.Read(argument, out var mailbox);

        Assert.AreEqual(reason, failure);
        Assert.AreSame(ConfiguredMailboxReader.Empty, mailbox);
    }

    [TestMethod]
    public void Read_FolderPastTheStoreBound_IsRefused() =>
        Assert.AreEqual(
            "a folder name is longer than 1024 bytes",
            ConfiguredMailboxReader.Read("ann@example.com:pw;folders=" + new string('é', 513), out _));

    [TestMethod]
    public void Read_RefusalOfAnyKey_NeverHoldsThePassword()
    {
        foreach (var settings in new[] { ";quota=0", ";quota=x", ";folders=a,a", ";folders=" + new string('x', 1025) })
        {
            var failure = ConfiguredMailboxReader.Read("ann@example.com:hunter2" + settings, out _);

            Assert.IsNotNull(failure);
            Assert.DoesNotContain("hunter2", failure);
        }
    }

    [TestMethod]
    public void Build_LoneSurrogateFolder_IsNotUtf8() =>
        Assert.AreEqual(
            "a folder name is not UTF-8",
            ConfiguredMailboxReader.Build("ann@example.com", "pw", null, null, ["a\uD800"], ConfiguredMailboxReader.IsAnySurlScheme, out _));

    [TestMethod]
    public void Reading_IsNamedByTheWholeItemShape()
    {
        Assert.AreEqual(
            "<address>:<password>[;protocols=<scheme>,...][;quota=<bytes>][;folders=<name>,...]",
            ConfiguredMailboxReader.Reading.Type.Name);
        Assert.IsNull(ConfiguredMailboxReader.Reading.Read("ann@example.com:pw;quota=1", out _));
    }

    private static ConfiguredMailbox AcceptedWithSettings(string argument)
    {
        var failure = ConfiguredMailboxReader.Read(argument, out var mailbox);

        Assert.IsNull(failure);
        return mailbox;
    }
}
