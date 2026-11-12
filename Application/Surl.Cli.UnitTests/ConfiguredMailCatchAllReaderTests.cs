namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredMailCatchAllReader"/> reading <c>&lt;domain&gt;=&lt;address&gt;</c> items (ADR-0101
/// decisions 3 to 5, ADR-0102 decision 1.3).
/// </summary>
[TestClass]
public sealed class ConfiguredMailCatchAllReaderTests
{
    [TestMethod]
    public void Read_DomainAndAddress_KeepsThemAsWritten()
    {
        var failure = ConfiguredMailCatchAllReader.Read("Example.COM.=postmaster@example.com", out var catchAll);

        Assert.IsNull(failure);
        Assert.AreEqual(new ConfiguredMailCatchAll("Example.COM.", "postmaster@example.com"), catchAll);
    }

    [TestMethod]
    [DataRow("example.com", "expected <domain>=<address>", DisplayName = "No separator")]
    [DataRow("=a@example.com", "the name is empty", DisplayName = "No domain")]
    [DataRow("example.com=", "an empty field", DisplayName = "No address")]
    [DataRow("exa_mple.com=a@example.com", "not a domain name", DisplayName = "Domain not a domain")]
    [DataRow("example.com=postmaster", "not a mail address", DisplayName = "Target not an address")]
    [DataRow("example.com=a%09@example.com", "a control character", DisplayName = "Control character")]
    [DataRow("example.com=a@example.com;readonly", "unknown flag readonly", DisplayName = "Unknown flag")]
    public void Read_RefusedItem_GivesTheReasonAndAnEmptyCatchAll(string argument, string reason)
    {
        var failure = ConfiguredMailCatchAllReader.Read(argument, out var catchAll);

        Assert.AreEqual(reason, failure);
        Assert.AreSame(ConfiguredMailCatchAllReader.Empty, catchAll);
    }

    [TestMethod]
    public void CatchAllDomainGivenTwice_NamesTheDomain() =>
        Assert.AreEqual("catch-all domain example.com is given twice", ConfiguredMailCatchAllReader.CatchAllDomainGivenTwice("example.com"));

    [TestMethod]
    public void Reading_IsNamedByTheItemShape()
    {
        Assert.AreEqual("<domain>=<address>", ConfiguredMailCatchAllReader.Reading.Type.Name);
        Assert.IsNull(ConfiguredMailCatchAllReader.Reading.Read("example.com=a@example.com", out _));
    }
}
