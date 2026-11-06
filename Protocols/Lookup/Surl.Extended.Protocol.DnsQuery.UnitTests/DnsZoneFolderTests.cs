using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reading the zone folder through <see cref="Surl.Content.IContentFileSystem"/> (ADR-0092
/// decision 1): every <c>*.zone</c> file, in ordinal order, and a load fault naming the file.
/// </summary>
[TestClass]
public sealed class DnsZoneFolderTests
{
    private static readonly string Folder = Path.Join(Path.DirectorySeparatorChar + "data", ".surl", "dns");

    [TestMethod]
    public async Task LoadAsync_FolderWithZoneFiles_LoadsEachZoneFileInOrdinalOrder()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Folder)
            .AddFile(Path.Join(Folder, "b.test.zone"), Zone())
            .AddFile(Path.Join(Folder, "a.test.ZONE"), Zone())
            .AddFile(Path.Join(Folder, "notes.txt"), [0xFF])
            .AddDirectory(Path.Join(Folder, "c.test.zone"));

        var zones = await new DnsZoneFolder(fileSystem, Folder).LoadAsync(new DnsZoneSet());

        CollectionAssert.AreEqual(new[] { "a.test.", "b.test." }, zones.Apexes.Select(apex => apex.ToString()).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_NoFolder_LoadsNoZone()
    {
        var folder = new DnsZoneFolder(new UnitTestInMemoryContentFileSystem(), Folder);

        var zones = await folder.LoadAsync(new DnsZoneSet());

        Assert.IsEmpty(zones.Apexes);
        Assert.AreEqual(Folder, folder.FolderPath);
    }

    [TestMethod]
    public async Task LoadAsync_MalformedZoneFile_ThrowsNamingTheFileAndTheLine()
    {
        var filePath = Path.Join(Folder, "a.test.zone");
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Folder)
            .AddFile(filePath, Encoding.UTF8.GetBytes("$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2"));

        var exception = await Assert.ThrowsExactlyAsync<DnsZoneLoadException>(
            () => new DnsZoneFolder(fileSystem, Folder).LoadAsync(new DnsZoneSet()));

        Assert.AreEqual(filePath, exception.FilePath);
        Assert.AreEqual("line 3: bad RDATA for A", exception.Message);
        Assert.IsInstanceOfType<DnsZoneFileException>(exception.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_UnreadableZoneFile_ThrowsWithTheReadFailure()
    {
        var filePath = Path.Join(Folder, "a.test.zone");
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddDirectory(Folder).AddUnreadableFile(filePath);

        var exception = await Assert.ThrowsExactlyAsync<DnsZoneLoadException>(
            () => new DnsZoneFolder(fileSystem, Folder).LoadAsync(new DnsZoneSet()));

        Assert.AreEqual(filePath, exception.FilePath);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_NullSet_Throws()
    {
        var folder = new DnsZoneFolder(new UnitTestInMemoryContentFileSystem(), Folder);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => folder.LoadAsync(null!));
    }

    [TestMethod]
    public void Constructor_MissingArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsZoneFolder(null!, Folder));
        Assert.ThrowsExactly<ArgumentException>(() => new DnsZoneFolder(new UnitTestInMemoryContentFileSystem(), string.Empty));
    }

    private static byte[] Zone() => Encoding.UTF8.GetBytes("$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\n");
}
