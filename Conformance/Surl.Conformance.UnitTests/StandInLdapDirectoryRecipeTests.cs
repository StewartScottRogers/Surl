using System.Text;
using Surl.Api;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite seeds the LDAP directory of one <see cref="SurlServer"/> through
/// <see cref="SurlServerDirectory.AddEntriesAsync"/> and the pinned upstream curl, playing the code under
/// test, finds the entry with an <c>ldap://</c> search; after <see cref="SurlServer.ResetAsync"/> the entry is
/// gone (BL-1169). An entry seeded at start through <see cref="SurlServerOptions.DirectoryLdif"/> survives that
/// reset: the same search finds it before and after, while a child entry added at runtime is gone (BL-2033,
/// ADR-0158). The Windows reference build runs over <c>WinLDAP</c>; elsewhere the pinned OpenLDAP build runs
/// (ADR-0076). Inconclusive, naming the pin, where no LDAP-capable build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInLdapDirectoryRecipeTests
{
    private const string BaseDn = "dc=example,dc=com";
    private const string Ldif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";
    private const string ChildDn = "cn=child," + BaseDn;
    private const string ChildLdif = "dn: cn=child,dc=example,dc=com\nobjectClass: person\ncn: child\nsn: child\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeedEntry_ThenSearchOverLdap_PrintsTheSeededDnAndAttribute()
    {
        // Arrange: an in-memory server whose directory the test seeds.
        await using var server = await StartAsync();
        await server.Directory.AddEntriesAsync(Ldif, TestContext.CancellationToken);

        // Act: the code under test searches for the entry.
        var result = await SearchAsync(server);

        // Assert: pinned curl prints the DN and the attribute and exits 0.
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var output = Encoding.UTF8.GetString(result.StandardOutput);
        StringAssert.Contains(output, $"DN: {BaseDn}");
        StringAssert.Contains(output, "objectClass: domain");
    }

    [TestMethod]
    public async Task ResetAsync_AfterSeeding_TheSameSearchNoLongerFindsTheEntry()
    {
        // Arrange: a server holding one seeded entry.
        await using var server = await StartAsync();
        await server.Directory.AddEntriesAsync(Ldif, TestContext.CancellationToken);

        // Act: the test resets the stores between tests, and the code under test searches again.
        await server.ResetAsync(TestContext.CancellationToken);
        var result = await SearchAsync(server);

        // Assert: the entry is gone.
        AssertNoSuchObject(result);
    }

    [TestMethod]
    public async Task ResetAsync_AfterStartTimeAndRuntimeSeeds_SearchStillFindsOnlyTheStartTimeEntry()
    {
        // Arrange: a server holding the base entry from start and a child entry added while it serves.
        await using var server = await StartAsync(directoryLdif: Ldif);
        await server.Directory.AddEntriesAsync(ChildLdif, TestContext.CancellationToken);

        // Act: the code under test searches for both, the test resets the stores, and it searches again.
        var baseBeforeReset = await SearchAsync(server);
        var childBeforeReset = await SearchAsync(server, ChildDn);
        await server.ResetAsync(TestContext.CancellationToken);
        var baseAfterReset = await SearchAsync(server);
        var childAfterReset = await SearchAsync(server, ChildDn);

        // Assert: the start-time entry is found both times; the child only before the reset.
        AssertFound(baseBeforeReset, BaseDn);
        AssertFound(childBeforeReset, ChildDn);
        AssertFound(baseAfterReset, BaseDn);
        AssertNoSuchObject(childAfterReset);
    }

    private static void AssertFound(UpstreamCurlRunResult result, string dn)
    {
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(Encoding.UTF8.GetString(result.StandardOutput), $"DN: {dn}");
    }

    /// <summary>
    /// The directory answers a search for a missing entry with noSuchObject, which curl reports as exit 39 with its
    /// LDAP library's text for the result code: WinLDAP's "No Such Object" on Windows, OpenLDAP's "No such object"
    /// elsewhere (measured on CI's ubuntu-latest, PR #38).
    /// </summary>
    private static void AssertNoSuchObject(UpstreamCurlRunResult result)
    {
        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, OperatingSystem.IsWindows() ? "No Such Object" : "No such object");
        Assert.DoesNotContain("DN:", Encoding.UTF8.GetString(result.StandardOutput));
    }

    private Task<SurlServer> StartAsync(string? directoryLdif = null)
    {
        var options = new SurlServerOptions { AllowAnonymous = true, DirectoryLdif = directoryLdif };
        options.ListenUrls.Add("ldap://127.0.0.1:0");
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    private Task<UpstreamCurlRunResult> SearchAsync(SurlServer server, string dn = BaseDn)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == "ldap");
        string[] arguments = ["-sS", "-m", "20", "-u", "anyone:anything", $"ldap://{url.Host}:{url.Port}/{dn}"];
        return OperatingSystem.IsWindows()
            ? PinnedUpstreamCurl.RunReferenceForProtocolAsync(TestContext, "ldap", arguments)
            : PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(TestContext, "OpenLDAP", arguments);
    }
}
