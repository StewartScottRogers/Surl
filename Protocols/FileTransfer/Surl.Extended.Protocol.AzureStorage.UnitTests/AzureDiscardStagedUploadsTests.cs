namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Discarding every staged upload at once, as a reset or restore does (ADR-0167, decision 2; BL-1230).
/// </summary>
[TestClass]
public sealed class AzureDiscardStagedUploadsTests
{
    private const string BlockA = "QUFB";
    private const string BlockListHead = "<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DiscardStagedUploadsAsync_AStagedBlock_MakesPutBlockListAnswerInvalidBlockListAndLeavesNoTemporaryFile()
    {
        var harness = Writable();
        await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/big.bin?comp=block&blockid={BlockA}", "one ");

        await harness.Server.DiscardStagedUploadsAsync(TestContext.CancellationToken);
        var committed = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}<Uncommitted>{BlockA}</Uncommitted></BlockList>");

        StringAssert.Contains(committed, "<Code>InvalidBlockList</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(AzureServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task DiscardStagedUploadsAsync_ACommittedBlockList_IsForgottenAndGetBlockListAnswersAsForANeverStagedBlob()
    {
        var harness = Writable();
        await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/big.bin?comp=block&blockid={BlockA}", "one ");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}<Latest>{BlockA}</Latest></BlockList>");

        await harness.Server.DiscardStagedUploadsAsync(TestContext.CancellationToken);
        var listed = await harness.SendWithBearerAsync("GET", "/photos/big.bin?comp=blocklist&blocklisttype=all");

        StringAssert.EndsWith(listed, $"{BlockListHead}<CommittedBlocks></CommittedBlocks><UncommittedBlocks></UncommittedBlocks></BlockList>");
    }

    [TestMethod]
    public async Task DiscardStagedUploadsAsync_NothingStaged_Completes()
    {
        var harness = Writable();

        await harness.Server.DiscardStagedUploadsAsync(TestContext.CancellationToken);
    }

    private static AzureServerHarness Writable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        return harness;
    }
}
