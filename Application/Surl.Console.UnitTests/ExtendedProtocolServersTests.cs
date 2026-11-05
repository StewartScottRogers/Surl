namespace Surl.Console;

[TestClass]
public sealed class ExtendedProtocolServersTests
{
    [TestMethod]
    public void AreTurnedOn_SwitchUnset_IsTrue()
    {
        Assert.IsTrue(ExtendedProtocolServers.AreTurnedOn(FakeEnvironmentVariableReader.Empty));
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("1")]
    [DataRow("false", DisplayName = "Only 0 turns them off")]
    [DataRow(" 0", DisplayName = "0 with a space is another value")]
    public void AreTurnedOn_SwitchAtAnotherValue_IsTrue(string value)
    {
        var environment = new FakeEnvironmentVariableReader(new Dictionary<string, string> { ["SURL_EXTENSIONS"] = value });

        Assert.IsTrue(ExtendedProtocolServers.AreTurnedOn(environment));
    }

    [TestMethod]
    public void AreTurnedOn_SwitchAtZero_IsFalse()
    {
        var environment = new FakeEnvironmentVariableReader(new Dictionary<string, string> { ["SURL_EXTENSIONS"] = "0" });

        Assert.IsFalse(ExtendedProtocolServers.AreTurnedOn(environment));
    }

    [TestMethod]
    public void Compose_ThisBuild_HasNoExtendedServerYet()
    {
        var services = new ExtendedProtocolServerServices(
            CommandLineRunner.ComposeContentStore(new Cli.SurlCommandLine(), TimeProvider.System),
            AuthenticationComposition.ComposeWithoutAccounts(TimeProvider.System),
            IsTlsUpgradeAvailable: false);

        var servers = ExtendedProtocolServers.Compose(services);

        Assert.IsEmpty(servers);
    }
}
