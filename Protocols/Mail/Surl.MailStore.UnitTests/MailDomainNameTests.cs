namespace Surl.MailStore;

[TestClass]
public sealed class MailDomainNameTests
{
    [TestMethod]
    [DataRow("example.com", "example.com")]
    [DataRow("Example.COM", "example.com")]
    [DataRow("example.com.", "example.com")]
    [DataRow("Bücher.Example", "xn--bcher-kva.example")]
    [DataRow("xn--bcher-kva.example", "xn--bcher-kva.example")]
    public void TryNormalise_DomainName_IsItsAsciiLowerCaseFormWithoutTheTrailingDot(string domain, string expected)
    {
        Assert.IsTrue(MailDomainName.TryNormalise(domain, out var asciiDomain));
        Assert.AreEqual(expected, asciiDomain);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("[127.0.0.1]")]
    [DataRow("a..b")]
    [DataRow("exa mple.com")]
    [DataRow("under_score.example")]
    public void TryNormalise_NoDomainName_ReturnsFalse(string domain)
    {
        Assert.IsFalse(MailDomainName.TryNormalise(domain, out var asciiDomain));
        Assert.AreEqual(string.Empty, asciiDomain);
    }

    [TestMethod]
    public void TryNormalise_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MailDomainName.TryNormalise(null!, out _));
    }
}
