using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Whether a flush position is reached by the staged appends without a gap or an overlap
/// ("Path - Update", <c>InvalidFlushPosition</c>; ADR-0109, decision 5.5).
/// </summary>
[TestClass]
public sealed class AzureStagedPathTests
{
    [TestMethod]
    [DataRow(4L, true, DisplayName = "the committed length, nothing staged needed")]
    [DataRow(10L, true, DisplayName = "the end of the contiguous ranges")]
    [DataRow(7L, true, DisplayName = "inside the ranges")]
    [DataRow(3L, false, DisplayName = "before the committed length")]
    [DataRow(11L, false, DisplayName = "past the staged data")]
    public async Task Reaches_ContiguousRanges_IsTrueFromTheCommittedLengthToTheirEnd(long position, bool expected)
    {
        await using var session = await OpenSessionAsync();
        var staged = new AzureStagedPath(session, 4, AzureServerHarness.Now);
        staged.Add(new AzureStagedRange(7, 3), AzureServerHarness.Now);
        staged.Add(new AzureStagedRange(4, 3), AzureServerHarness.Now);

        Assert.AreEqual(expected, staged.Reaches(position));
    }

    [TestMethod]
    [DataRow(5L, DisplayName = "a gap")]
    [DataRow(3L, DisplayName = "an overlap")]
    public async Task Reaches_RangesThatAreNotContiguous_IsFalse(long secondPosition)
    {
        await using var session = await OpenSessionAsync();
        var staged = new AzureStagedPath(session, 0, AzureServerHarness.Now);
        staged.Add(new AzureStagedRange(0, 4), AzureServerHarness.Now);
        staged.Add(new AzureStagedRange(secondPosition, 4), AzureServerHarness.Now);

        Assert.IsFalse(staged.Reaches(8));
    }

    private static async Task<ContentUploadSession> OpenSessionAsync()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.WriteFile("a.txt", "");
        var opening = await harness.Store.OpenUploadAsync(harness.Store.MapRequestPath("/a.txt"), new ContentUploadOpening(true, false, false), CancellationToken.None);

        return opening.Session!;
    }
}
