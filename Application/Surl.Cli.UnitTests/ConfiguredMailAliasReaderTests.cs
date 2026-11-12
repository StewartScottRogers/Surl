namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredMailAliasReader"/> reading <c>&lt;address&gt;=&lt;address&gt;,...</c> items (ADR-0101
/// decisions 3 to 5, ADR-0102 decision 1.3).
/// </summary>
[TestClass]
public sealed class ConfiguredMailAliasReaderTests
{
    [TestMethod]
    public void Read_AliasAndTargets_KeepsThemAsWrittenAndDecoded()
    {
        var failure = ConfiguredMailAliasReader.Read("Sales@Example.COM=ann@example.com,b%2Cob@example.com", out var alias);

        Assert.IsNull(failure);
        Assert.AreEqual("Sales@Example.COM", alias.Address);
        CollectionAssert.AreEqual(new[] { "ann@example.com", "b,ob@example.com" }, alias.Targets.ToArray());
    }

    [TestMethod]
    [DataRow("sales@example.com", "expected <address>=<address>,...", DisplayName = "No separator")]
    [DataRow("=ann@example.com", "the name is empty", DisplayName = "No alias")]
    [DataRow("sales@example.com=", "an empty field", DisplayName = "No target")]
    [DataRow("sales=ann@example.com", "not a mail address", DisplayName = "Alias not an address")]
    [DataRow("sales@exa_mple.com=ann@example.com", "not a domain name", DisplayName = "Alias domain not a domain")]
    [DataRow("sales@example.com=ann", "not a mail address", DisplayName = "Target not an address")]
    [DataRow("sales@example.com=ann@example.com,ann@EXAMPLE.com", "member ann@EXAMPLE.com is given twice", DisplayName = "Target twice")]
    [DataRow("sales@example.com=ann@example.com;x=1", "unknown key x", DisplayName = "Unknown key")]
    public void Read_RefusedItem_GivesTheReasonAndAnEmptyAlias(string argument, string reason)
    {
        var failure = ConfiguredMailAliasReader.Read(argument, out var alias);

        Assert.AreEqual(reason, failure);
        Assert.AreSame(ConfiguredMailAliasReader.Empty, alias);
    }

    [TestMethod]
    public void AliasGivenTwice_NamesTheAddress() =>
        Assert.AreEqual("alias sales@example.com is given twice", ConfiguredMailAliasReader.AliasGivenTwice("sales@example.com"));

    [TestMethod]
    public void Reading_IsNamedByTheItemShape()
    {
        Assert.AreEqual("<address>=<address>,...", ConfiguredMailAliasReader.Reading.Type.Name);
        Assert.IsNull(ConfiguredMailAliasReader.Reading.Read("a@example.com=b@example.com", out _));
    }
}
