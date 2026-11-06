namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The staging area's room: at most <see cref="AzureStagingArea.MaxStagings"/> stagings, the least
/// recently touched one holding no staged bytes forgotten to make room (ADR-0109, decision 7).
/// </summary>
[TestClass]
public sealed class AzureStagingAreaTests
{
    [TestMethod]
    public async Task TryAddAsync_WhenFullWithIdleStagings_ForgetsTheLeastRecentlyTouched()
    {
        var clock = new FixedTimeProvider(AzureServerHarness.Now);
        var area = new AzureStagingArea(clock);
        for (var staging = 0; staging < AzureStagingArea.MaxStagings; staging++)
        {
            Assert.IsTrue(await area.TryAddAsync($"{staging}", new AzureStagedBlocks(AzureServerHarness.Now.AddSeconds(staging == 7 ? -1 : staging))));
        }

        var added = await area.TryAddAsync("new", new AzureStagedBlocks(AzureServerHarness.Now));

        Assert.IsTrue(added);
        Assert.IsNull(area.Find<AzureStagedBlocks>("7"));
        Assert.IsNotNull(area.Find<AzureStagedBlocks>("0"));
        Assert.IsNotNull(area.Find<AzureStagedBlocks>("new"));
    }

    [TestMethod]
    public async Task TryAddAsync_WhenFullOfStagedBytes_RefusesTheNew()
    {
        var area = new AzureStagingArea(new FixedTimeProvider(AzureServerHarness.Now));
        for (var staging = 0; staging < AzureStagingArea.MaxStagings; staging++)
        {
            await area.TryAddAsync($"{staging}", new HoldingStaging());
        }

        var added = await area.TryAddAsync("new", new AzureStagedBlocks(AzureServerHarness.Now));

        Assert.IsFalse(added);
        Assert.IsNull(area.Find<AzureStagedBlocks>("new"));
    }

    [TestMethod]
    public async Task Find_AStagingOfAnotherKind_IsNull()
    {
        var area = new AzureStagingArea(new FixedTimeProvider(AzureServerHarness.Now));
        await area.TryAddAsync("key", new AzureStagedBlocks(AzureServerHarness.Now));

        var found = area.Find<HoldingStaging>("key");

        Assert.IsNull(found);
    }

    private sealed class HoldingStaging : IAzureStaging
    {
        public DateTimeOffset LastTouched => AzureServerHarness.Now;

        public bool HoldsStagedBytes => true;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
