using Surl.Cli;
using Surl.Control;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's LDAP read-back member over a composed instance (ADR-0164 decision 9, BL-1219): it returns
/// what <see cref="ComposedStores.SeedEntriesAsync"/> seeded, and an instance with no LDAP server is a
/// <c>NotFound</c> refusal.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetDirectoryReadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadDirectoryLdifAsync_ASeededEntry_ReadsBack()
    {
        await using var serving = await ComposeAsync();
        var target = new ComposedControlTarget(serving.Events, serving.Faults, serving.Stores);
        await target.SeedDirectoryAsync("dn: dc=example,dc=com\ndc: example\n", TestContext.CancellationToken);

        var ldif = await target.ReadDirectoryLdifAsync(TestContext.CancellationToken);

        Assert.AreEqual("version: 1\n\ndn: dc=example,dc=com\ndc: example\n", ldif);
    }

    [TestMethod]
    public async Task ReadDirectoryLdifAsync_NoLdapServer_IsANotFoundRefusal()
    {
        await using var serving = await ComposeAsync();
        var without = new ComposedControlTarget(
            serving.Events, serving.Faults, new ComposedStores(serving.Stores.Content, serving.Stores.MetadataIndex, serving.Stores.Mail, serving.Stores.Bus, serving.Stores.Accounts));

        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => without.ReadDirectoryLdifAsync(TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.NotFound, refusal.Kind);
    }

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "ldap://127.0.0.1:0"]).CommandLine!,
            new StartTestSeams().Seams,
            TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
