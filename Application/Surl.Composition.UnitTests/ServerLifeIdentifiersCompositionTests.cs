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
