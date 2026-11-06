namespace Surl.Content;

[TestClass]
public sealed class ContentCallerTests
{
    [TestMethod]
    public void ForAccount_KeepsTheAccountAndScheme()
    {
        ContentCaller caller = ContentCaller.ForAccount("alice", "sftp");

        Assert.AreEqual("alice", caller.AccountName);
        Assert.AreEqual("sftp", caller.Scheme);
        Assert.IsFalse(caller.IsAnonymous);
    }

    [TestMethod]
    public void Anonymous_HasNoAccountName()
    {
        ContentCaller caller = ContentCaller.Anonymous("tftp");

        Assert.IsNull(caller.AccountName);
        Assert.AreEqual("tftp", caller.Scheme);
        Assert.IsTrue(caller.IsAnonymous);
    }

    [TestMethod]
    [DataRow("", "sftp")]
    [DataRow("alice", "")]
    public void ForAccount_AnEmptyAccountOrScheme_Throws(string accountName, string scheme)
    {
        Assert.ThrowsExactly<ArgumentException>(() => ContentCaller.ForAccount(accountName, scheme));
    }

    [TestMethod]
    public void Anonymous_AnEmptyScheme_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ContentCaller.Anonymous(""));
    }
}
