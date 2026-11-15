using System.Text;
using Surl.Api;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite seeds the LDAP directory of one <see cref="SurlServer"/> through
/// <see cref="SurlServerDirectory.AddEntriesAsync"/> and the pinned upstream curl, playing the code under
/// test, finds the entry with an <c>ldap://</c> search; after <see cref="SurlServer.ResetAsync"/> the entry is
/// gone (BL-1169). The Windows reference build runs over <c>WinLDAP</c>; elsewhere the pinned OpenLDAP build
/// runs (ADR-0076). Inconclusive, naming the pin, where no LDAP-capable build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInLdapDirectoryRecipeTests
{
    private const string BaseDn = "dc=example,dc=com";
    private const string Ldif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";

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

        // Assert: the entry is gone; the empty directory answers noSuchObject, which curl reports as exit 39
        // with its LDAP library's text for the result code: WinLDAP's "No Such Object" on Windows, OpenLDAP's
        // "No such object" elsewhere (measured on CI's ubuntu-latest, PR #38).
        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, OperatingSystem.IsWindows() ? "No Such Object" : "No such object");
        Assert.DoesNotContain("DN:", Encoding.UTF8.GetString(result.StandardOutput));
    }

    private Task<SurlServer> StartAsync()
    {
        var options = new SurlServerOptions { AllowAnonymous = true };
        options.ListenUrls.Add("ldap://127.0.0.1:0");
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    private Task<UpstreamCurlRunResult> SearchAsync(SurlServer server)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == "ldap");
        string[] arguments = ["-sS", "-m", "20", "-u", "anyone:anything", $"ldap://{url.Host}:{url.Port}/{BaseDn}"];
        return OperatingSystem.IsWindows()
            ? PinnedUpstreamCurl.RunReferenceForProtocolAsync(TestContext, "ldap", arguments)
            : PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(TestContext, "OpenLDAP", arguments);
    }
}
