using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

[TestClass]
public sealed class OciSessionUuidTests
{
    private sealed class SeededBytes(int seed) : IIdentifierRandom
    {
        private readonly Random random = new(seed);

        public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
    }

    private static async Task<string> OpenSessionUuidAsync(OciRegistryServerHarness harness)
    {
        var response = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", "Content-Length: 0");
        return Regex.Match(response, "Docker-Upload-UUID: ([^\r]+)\r\n").Groups[1].Value;
    }

    [TestMethod]
    public void Draw_SetsTheVersion4AndRfc4122VariantBits()
    {
        var uuid = OciSessionUuid.Draw(new SeededBytes(1)).ToString("D");

        Assert.IsTrue(Regex.IsMatch(uuid, "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"), uuid);
    }

    [TestMethod]
    public async Task OpenAsync_SameSeed_GivesTheSameSessionUuid()
    {
        var first = await OpenSessionUuidAsync(OciRegistryServerHarness.DrawingSessionIds(new SeededBytes(7)));
        var second = await OpenSessionUuidAsync(OciRegistryServerHarness.DrawingSessionIds(new SeededBytes(7)));
        var other = await OpenSessionUuidAsync(OciRegistryServerHarness.DrawingSessionIds(new SeededBytes(8)));

        Assert.AreNotEqual(string.Empty, first);
        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, other);
    }

    [TestMethod]
    public async Task OpenAsync_Unseeded_GivesAFreshSessionUuidEachTime()
    {
        var harness = OciRegistryServerHarness.DrawingSessionIds();

        var first = await OpenSessionUuidAsync(harness);
        var second = await OpenSessionUuidAsync(harness);

        Assert.AreNotEqual(first, second);
    }
}
