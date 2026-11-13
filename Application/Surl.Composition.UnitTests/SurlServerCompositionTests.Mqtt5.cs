using Surl.Cli;
using Surl.Extended.Protocol.Mqtt5;

namespace Surl.Composition;

/// <summary>
/// The MQTT 5 broker the extended servers compose for <c>mqtt5</c> (ADR-0093 decision 9, BL-976):
/// <c>--mqtt5-disconnect-after-retained</c> reaches it, and is off when absent.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    [DataRow("--mqtt5-disconnect-after-retained", true)]
    [DataRow("--no-mqtt5-disconnect-after-retained", false)]
    [DataRow("--allow-anonymous", false)]
    public async Task ComposeAsync_Mqtt5DisconnectAfterRetained_ReachesTheBroker(string option, bool expected)
    {
        Mqtt5Broker? broker = null;
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                broker = ExtendedProtocolServers.Compose(services).OfType<Mqtt5Broker>().Single();
                return [broker];
            },
        };
        var commandLine = CommandLineParser.Parse([option, "--allow-anonymous", "mqtt5://127.0.0.1:0/"], ["mqtt5"]).CommandLine!;

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.AreEqual(expected, broker!.DisconnectsAfterRetained);
    }
}
