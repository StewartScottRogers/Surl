using System.Net;
using Surl.Protocol.Abstractions;

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
    public void WithExchange_KeepsTheCallerAndNamesTheExchangeWhileTheOriginalNamesNone()
    {
        ContentCaller original = ContentCaller.ForAccount("alice", "sftp");

        ContentCaller onSeven = original.WithExchange(7);

        Assert.IsNull(original.ExchangeId);
        Assert.AreEqual(7L, onSeven.ExchangeId);
        Assert.AreEqual("alice", onSeven.AccountName);
        Assert.AreEqual("sftp", onSeven.Scheme);
    }

    [TestMethod]
    public void ForExchange_TakesTheIdSchemeAndListenUrlFromTheExchangeAndKeepsThemThroughWithExchange()
    {
        var exchange = new ExchangeContext(3, new ListenUrl("sftp", "::1", 22).WithBoundPort(2222), new IPEndPoint(IPAddress.IPv6Loopback, 2222), new IPEndPoint(IPAddress.IPv6Loopback, 50000), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);

        ContentCaller caller = ContentCaller.ForExchange(exchange, "alice").WithExchange(8);

        Assert.AreEqual("alice", caller.AccountName);
        Assert.AreEqual("sftp", caller.Scheme);
        Assert.AreEqual(8L, caller.ExchangeId);
        Assert.AreEqual("sftp://[::1]:2222/", caller.ListenUrl);
        Assert.IsNull(ContentCaller.ForAccount("alice", "sftp").ListenUrl);
    }

    [TestMethod]
    public void ForExchange_NullExchange_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentCaller.ForExchange(null!, "alice"));
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
