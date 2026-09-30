namespace Surl.MailStore;

[TestClass]
public sealed class MailboxNameTests
{
    [TestMethod]
    [DataRow("inbox", "INBOX")]
    [DataRow("InBoX", "INBOX")]
    [DataRow("Sent", "Sent")]
    [DataRow("inbox.Sent", "inbox.Sent")]
    public void Canonical_MatchesInboxInAnyCaseAndNothingElse(string name, string expected)
    {
        Assert.AreEqual(expected, MailboxName.Canonical(name));
    }

    [TestMethod]
    [DataRow("a")]
    [DataRow("Archive/2026")]
    [DataRow("Entwürfe")]
    [DataRow("\u0080 C1 controls are not refused")]
    [DataRow("📧 surrogate pair")]
    public void IsValid_ValidName_IsTrue(string name)
    {
        Assert.IsTrue(MailboxName.IsValid(name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("tab\there")]
    [DataRow("nul\u0000")]
    [DataRow("del\u007F")]
    public void IsValid_InvalidName_IsFalse(string name)
    {
        Assert.IsFalse(MailboxName.IsValid(name));
    }

    [TestMethod]
    public void IsValid_LoneSurrogate_IsFalse()
    {
        // Built in code: an attribute argument cannot carry a lone surrogate intact.
        Assert.IsFalse(MailboxName.IsValid("lone " + (char)0xD83D + " surrogate"));
    }

    [TestMethod]
    public void IsValid_NameOf1024Utf8Bytes_IsTrue()
    {
        Assert.IsTrue(MailboxName.IsValid(new string('a', 1022) + "é"));
    }

    [TestMethod]
    public void IsValid_NameOf1025Utf8Bytes_IsFalse()
    {
        Assert.IsFalse(MailboxName.IsValid(new string('a', 1023) + "é"));
    }

    [TestMethod]
    public void IsValid_NameOf1025Characters_IsFalse()
    {
        Assert.IsFalse(MailboxName.IsValid(new string('a', 1025)));
    }
}
