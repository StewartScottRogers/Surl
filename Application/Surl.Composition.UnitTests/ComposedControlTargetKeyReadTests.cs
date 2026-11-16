using System.Text;
using Surl.Cli;
using Surl.Control;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's Redis read-back members over a composed instance (ADR-0164 decision 9, BL-1218): each
/// returns what <see cref="ComposedStores.SeedKeyAsync"/> seeded and an absent key is a <c>NotFound</c> refusal.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetKeyReadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadKeyAsyncAndListKeysAsync_SeededKeys_ReadBack()
    {
        await using var serving = await ComposeAsync();
        var target = new ComposedControlTarget(serving.Events, serving.Faults, serving.Stores);
        await target.SeedKeyAsync(0, "a"u8.ToArray(), "one"u8.ToArray(), TimeSpan.FromMinutes(1), TestContext.CancellationToken);
        await target.SeedKeyAsync(0, "b"u8.ToArray(), "two"u8.ToArray(), null, TestContext.CancellationToken);

        var read = await target.ReadKeyAsync(0, "a"u8.ToArray(), TestContext.CancellationToken);
        var page = await target.ListKeysAsync(0, "*", "a"u8.ToArray(), 10, TestContext.CancellationToken);

        Assert.AreEqual("one", Encoding.ASCII.GetString(read.Value!.Value.Span));
        Assert.IsNotNull(read.Info.ExpiresAt);
        Assert.AreEqual("string", read.Info.Type);
        Assert.AreEqual("b", Encoding.ASCII.GetString(page.Keys.Single().Key.Span));
        Assert.IsFalse(page.HasMore);
    }

    [TestMethod]
    public async Task ReadKeyAsyncAndListKeysAsync_AbsentOrBad_AreRefusals()
    {
        await using var serving = await ComposeAsync();
        var target = new ComposedControlTarget(serving.Events, serving.Faults, serving.Stores);

        var absent = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadKeyAsync(0, "none"u8.ToArray(), TestContext.CancellationToken));
        var badLimit = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ListKeysAsync(0, "*", default, 0, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.NotFound, absent.Kind);
        Assert.AreEqual(ControlRefusalKind.BadRequest, badLimit.Kind);
    }

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "redis://127.0.0.1:0"], ["redis", "rediss"]).CommandLine!,
            new StartTestSeams().Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
