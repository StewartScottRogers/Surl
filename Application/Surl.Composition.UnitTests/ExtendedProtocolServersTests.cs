using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Extended.Protocol.Nats;
using Surl.Extended.Protocol.S3;
using Surl.Protocol.Mqtt;

namespace Surl.Composition;

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
    public void Compose_ThisBuild_IsTheNatsServerForNatsAndNatssThenTheS3ServerForS3()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var servers = ExtendedProtocolServers.Compose(services);

        Assert.HasCount(2, servers);
        Assert.IsInstanceOfType<NatsServer>(servers[0]);
        Assert.IsInstanceOfType<S3ProtocolServer>(servers[1]);
        CollectionAssert.AreEqual(new[] { "nats", "natss", "s3" }, servers.SelectMany(server => server.Schemes).ToArray());
        CollectionAssert.AreEqual(
            new[] { "nats", "natss", "s3" }, ExtendedProtocolServers.Registrations.SelectMany(registration => registration.Schemes).ToArray());
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheNatsServerOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var natsServer = ExtendedProtocolServers.Compose(services).OfType<NatsServer>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var bus = ComposedServerFields.Single<MessageBus.MessageBus>(natsServer);
        Assert.AreSame(services.ComposedServices.MessageBus, bus);
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer));
    }

    [TestMethod]
    [DataRow(true, 40)]
    [DataRow(false, 0)]
    public void Compose_ThisBuild_BuildsTheNatsServerWithTheIdleTimeoutAndAllowAnonymous(bool allowAnonymous, int idleTimeoutSeconds)
    {
        var services = new ExtendedProtocolServerServices(ComposeServices(allowAnonymous, TimeSpan.FromSeconds(idleTimeoutSeconds)));

        var natsServer = ExtendedProtocolServers.Compose(services).OfType<NatsServer>().Single();

        var options = ComposedServerFields.Single<NatsServerOptions>(natsServer);
        Assert.AreEqual(TimeSpan.FromSeconds(idleTimeoutSeconds), options.IdleTimeout);
        Assert.AreEqual(allowAnonymous, options.AllowsAnonymous);
    }
    [TestMethod]
    public void Compose_ThisBuild_BuildsTheS3ServerOverTheServicesContentStoreAndIndex()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var server = ExtendedProtocolServers.Compose(services).OfType<S3ProtocolServer>().Single();

        Assert.AreSame(services.ComposedServices.ContentStore, ComposedServerFields.Single<ContentStore>(server));
        Assert.AreSame(services.ComposedServices.ObjectMetadataIndex, ComposedServerFields.Single<ObjectMetadataIndex>(server));
    }

    [TestMethod]
    public void Compose_NullServices_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtendedProtocolServers.Compose(null!));
    }

    private static ComposedProtocolServices ComposeServices(bool allowAnonymous, TimeSpan idleTimeout)
    {
        var unserved = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        return new ComposedProtocolServices(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            new AuthenticationPolicy(
                new AuthenticationSettings(new AccountBook([]), allowAnonymous, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted),
                [],
                TimeProvider.System),
            TimeProvider.System,
            unserved.SshHostKeys,
            unserved.SshAlgorithms,
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false)
        {
            IdleTimeout = idleTimeout,
        };
    }
}
