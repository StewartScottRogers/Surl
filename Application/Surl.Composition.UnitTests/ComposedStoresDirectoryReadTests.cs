using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads the LDAP directory back as LDIF through <see cref="ComposedStores"/> (ADR-0164 decision 9,
/// BL-1219), with no LDAP client.
/// </summary>
[TestClass]
public sealed class ComposedStoresDirectoryReadTests
{
    private const string ListenUrl = "ldap://127.0.0.1:0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadDirectoryLdif_ASeededEntry_ReadsBackLessUserPasswordAndPublishesNothing()
    {
        await using var running = await ComposeAsync();
        await running.Stores.SeedEntriesAsync(
            "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\nuserPassword: secret\n", TestContext.CancellationToken);
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        var read = running.Stores.ReadDirectoryLdif();

        Assert.IsTrue(read.IsFound);
        Assert.AreEqual("version: 1\n\ndn: dc=example,dc=com\nobjectClass: domain\ndc: example\n", read.Value);
        Assert.IsFalse(subscription.TryRead(out _));
    }

    [TestMethod]
    public async Task ReadDirectoryLdif_AnInstanceWithoutLdap_IsNotFound()
    {
        await using var running = await ComposeAsync();
        var without = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        Assert.AreEqual(ComposedReadOutcome.NotFound, without.ReadDirectoryLdif().Outcome);
    }

    [TestMethod]
    public async Task ReadDirectoryLdif_MoreThanTheReadBound_IsTooLarge()
    {
        await using var running = await ComposeAsync();
        var value = new string('a', 1_000_000);
        for (var batch = 0; batch < 2; batch++)
        {
            var ldif = string.Concat(Enumerable.Range(batch * 9, 9).Select(index => $"dn: o=big{index}\ndescription: {value}\n\n"));
            await running.Stores.SeedEntriesAsync(ldif, TestContext.CancellationToken);
        }

        Assert.AreEqual(ComposedReadOutcome.TooLarge, running.Stores.ReadDirectoryLdif().Outcome);
    }

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory()).Seams,
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }
}
