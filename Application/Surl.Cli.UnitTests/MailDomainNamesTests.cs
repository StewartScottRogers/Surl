namespace Surl.Cli;

/// <summary>
/// <see cref="MailDomainNames"/> reading mail domain names and addresses into their compared form
/// (ADR-0102 decisions 1.1 and 1.2).
/// </summary>
[TestClass]
public sealed class MailDomainNamesTests
{
    [TestMethod]
    [DataRow("example.com", "example.com", DisplayName = "Already ASCII lower case")]
    [DataRow("EXAMPLE.Com", "example.com", DisplayName = "Case folded")]
    [DataRow("example.com.", "example.com", DisplayName = "One trailing dot dropped")]
    [DataRow("Bücher.Example.", "xn--bcher-kva.example", DisplayName = "U-label converted to ASCII")]
    [DataRow("XN--BCHER-KVA.example", "xn--bcher-kva.example", DisplayName = "A-label case folded")]
    public void ReadDomain_DomainName_GivesTheAsciiLowerCaseForm(string name, string expected)
    {
        var failure = MailDomainNames.ReadDomain(name, out var domain);

        Assert.IsNull(failure);
        Assert.AreEqual(expected, domain);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow(".", DisplayName = "Only a dot")]
    [DataRow("example.com..", DisplayName = "Two trailing dots")]
    [DataRow("exa_mple.com", DisplayName = "Underscore under STD3 rules")]
    [DataRow("exa mple.com", DisplayName = "Space")]
    [DataRow("a..b", DisplayName = "Empty label")]
    public void ReadDomain_NotADomainName_IsRefused(string name)
    {
        var failure = MailDomainNames.ReadDomain(name, out var domain);

        Assert.AreEqual("not a domain name", failure);
        Assert.IsEmpty(domain);
    }

    [TestMethod]
    [DataRow("Ann@Example.COM", "Ann@example.com", DisplayName = "Local part kept as written")]
    [DataRow("ann@bücher.example.", "ann@xn--bcher-kva.example", DisplayName = "IDN domain")]
    [DataRow("a@b@Example.com", "a@b@example.com", DisplayName = "Split at the last @")]
    public void ReadAddress_MailAddress_NormalisesItsDomain(string address, string expected)
    {
        var failure = MailDomainNames.ReadAddress(address, out var normalised);

        Assert.IsNull(failure);
        Assert.AreEqual(expected, normalised);
    }

    [TestMethod]
    [DataRow("ann", "not a mail address", DisplayName = "No @")]
    [DataRow("@example.com", "not a mail address", DisplayName = "Empty local part")]
    [DataRow("ann@", "not a mail address", DisplayName = "Empty domain")]
    [DataRow("ann@exa_mple.com", "not a domain name", DisplayName = "Domain refused")]
    public void ReadAddress_NotAMailAddress_IsRefused(string address, string reason)
    {
        var failure = MailDomainNames.ReadAddress(address, out var normalised);

        Assert.AreEqual(reason, failure);
        Assert.IsEmpty(normalised);
    }

    [TestMethod]
    public void Read_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MailDomainNames.ReadDomain(null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => MailDomainNames.ReadAddress(null!, out _));
    }
}
