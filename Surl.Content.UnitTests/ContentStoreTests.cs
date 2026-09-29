namespace Surl.Content;

[TestClass]
public sealed class ContentStoreTests
{
    private static readonly string Root = Path.Join("/", "srv", "www");

    [TestMethod]
    [DataRow("/../x", ContentPathRefusal.DotSegment)]
    [DataRow("/a/../../x", ContentPathRefusal.DotSegment)]
    [DataRow("/%2e%2e/x", ContentPathRefusal.DotSegment)]
    [DataRow("/%2E%2E/x", ContentPathRefusal.DotSegment)]
    [DataRow("/.%2e/x", ContentPathRefusal.DotSegment)]
    [DataRow("/./x", ContentPathRefusal.DotSegment)]
    [DataRow("/.", ContentPathRefusal.DotSegment)]
    [DataRow("/..", ContentPathRefusal.DotSegment)]
    [DataRow("/..%2fx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..%2Fx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..%5cx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..\\x", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/\\\\server\\share\\x", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/C:%5cx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("//server/share/x", ContentPathRefusal.EmptySegment)]
    [DataRow("//", ContentPathRefusal.EmptySegment)]
    [DataRow("/a//b", ContentPathRefusal.EmptySegment)]
    [DataRow("/C:/x", ContentPathRefusal.ColonInSegment)]
    [DataRow("/c:", ContentPathRefusal.ColonInSegment)]
    [DataRow("/file.txt::$DATA", ContentPathRefusal.ColonInSegment)]
    [DataRow("/%00x", ContentPathRefusal.ControlCharacter)]
    [DataRow("/x%01", ContentPathRefusal.ControlCharacter)]
    [DataRow("/x%7F", ContentPathRefusal.ControlCharacter)]
    [DataRow("/%zz", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%4", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%4g", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%g4", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/ok/%zz/x", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%C3", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/%FF.txt", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/%C0%AE%C0%AE/x", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/x.", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/x%20", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/...", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/CON", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/nul.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/dir/com1", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/LPT9.log", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CON%20.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/COM%C2%B9", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CONIN$", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/conout$.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CLOCK$", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("", ContentPathRefusal.NotRooted)]
    [DataRow("x", ContentPathRefusal.NotRooted)]
    [DataRow("*", ContentPathRefusal.NotRooted)]
    [DataRow("\\x", ContentPathRefusal.NotRooted)]
    [DataRow("C:/x", ContentPathRefusal.NotRooted)]
    public void MapRequestPath_RefusesEscape_WithoutAskingTheFileSystem(string requestPath, ContentPathRefusal expected)
    {
        var fileSystem = new InMemoryContentFileSystem().AddDirectory(Root);
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsFalse(mapping.IsMapped);
        Assert.AreEqual(expected, mapping.Refusal);
        Assert.IsNull(mapping.Location);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public void MapRequestPath_DotDotThatStaysInsideTheRoot_IsRefused()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "a"))
            .AddFile(Path.Join(Root, "b"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/a/../b");

        Assert.AreEqual(ContentPathRefusal.DotSegment, mapping.Refusal);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkPointingOutOfTheRoot_IsRefusedWithoutOpeningIt()
    {
        string outside = Path.Join("/", "etc");
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "out"), outside)
            .AddFile(Path.Join(outside, "passwd"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/out/passwd");

        Assert.IsFalse(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
        Assert.IsNull(mapping.Location);
        Assert.IsTrue(fileSystem.Calls.TrueForAll(call => call.StartsWith("ResolveFinalPath(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkToASiblingWhoseNameStartsWithTheRoot_IsRefused()
    {
        string sibling = Root + "-evil";
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "out"), sibling)
            .AddFile(Path.Join(sibling, "x"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/out/x");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkToTheRootsParent_IsRefused()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "up"), Path.Join("/", "srv"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/up");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [DataRow("/", new string[0], ContentEntryKind.Directory)]
    [DataRow("/file.txt", new[] { "file.txt" }, ContentEntryKind.File)]
    [DataRow("/dir/file.txt", new[] { "dir", "file.txt" }, ContentEntryKind.File)]
    [DataRow("/dir", new[] { "dir" }, ContentEntryKind.Directory)]
    [DataRow("/dir/", new[] { "dir" }, ContentEntryKind.Directory)]
    [DataRow("/with%20space.txt", new[] { "with space.txt" }, ContentEntryKind.File)]
    [DataRow("/caf%C3%A9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/caf%c3%a9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/caf\u00E9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/.hidden", new[] { ".hidden" }, ContentEntryKind.File)]
    [DataRow("/CONSOLE.txt", new[] { "CONSOLE.txt" }, ContentEntryKind.File)]
    [DataRow("/100%25.txt", new[] { "100%.txt" }, ContentEntryKind.File)]
    [DataRow("/missing.txt", new[] { "missing.txt" }, ContentEntryKind.None)]
    public void MapRequestPath_WellFormedPath_MapsInsideTheRoot(string requestPath, string[] expectedSegments, ContentEntryKind expectedKind)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"))
            .AddDirectory(Path.Join(Root, "dir"))
            .AddFile(Path.Join(Root, "dir", "file.txt"))
            .AddFile(Path.Join(Root, "with space.txt"))
            .AddFile(Path.Join(Root, "caf\u00E9.txt"))
            .AddFile(Path.Join(Root, ".hidden"))
            .AddFile(Path.Join(Root, "CONSOLE.txt"))
            .AddFile(Path.Join(Root, "100%.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.None, mapping.Refusal);
        Assert.AreEqual(Path.Join([Root, .. expectedSegments]), mapping.Location);
        Assert.AreEqual(expectedKind, mapping.EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkWhoseFinalTargetStaysInsideTheRoot_MapsToTheTarget()
    {
        string target = Path.Join(Root, "dir");
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(target)
            .AddFile(Path.Join(target, "file.txt"))
            .AddSymbolicLink(Path.Join(Root, "hop"), Path.Join(Root, "link"))
            .AddSymbolicLink(Path.Join(Root, "link"), target);
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/hop/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(Path.Join(target, "file.txt"), mapping.Location);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootIsItselfASymbolicLink_ComparesAgainstItsTarget()
    {
        string realRoot = Path.Join("/", "data", "www");
        var fileSystem = new InMemoryContentFileSystem()
            .AddSymbolicLink(Root, realRoot)
            .AddDirectory(realRoot)
            .AddFile(Path.Join(realRoot, "file.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(Path.Join(realRoot, "file.txt"), mapping.Location);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootWithTrailingSeparator_MapsTheRootAndItsChildren()
    {
        string root = Root + Path.DirectorySeparatorChar;
        var fileSystem = new InMemoryContentFileSystem().AddFile(Path.Join(root, "file.txt"));
        var store = new ContentStore(root, fileSystem);

        Assert.AreEqual(root, store.MapRequestPath("/").Location);
        Assert.AreEqual(Path.Join(root, "file.txt"), store.MapRequestPath("/file.txt").Location);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootIsTheFileSystemRoot_MapsItsChildren()
    {
        string root = Path.DirectorySeparatorChar.ToString();
        var fileSystem = new InMemoryContentFileSystem().AddFile(Path.Join(root, "file.txt"));
        var store = new ContentStore(root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    public void Constructor_KeepsTheServedRoot()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem());

        Assert.AreEqual(Root, store.ServedRoot);
    }

    [TestMethod]
    public void Constructor_RejectsMissingArguments()
    {
        var fileSystem = new InMemoryContentFileSystem();

        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(null!, fileSystem));
        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(string.Empty, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(Root, null!));
    }

    [TestMethod]
    public void MapRequestPath_RejectsNull()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem());

        Assert.ThrowsExactly<ArgumentNullException>(() => store.MapRequestPath(null!));
    }
}
