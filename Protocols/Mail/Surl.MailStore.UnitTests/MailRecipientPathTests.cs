namespace Surl.MailStore;

[TestClass]
public sealed class MailRecipientPathTests
{
    [TestMethod]
    [DataRow("<alice@example.com>", "alice")]
    [DataRow("alice@example.com", "alice")]
    [DataRow("<@relay.one,@relay.two:alice@example.com>", "alice")]
    [DataRow("<first.last@[192.0.2.1]>", "first.last")]
    [DataRow("<a!#$%&'*+-/=?^_`{|}~z@x>", "a!#$%&'*+-/=?^_`{|}~z")]
    [DataRow("<\"alice smith\"@example.com>", "alice smith")]
    [DataRow("<\"a\\\"b\\\\c\"@example.com>", "a\"b\\c")]
    [DataRow("<\"\"@example.com>", "")]
    [DataRow("<jörg@bücher.example>", "jörg")]
    [DataRow("<Postmaster>", "Postmaster")]
    [DataRow("postmaster", "postmaster")]
    public void TryReadLocalPart_ValidPath_ReadsTheLocalPart(string path, string expected)
    {
        Assert.IsTrue(MailRecipientPath.TryReadLocalPart(path, out var localPart));
        Assert.AreEqual(expected, localPart);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<>")]
    [DataRow("<")]
    [DataRow("<alice@example.com")]
    [DataRow("<alice>")]
    [DataRow("alice@")]
    [DataRow("@relay.one alice@example.com")]
    [DataRow("<.alice@example.com>")]
    [DataRow("<alice.@example.com>")]
    [DataRow("<al..ice@example.com>")]
    [DataRow("<al ice@example.com>")]
    [DataRow("<al(ice@example.com>")]
    [DataRow("<\"alice@example.com>")]
    [DataRow("\"alice\\")]
    [DataRow("<\"al\u0001ice\"@example.com>")]
    [DataRow("<\"al\u007Fice\"@example.com>")]
    [DataRow("<\"alice\"x@example.com>")]
    [DataRow("<\"alice\">")]
    [DataRow("<alice@exa mple.com>")]
    [DataRow("<alice@example\u007F.com>")]
    [DataRow("<alice@<example.com>")]
    [DataRow("<alice@example.com>>")]
    [DataRow("<alice@one@two>")]
    public void TryReadLocalPart_InvalidPath_ReturnsFalse(string path)
    {
        Assert.IsFalse(MailRecipientPath.TryReadLocalPart(path, out _));
    }

    [TestMethod]
    [DataRow("<@relay.one:alice@Example.COM>", "alice", "Example.COM")]
    [DataRow("<\"a b\"@[192.0.2.1]>", "a b", "[192.0.2.1]")]
    [DataRow("<Postmaster>", "Postmaster", null)]
    public void TryReadAddress_ValidPath_ReadsTheLocalPartAndTheDomainAsSent(string path, string expectedLocalPart, string? expectedDomain)
    {
        Assert.IsTrue(MailRecipientPath.TryReadAddress(path, out var localPart, out var domain));
        Assert.AreEqual(expectedLocalPart, localPart);
        Assert.AreEqual(expectedDomain, domain);
    }

    [TestMethod]
    public void TryReadAddress_InvalidPath_ReturnsFalse()
    {
        Assert.IsFalse(MailRecipientPath.TryReadAddress("<alice@example.com", out var localPart, out var domain));
        Assert.AreEqual(string.Empty, localPart);
        Assert.IsNull(domain);
    }
}
