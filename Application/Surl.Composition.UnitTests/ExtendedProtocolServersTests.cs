using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Extended.Protocol.Amqp0;
using Surl.Extended.Protocol.Amqp1;
using Surl.Extended.Protocol.AzureStorage;
using Surl.Extended.Protocol.Coap;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.GoogleCloudStorage;
using Surl.Extended.Protocol.Kafka;
using Surl.Extended.Protocol.McpServer;
using Surl.Extended.Protocol.Mqtt5;
using Surl.Extended.Protocol.Nats;
using Surl.Extended.Protocol.Stomp;
using Surl.Extended.Protocol.Redis;
using Surl.Extended.Protocol.S3;
using Surl.Extended.Protocol.SignalR;
using Surl.Extended.Protocol.SocketIo;
using Surl.Extended.Protocol.WebTransport;
using Surl.Extended.Protocol.Xmpp;
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
    public void Compose_ThisBuild_IsTheAmqp0ThenTheAmqp1ContainerThenTheDnsUdpAndTcpServersThenTheGsServerThenTheMcpServerThenTheNatsServerThenTheS3ServerThenTheSocketIoServerThenTheWebTransportServerAfterTheCoapServers()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var servers = ExtendedProtocolServers.Compose(services);

        Assert.HasCount(23, servers);
        Assert.IsInstanceOfType<Amqp0Broker>(servers[0]);
        Assert.IsInstanceOfType<Amqp1Container>(servers[1]);
        Assert.IsInstanceOfType<AzureStorageProtocolServer>(servers[2]);
        Assert.IsInstanceOfType<CoapProtocolServer>(servers[3]);
        CollectionAssert.AreEqual(new[] { "coaps" }, servers[4].Schemes.ToArray());
        Assert.IsInstanceOfType<DnsUdpProtocolServer>(Assert.IsInstanceOfType<DnsDatagramRoutingProtocolServer>(servers[5]).UdpServer);
        Assert.IsInstanceOfType<DnsTcpProtocolServer>(Assert.IsInstanceOfType<DnsConnectionRoutingProtocolServer>(servers[6]).StreamServer);
        Assert.IsInstanceOfType<GoogleCloudStorageProtocolServer>(servers[7]);
        Assert.IsInstanceOfType<KafkaBroker>(servers[8]);
        var mcp = Assert.IsInstanceOfType<McpProtocolServer>(servers[9]);
        CollectionAssert.AreEqual(new[] { "mcp", "mcps" }, mcp.Schemes.ToArray());
        Assert.IsInstanceOfType<McpStdioListenUrlServer>(servers[10]);
        Assert.IsInstanceOfType<Mqtt5Broker>(servers[11]);
        Assert.IsInstanceOfType<Mqtt5WebSocketServer>(servers[12]);
        Assert.IsInstanceOfType<NatsServer>(servers[13]);
        Assert.IsInstanceOfType<RedisServer>(servers[14]);
        Assert.IsInstanceOfType<S3ProtocolServer>(servers[15]);
        Assert.IsInstanceOfType<SignalRProtocolServer>(servers[16]);
        Assert.IsInstanceOfType<SocketIoProtocolServer>(servers[17]);
        Assert.IsInstanceOfType<StompServer>(servers[18]);
        CollectionAssert.AreEqual(new[] { "stomps" }, servers[19].Schemes.ToArray());
        Assert.IsInstanceOfType<WebTransportProtocolServer>(servers[20]);
        Assert.IsInstanceOfType<XmppServer>(servers[21]);
        CollectionAssert.AreEqual(new[] { "xmpps" }, servers[22].Schemes.ToArray());
        CollectionAssert.AreEqual(new[] { "amqp", "amqps", "amqp1", "amqp1s", "az", "abfs", "abfss", "coap", "coaps", "dns", "dns", "gs", "kafka", "kafkas", "mcp", "mcps", "mcp+stdio", "mqtt5", "mqtt5s", "mqtt5+ws", "nats", "natss", "redis", "rediss", "s3", "signalr", "signalrs", "socketio", "socketios", "stomp", "stomps", "webtransport", "xmpp", "xmpps" }, servers.SelectMany(server => server.Schemes).ToArray());
        CollectionAssert.AreEqual(
            new[] { "amqp", "amqps", "amqp1", "amqp1s", "az", "abfs", "abfss", "coap", "coaps", "dns", "dns", "gs", "kafka", "kafkas", "mcp", "mcps", "mcp+stdio", "mqtt5", "mqtt5s", "mqtt5+ws", "nats", "natss", "redis", "rediss", "s3", "signalr", "signalrs", "socketio", "socketios", "stomp", "stomps", "webtransport", "xmpp", "xmpps" }, ExtendedProtocolServers.Registrations.SelectMany(registration => registration.Schemes).ToArray());
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
    public async Task Compose_WebTransportServer_ServesWithTheCertificateTheCompositionFillsIntoItsSourceLater()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);
        var server = (WebTransportProtocolServer)ExtendedProtocolServers.Compose(services).Single(composed => composed.Schemes.Contains("webtransport"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var context = new ExchangeContext(
            1,
            new ListenUrl("webtransport", "127.0.0.1", 1),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 2),
            new RecordingExchangeLog(),
            TimeProvider.System,
            cancelled.Token);

        // No certificate yet: nothing is served and the flow is untouched.
        await using var withoutCertificate = new InMemoryDatagramFlow(new byte[1], []);
        await server.ServeAsync(withoutCertificate, context);
        Assert.IsEmpty(withoutCertificate.SentDatagrams);

        // The composition filled the source: the flow is served, and the cancelled exchange stops it before the handshake.
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        services.ComposedServices.QuicCertificate.Certificate = new QuicServerCertificate([new byte[] { 1 }], key);
        await using var withCertificate = new InMemoryDatagramFlow(new byte[1], []);
        await Assert.ThrowsAsync<OperationCanceledException>(() => server.ServeAsync(withCertificate, context));
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

        foreach (var server in new IProtocolServer[] { servers.OfType<DnsDatagramRoutingProtocolServer>().Single().UdpServer, servers.OfType<DnsConnectionRoutingProtocolServer>().Single().StreamServer })
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
    public void Compose_ThisBuild_BuildsTheStompBrokerOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var stompServer = ExtendedProtocolServers.Compose(services).OfType<StompServer>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var bus = ComposedServerFields.Single<MessageBus.MessageBus>(stompServer);
        Assert.AreSame(services.ComposedServices.MessageBus, bus);
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer));
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheAmqpBrokerOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var amqpBroker = ExtendedProtocolServers.Compose(services).OfType<Amqp0Broker>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var bus = ComposedServerFields.Single<MessageBus.MessageBus>(amqpBroker);
        Assert.AreSame(services.ComposedServices.MessageBus, bus);
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer));
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheMqtt5BrokerOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var mqtt5Broker = ExtendedProtocolServers.Compose(services).OfType<Mqtt5Broker>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var bus = ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer);
        Assert.AreSame(services.ComposedServices.MessageBus, bus);
        var state = ComposedServerFields.Single<Mqtt5BrokerState>(mqtt5Broker);
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(state));
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheMqtt5WebSocketServerOverTheBrokerThatAnswersMqtt5()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var servers = ExtendedProtocolServers.Compose(services);

        var broker = servers.OfType<Mqtt5Broker>().Single();
        Assert.AreSame(broker, ComposedServerFields.Single<Mqtt5Broker>(servers.OfType<Mqtt5WebSocketServer>().Single()));
    }

    [TestMethod]
    [DataRow(true, 40)]
    [DataRow(false, 0)]
    public void Compose_ThisBuild_BuildsTheAmqpBrokerWithTheIdleTimeoutAndAnonymousLogin(bool allowAnonymous, int idleTimeoutSeconds)
    {
        var services = new ExtendedProtocolServerServices(ComposeServices(allowAnonymous, TimeSpan.FromSeconds(idleTimeoutSeconds)));

        var amqpBroker = ExtendedProtocolServers.Compose(services).OfType<Amqp0Broker>().Single();

        var options = ComposedServerFields.Single<Amqp0BrokerOptions>(amqpBroker);
        Assert.AreEqual(TimeSpan.FromSeconds(idleTimeoutSeconds), options.IdleTimeout);
        Assert.AreEqual(allowAnonymous, options.OffersAnonymous);
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheStompBrokerWithTheIdleTimeout()
    {
        var services = new ExtendedProtocolServerServices(ComposeServices(false, TimeSpan.FromSeconds(40)));

        var stompServer = ExtendedProtocolServers.Compose(services).OfType<StompServer>().Single();

        var options = ComposedServerFields.Single<StompServerOptions>(stompServer);
        Assert.AreEqual(TimeSpan.FromSeconds(40), options.IdleTimeout);
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
    public void Compose_ThisBuild_BuildsTheAmqp1ContainerOverTheOneBusTheMqttServerIsBuiltOver()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var container = ExtendedProtocolServers.Compose(services).OfType<Amqp1Container>().Single();
        var mqttServer = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, services.ComposedServices)
            .OfType<MqttProtocolServer>().Single();

        var bus = ComposedServerFields.Single<MessageBus.MessageBus>(container);
        Assert.AreSame(services.ComposedServices.MessageBus, bus);
        Assert.AreSame(bus, ComposedServerFields.Single<MessageBus.MessageBus>(mqttServer));
    }

    [TestMethod]
    [DataRow(true, 40, true)]
    [DataRow(false, 0, false)]
    public void Compose_ThisBuild_BuildsTheAmqp1ContainerWithTheIdleTimeoutAllowAnonymousAndTlsUpgrade(
        bool allowAnonymous, int idleTimeoutSeconds, bool isTlsUpgradeAvailable)
    {
        var composed = ComposeServices(allowAnonymous, TimeSpan.FromSeconds(idleTimeoutSeconds), isTlsUpgradeAvailable);

        var container = ExtendedProtocolServers.Compose(new ExtendedProtocolServerServices(composed)).OfType<Amqp1Container>().Single();

        Assert.AreEqual(TimeSpan.FromSeconds(idleTimeoutSeconds), ComposedServerFields.Single<Amqp1ContainerOptions>(container).IdleTimeout);
        var negotiation = ComposedServerFields.Single<Amqp1SecurityNegotiationOptions>(container);
        Assert.AreEqual(allowAnonymous, negotiation.AllowsAnonymous);
        Assert.AreEqual(isTlsUpgradeAvailable, negotiation.CanUpgradeToTls);
        Assert.AreSame(composed.AuthenticationPolicy, ComposedServerFields.Single<ISaslAuthenticationPolicy>(container));
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

    private static ComposedProtocolServices ComposeServices(bool allowAnonymous, TimeSpan idleTimeout, bool isTlsUpgradeAvailable = false)
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
            isTlsUpgradeAvailable,
            echoesWebSocketMessages: false,
            advertisesHttp3: false)
        {
            IdleTimeout = idleTimeout,
        };
    }

    [TestMethod]
    public void SurlVersion_IsTheInformationalVersionWithoutBuildMetadata()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(ExtendedProtocolServers.SurlVersion));
        Assert.DoesNotContain("+", ExtendedProtocolServers.SurlVersion);
    }
}
