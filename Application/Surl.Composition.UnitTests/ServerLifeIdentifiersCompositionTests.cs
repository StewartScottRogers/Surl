using Surl.Cli;
using Surl.Extended.Protocol.Nats;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ServerLifeIdentifiersCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NatsServerId_TwoCompositionsWithSeed42_AreTheSame()
    {
        var first = await ComposeAnnouncedIdsAsync("--random-seed", "42");
        var second = await ComposeAnnouncedIdsAsync("--random-seed", "42");

        Assert.AreEqual(first.NatsServerId, second.NatsServerId);
        Assert.AreEqual(first.KafkaClusterId, second.KafkaClusterId);
    }

    [TestMethod]
    public async Task NatsServerIdAndKafkaClusterId_TwoUnseededCompositions_Differ()
    {
        var first = await ComposeAnnouncedIdsAsync();
        var second = await ComposeAnnouncedIdsAsync();

        Assert.AreNotEqual(first.NatsServerId, second.NatsServerId);
        Assert.AreNotEqual(first.KafkaClusterId, second.KafkaClusterId);
    }

    [TestMethod]
    public async Task NatsServerIdAndKafkaClusterId_DifferentSeeds_Differ()
    {
        var first = await ComposeAnnouncedIdsAsync("--random-seed", "42");
        var second = await ComposeAnnouncedIdsAsync("--random-seed", "43");

        Assert.AreNotEqual(first.NatsServerId, second.NatsServerId);
        Assert.AreNotEqual(first.KafkaClusterId, second.KafkaClusterId);
    }

    [TestMethod]
    public async Task XmppStreamId_TwoCompositionsWithSeed42_AreTheSame()
    {
        var first = await ComposeXmppStreamIdAsync("--random-seed", "42");
        var second = await ComposeXmppStreamIdAsync("--random-seed", "42");

        Assert.AreEqual(first, second);
        Assert.AreEqual(32, first.Length);
    }

    [TestMethod]
    public async Task XmppStreamId_TwoUnseededCompositions_Differ()
    {
        var first = await ComposeXmppStreamIdAsync();
        var second = await ComposeXmppStreamIdAsync();

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public async Task Amqp1ContainerId_TwoCompositionsWithSeed42_AreTheSame()
    {
        var first = await ComposeAmqp1ContainerIdAsync("--random-seed", "42");
        var second = await ComposeAmqp1ContainerIdAsync("--random-seed", "42");

        Assert.AreEqual(first, second);
        Assert.MatchesRegex("^surl-[0-9a-f]{32}$", first);
    }

    [TestMethod]
    public async Task Amqp1ContainerId_DifferentSeeds_Differ()
    {
        var first = await ComposeAmqp1ContainerIdAsync("--random-seed", "42");
        var second = await ComposeAmqp1ContainerIdAsync("--random-seed", "43");

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public async Task Amqp1ContainerId_TwoUnseededCompositions_Differ()
    {
        var first = await ComposeAmqp1ContainerIdAsync();
        var second = await ComposeAmqp1ContainerIdAsync();

        Assert.AreNotEqual(first, second);
    }

    private async Task<string> ComposeAmqp1ContainerIdAsync(params string[] options)
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", .. options, "amqp1://127.0.0.1:0"], ["amqp1"]).CommandLine!, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
        return serving.Servers.OfType<Surl.Extended.Protocol.Amqp1.Amqp1Container>().Single().ContainerId;
    }

    private async Task<string> ComposeXmppStreamIdAsync(params string[] options)
    {
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", .. options, "xmpp://127.0.0.1:0"], ["xmpp"]).CommandLine!, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
        var xmpp = serving.Servers.OfType<Surl.Extended.Protocol.Xmpp.XmppServer>().Single();
        var connection = new FakeConnection([]);

        await xmpp.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        var text = System.Text.Encoding.UTF8.GetString(connection.WrittenBytes);
        var match = System.Text.RegularExpressions.Regex.Match(text, "id='([0-9a-f]+)'");
        Assert.IsTrue(match.Success, text);
        return match.Groups[1].Value;
    }

    private async Task<(string NatsServerId, string KafkaClusterId)> ComposeAnnouncedIdsAsync(params string[] options)
    {
        var kafkaClusterId = string.Empty;
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                kafkaClusterId = ExtendedProtocolServers.ComposeKafkaMetadataAnswerer(services.ComposedServices).ClusterId;
                return ExtendedProtocolServers.Compose(services);
            },
        };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse([.. options, "nats://127.0.0.1:0"], ["nats"]).CommandLine!, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
        var nats = serving.Servers.OfType<NatsServer>().Single();
        return (nats.ServerId, kafkaClusterId);
    }
}
