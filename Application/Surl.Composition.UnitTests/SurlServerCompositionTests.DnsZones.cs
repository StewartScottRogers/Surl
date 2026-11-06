using Surl.Cli;
using Surl.Content;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    private static readonly string DnsZoneFolderUnderServed = Path.Join(Path.GetFullPath("served"), ".surl", "dns");

    private static readonly byte[] ExampleZone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n"u8.ToArray();

    [TestMethod]
    public void ComposeDnsZoneFolder_WithADataDirectory_IsItsSurlDnsFolder()
    {
        var folder = SurlServerComposition.ComposeDnsZoneFolder(
            new SurlCommandLine { DataDirectory = "served" }, new InMemoryContentFileSystem(TimeProvider.System));

        Assert.AreEqual(DnsZoneFolderUnderServed, folder!.FolderPath);
    }

    [TestMethod]
    public void ComposeDnsZoneFolder_WithoutADataDirectory_IsNone()
    {
        Assert.IsNull(SurlServerComposition.ComposeDnsZoneFolder(new SurlCommandLine(), new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public void ComposeDnsZoneFolder_NullCommandLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SurlServerComposition.ComposeDnsZoneFolder(null!, new InMemoryContentFileSystem(TimeProvider.System)));
    }

    [TestMethod]
    public async Task LoadDnsZonesAsync_NoFolder_IsNoZone()
    {
        var (zones, failureMessage) = await SurlServerComposition.LoadDnsZonesAsync(null, TestContext.CancellationToken);

        Assert.IsEmpty(zones!.Apexes);
        Assert.IsNull(failureMessage);
    }

    [TestMethod]
    public async Task LoadDnsZonesAsync_AZoneFile_IsItsZone()
    {
        var folder = new DnsZoneFolder(FileSystemWithZone("example.com.zone", ExampleZone), DnsZoneFolderUnderServed);

        var (zones, failureMessage) = await SurlServerComposition.LoadDnsZonesAsync(folder, TestContext.CancellationToken);

        Assert.IsNull(failureMessage);
        Assert.AreEqual("example.com.", zones!.Apexes.Single().ToString());
    }

    [TestMethod]
    public async Task LoadDnsZonesAsync_AMalformedZoneFile_GivesCouldNotReadWithTheFileAndLine()
    {
        var folder = new DnsZoneFolder(FileSystemWithZone("example.com.zone", "not a zone\n"u8.ToArray()), DnsZoneFolderUnderServed);

        var (zones, failureMessage) = await SurlServerComposition.LoadDnsZonesAsync(folder, TestContext.CancellationToken);

        Assert.IsNull(zones);
        StringAssert.StartsWith(failureMessage, $"(37) Could not read {Path.Join(DnsZoneFolderUnderServed, "example.com.zone")}: line 1: ");
    }

    [TestMethod]
    public async Task ComposeAsync_ADataDirectoryWithAMalformedZoneFile_FailsWithCouldNotReadFile()
    {
        var seams = new StartTestSeams().Seams with { DataDirectoryFileSystem = FileSystemWithZone("example.com.zone", "not a zone\n"u8.ToArray()) };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        StringAssert.StartsWith(start.Failure.Lines.Single(), $"(37) Could not read {Path.Join(DnsZoneFolderUnderServed, "example.com.zone")}: line 1: ");
    }

    [TestMethod]
    public async Task LoadServiceStateAsync_ADataDirectoryWithAZoneFile_HoldsItsZone()
    {
        var commandLine = new SurlCommandLine { DataDirectory = "served" };
        var policy = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(commandLine, TimeProvider.System), TimeProvider.System).AuthenticationPolicy;

        var (state, failureMessage) = await SurlServerComposition.LoadServiceStateAsync(
            commandLine,
            FileSystemWithZone("example.com.zone", ExampleZone),
            policy,
            [],
            isTlsUpgradeAvailable: false,
            TimeProvider.System,
            DiscardingSurlEventPublisher.Instance,
            EmptyFaultPlan.Instance,
            TestContext.CancellationToken);

        Assert.IsNull(failureMessage);
        Assert.AreEqual("example.com.", state!.DnsZones.Apexes.Single().ToString());
    }

    private static InMemoryContentFileSystem FileSystemWithZone(string fileName, byte[] content)
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(DnsZoneFolderUnderServed);
        using (var file = fileSystem.CreateFileForAsyncWrite(Path.Join(DnsZoneFolderUnderServed, fileName)))
        {
            file.Write(content);
        }

        return fileSystem;
    }
}
