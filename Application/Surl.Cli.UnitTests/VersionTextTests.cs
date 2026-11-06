namespace Surl.Cli;

[TestClass]
public sealed class VersionTextTests
{
    [TestMethod]
    public void Compose_WritesVersionRuntimeAndSortedSchemes()
    {
        var text = VersionText.Compose("1.2.3", "win-x64", ["https", "http", "dict"]);

        Assert.AreEqual($"surl 1.2.3 (win-x64){Environment.NewLine}Protocols: dict http https{Environment.NewLine}", text);
    }

    [TestMethod]
    [DataRow("1.2.3+0f04d1d", "1.2.3", DisplayName = "Commit after + removed")]
    [DataRow("2.0.0-beta.1+abc+def", "2.0.0-beta.1", DisplayName = "Everything from the first + removed")]
    [DataRow("+0f04d1d", "1.0.0", DisplayName = "Nothing before +")]
    [DataRow("", "1.0.0", DisplayName = "Empty")]
    [DataRow(null, "1.0.0", DisplayName = "No version set")]
    public void Compose_Version_IsTheInformationalVersionLessBuildMetadata(string? informationalVersion, string expected) =>
        Assert.StartsWith($"surl {expected} (linux-arm64){Environment.NewLine}", VersionText.Compose(informationalVersion, "linux-arm64", ["http"]));

    [TestMethod]
    public void Compose_SchemesInOrdinalOrder()
    {
        var text = VersionText.Compose("1.0.0", "osx-arm64", ["wss", "ws", "tftp", "https", "http"]);

        Assert.EndsWith($"Protocols: http https tftp ws wss{Environment.NewLine}", text);
    }

    [TestMethod]
    public void Compose_NoServedScheme_WritesProtocolsWithNoTrailingSpace() =>
        Assert.EndsWith($"{Environment.NewLine}Protocols:{Environment.NewLine}", VersionText.Compose("1.0.0", "win-x64", []));

    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => VersionText.Compose("1.0.0", null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => VersionText.Compose("1.0.0", "win-x64", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => VersionText.Compose("1.0.0", "win-x64", [], null!));
    }

    [TestMethod]
    public void Compose_ExtendedSchemes_FollowProtocolsOnAnExtendedLineInOrdinalOrder()
    {
        var text = VersionText.Compose("1.0.0", "win-x64", ["http", "dict"], ["s3", "redis"]);

        var newLine = Environment.NewLine;
        Assert.AreEqual($"surl 1.0.0 (win-x64){newLine}Protocols: dict http{newLine}Extended: redis s3{newLine}", text);
    }

    [TestMethod]
    public void Compose_NoExtendedScheme_WritesNoExtendedLine()
    {
        var text = VersionText.Compose("1.0.0", "win-x64", ["http"], []);

        Assert.AreEqual(VersionText.Compose("1.0.0", "win-x64", ["http"]), text);
        Assert.DoesNotContain("Extended:", text);
    }
}
