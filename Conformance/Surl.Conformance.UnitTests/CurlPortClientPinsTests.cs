namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortClientPinsTests
{
    private const string Sha256 = "1111111111111111111111111111111111111111111111111111111111111111";

    [TestMethod]
    public void Parse_CompleteEntry_ReadsEveryField()
    {
        var pins = CurlPortClientPins.Parse(Builds(Entry()));

        Assert.HasCount(1, pins);
        var pin = pins[0];
        Assert.AreEqual("win-x64", pin.Platform);
        Assert.AreEqual("%LOCALAPPDATA%/Surl/curl-port/abc/curl.exe", pin.Path);
        Assert.AreEqual(Sha256, pin.Sha256);
        Assert.AreEqual("abc", pin.Commit);
        Assert.AreEqual("dotnet publish Curl.Console", pin.PublishCommand);
        Assert.AreEqual("2026-11-04", pin.Published);
    }

    [TestMethod]
    public void Parse_SeveralEntries_KeepsFileOrder()
    {
        var pins = CurlPortClientPins.Parse(Builds(Entry(platform: "win-x64"), Entry(platform: "linux-x64")));

        CollectionAssert.AreEqual(new[] { "win-x64", "linux-x64" }, pins.Select(pin => pin.Platform).ToArray());
    }

    [TestMethod]
    public void Parse_EmptyBuildsArray_ReturnsNoPins()
    {
        Assert.IsEmpty(CurlPortClientPins.Parse("""{ "builds": [] }"""));
    }

    [TestMethod]
    public void Parse_MalformedJson_ThrowsNamingInvalidJson()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse("{ not json"));

        StringAssert.Contains(exception.Message, "CurlPortBuilds.json is not valid JSON");
    }

    [TestMethod]
    [DataRow("""{ "release": "x" }""")]
    [DataRow("""{ "builds": {} }""")]
    [DataRow("""[]""")]
    public void Parse_NoBuildsArray_ThrowsNamingBuildsArray(string json)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse(json));

        StringAssert.Contains(exception.Message, "has no builds array");
    }

    [TestMethod]
    public void Parse_EntryIsNotAnObject_ThrowsNamingTheEntry()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse("""{ "builds": [ "curl" ] }"""));

        StringAssert.Contains(exception.Message, "Entry 0 of builds");
        StringAssert.Contains(exception.Message, "is not an object");
    }

    [TestMethod]
    [DataRow("platform")]
    [DataRow("path")]
    [DataRow("sha256")]
    [DataRow("commit")]
    [DataRow("publishCommand")]
    [DataRow("published")]
    public void Parse_EntryMissingField_ThrowsNamingTheField(string field)
    {
        var fields = Fields();
        fields.Remove(field);

        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse(Builds(EntryOf(fields))));

        StringAssert.Contains(exception.Message, $"has no {field}");
    }

    [TestMethod]
    public void Parse_FieldIsBlank_ThrowsNamingTheField()
    {
        var fields = Fields();
        fields["commit"] = "  ";

        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse(Builds(EntryOf(fields))));

        StringAssert.Contains(exception.Message, "has no commit");
    }

    [TestMethod]
    public void Parse_FieldIsNotAString_ThrowsNamingTheField()
    {
        var json = Builds(Entry().Replace("\"published\": \"2026-11-04\"", "\"published\": 20261104", StringComparison.Ordinal));

        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse(json));

        StringAssert.Contains(exception.Message, "The published of entry 0");
        StringAssert.Contains(exception.Message, "is not a string");
    }

    [TestMethod]
    [DataRow("ABC")]
    [DataRow("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void Parse_Sha256NotSixtyFourHexDigits_ThrowsNamingSha256(string sha256)
    {
        var fields = Fields();
        fields["sha256"] = sha256;

        var exception = Assert.ThrowsExactly<FormatException>(() => CurlPortClientPins.Parse(Builds(EntryOf(fields))));

        StringAssert.Contains(exception.Message, "The sha256 of entry 0");
    }

    [TestMethod]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortClientPins.Parse(null!));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_RepositoryPinFile_ParsesWithoutFailure()
    {
        var path = Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), CurlPortClientPins.FileName);

        var pins = CurlPortClientPins.Parse(File.ReadAllText(path));

        Assert.IsNotNull(pins);
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["platform"] = "win-x64",
        ["path"] = "%LOCALAPPDATA%/Surl/curl-port/abc/curl.exe",
        ["sha256"] = Sha256,
        ["commit"] = "abc",
        ["publishCommand"] = "dotnet publish Curl.Console",
        ["published"] = "2026-11-04",
    };

    private static string Entry(string platform = "win-x64")
    {
        var fields = Fields();
        fields["platform"] = platform;
        return EntryOf(fields);
    }

    private static string EntryOf(Dictionary<string, string> fields) =>
        "{ " + string.Join(", ", fields.Select(field => $"\"{field.Key}\": \"{field.Value}\"")) + " }";

    private static string Builds(params string[] entries) =>
        $$"""{ "builds": [ {{string.Join(", ", entries)}} ] }""";
}
