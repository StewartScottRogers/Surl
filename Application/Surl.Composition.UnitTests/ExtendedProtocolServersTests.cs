using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.Nats;
using Surl.Extended.Protocol.S3;
using Surl.Protocol.Abstractions;
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
    public void Compose_ThisBuild_IsTheDnsUdpAndTcpServersThenTheNatsServerThenTheS3Server()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var servers = ExtendedProtocolServers.Compose(services);

        Assert.HasCount(4, servers);
        Assert.IsInstanceOfType<DnsUdpProtocolServer>(servers[0]);
        Assert.IsInstanceOfType<DnsTcpProtocolServer>(servers[1]);
        Assert.IsInstanceOfType<NatsServer>(servers[2]);
        Assert.IsInstanceOfType<S3ProtocolServer>(servers[3]);
        CollectionAssert.AreEqual(new[] { "dns", "dns", "nats", "natss", "s3" }, servers.SelectMany(server => server.Schemes).ToArray());
        CollectionAssert.AreEqual(
            new[] { "dns", "dns", "nats", "natss", "s3" }, ExtendedProtocolServers.Registrations.SelectMany(registration => registration.Schemes).ToArray());
    }

    [TestMethod]
    public void Compose_ThisBuild_RegistersDnsAsADatagramAndAConnectionServerSoOneListenUrlBindsUdpAndTcpOnOnePort()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var dnsServers = ExtendedProtocolServers.Compose(services).Where(server => server.Schemes.Contains("dns")).ToArray();

        Assert.HasCount(1, dnsServers.OfType<IDatagramProtocolServer>());
        Assert.HasCount(1, dnsServers.OfType<IConnectionProtocolServer>());
        Assert.HasCount(2, dnsServers);
    }

    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchAtZero_ComposesNoDnsServer()
    {
        var servers = SurlServerComposition.ComposeExtendedProtocolServers(
            areExtensionsTurnedOn: false, composeExtendedProtocolServers: null, SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System));

        Assert.IsEmpty(servers);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Compose_AllowsZoneTransferSetOrNot_ReachesBothDnsServersOverTheOneZoneSet(bool allowsZoneTransfer)
    {
        var unserved = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        var zones = new DnsZoneSet();
        var services = new ExtendedProtocolServerServices(new ComposedProtocolServices(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex) { DnsZones = zones },
            unserved.AuthenticationPolicy,
            TimeProvider.System,
            unserved.SshHostKeys,
            unserved.SshAlgorithms,
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false)
        {
            AllowsZoneTransfer = allowsZoneTransfer,
        });

        var servers = ExtendedProtocolServers.Compose(services);

        foreach (var server in new IProtocolServer[] { servers.OfType<DnsUdpProtocolServer>().Single(), servers.OfType<DnsTcpProtocolServer>().Single() })
        {
            var answerer = ComposedServerFields.Single<DnsQueryAnswerer>(server);
            Assert.AreSame(zones, ComposedServerFields.Single<DnsZoneSet>(answerer));
            Assert.AreEqual(allowsZoneTransfer, AllowZoneTransferField(answerer), server.GetType().Name);
        }
    }

    [TestMethod]
    public void ComposedProtocolServices_AllowsZoneTransferUnset_IsOff()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        Assert.IsFalse(services.ComposedServices.AllowsZoneTransfer);
        Assert.IsEmpty(services.ComposedServices.DnsZones.Apexes);
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

    private static bool AllowZoneTransferField(DnsQueryAnswerer answerer) =>
        (bool)typeof(DnsQueryAnswerer)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(bool))
            .GetValue(answerer)!;

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
