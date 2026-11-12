using Surl.Core;

namespace Surl.Api;

// BL-998: SurlServerOptions.RandomSeed is --random-seed's spelling and reaches the composition, so servers side by side
// in one process each draw from their own seed (ADR-0124, decision 6).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task IdentifiersForExchange_TwoServersWithTheSameSeed_DrawEqualBytes()
    {
        await using var first = await StartSeededAsync(42);
        await using var second = await StartSeededAsync(42);

        CollectionAssert.AreEqual(DrawIdentifierBytes(first), DrawIdentifierBytes(second));
    }

    [TestMethod]
    public async Task IdentifiersForExchange_TwoServersWithDifferentSeeds_DrawDifferentBytes()
    {
        await using var first = await StartSeededAsync(42);
        await using var second = await StartSeededAsync(43);

        CollectionAssert.AreNotEqual(DrawIdentifierBytes(first), DrawIdentifierBytes(second));
    }

    [TestMethod]
    public async Task IdentifiersForExchange_NoSeed_IsTheCryptographicSource()
    {
        await using var server = await StartSeededAsync(null);

        Assert.AreSame(CryptographicIdentifierRandom.Instance, server.IdentifiersForExchange(1));
    }

    private async Task<SurlServer> StartSeededAsync(long? seed)
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.RandomSeed = seed;
        return await SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);
    }

    private static byte[] DrawIdentifierBytes(SurlServer server)
    {
        var bytes = new byte[16];
        server.IdentifiersForExchange(1).NextBytes(bytes);
        return bytes;
    }
}
