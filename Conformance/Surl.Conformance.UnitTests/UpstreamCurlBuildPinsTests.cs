namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlBuildPinsTests
{
    private const string Sha256 = "0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778";

    [TestMethod]
    public void Parse_CompleteEntry_ReadsEveryField()
    {
        var json = $$"""
            {
              "release": "8.21.0",
              "builds": [
                {
                  "platform": "win-x64",
                  "defaultPath": "C:\\curl\\curl.exe",
                  "sha256": "{{Sha256}}",
                  "version": "curl 8.21.0",
                  "protocols": "dict file  http",
                  "role": "reference"
                }
              ]
            }
            """;

        var pins = UpstreamCurlBuildPins.Parse(json);

        Assert.HasCount(1, pins);
        var pin = pins[0];
        Assert.AreEqual("win-x64", pin.Platform);
        Assert.AreEqual(@"C:\curl\curl.exe", pin.DefaultPath);
        Assert.AreEqual(Sha256, pin.Sha256);
        Assert.AreEqual("curl 8.21.0", pin.Version);
        CollectionAssert.AreEqual(new[] { "dict", "file", "http" }, pin.Protocols.ToArray());
        Assert.AreEqual(UpstreamCurlBuildRole.Reference, pin.Role);
    }

    [TestMethod]
    public void Parse_EntryWithoutRole_ReadsAsReference()
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry()));

        Assert.AreEqual(UpstreamCurlBuildRole.Reference, pins[0].Role);
    }

    [TestMethod]
    public void Parse_SupplementaryRole_ReadsAsSupplementary()
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry(extra: "\"role\": \"supplementary\"")));

        Assert.AreEqual(UpstreamCurlBuildRole.Supplementary, pins[0].Role);
    }

    [TestMethod]
    public void Parse_EntryWithoutVersionOrProtocols_ReadsThemAsEmpty()
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry()));

        Assert.AreEqual(string.Empty, pins[0].Version);
        Assert.IsEmpty(pins[0].Protocols);
    }

    [TestMethod]
    public void Parse_SeveralEntries_KeepsFileOrder()
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry("first"), Entry("second")));

        CollectionAssert.AreEqual(new[] { "first", "second" }, pins.Select(pin => pin.DefaultPath).ToArray());
    }

    [TestMethod]
    public void Parse_EmptyBuildsArray_ReturnsNoPins()
    {
        var pins = UpstreamCurlBuildPins.Parse("""{ "builds": [] }""");

        Assert.IsEmpty(pins);
    }

    [TestMethod]
    public void Parse_MalformedJson_ThrowsNamingInvalidJson()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse("{ \"builds\": ["));

        StringAssert.Contains(exception.Message, "is not valid JSON");
    }

    [TestMethod]
    public void Parse_NoBuildsProperty_ThrowsNamingBuildsArray()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse("""{ "release": "8.21.0" }"""));

        StringAssert.Contains(exception.Message, "has no builds array");
    }

    [TestMethod]
    public void Parse_BuildsIsNotAnArray_ThrowsNamingBuildsArray()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse("""{ "builds": {} }"""));

        StringAssert.Contains(exception.Message, "has no builds array");
    }

    [TestMethod]
    public void Parse_RootIsNotAnObject_ThrowsNamingBuildsArray()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse("[]"));

        StringAssert.Contains(exception.Message, "has no builds array");
    }

    [TestMethod]
    public void Parse_EntryIsNotAnObject_ThrowsNamingTheEntry()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(Entry(), "42")));

        StringAssert.Contains(exception.Message, "Entry 1 of builds");
        StringAssert.Contains(exception.Message, "is not an object");
    }

    [TestMethod]
    [DataRow("sha256")]
    [DataRow("defaultPath")]
    [DataRow("platform")]
    public void Parse_EntryMissingRequiredField_ThrowsNamingTheField(string field)
    {
        var entry = $$"""
            {
              {{string.Join(", ", RequiredFields().Where(pair => pair.Key != field).Select(pair => $"\"{pair.Key}\": \"{pair.Value}\""))}}
            }
            """;

        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(entry)));

        StringAssert.Contains(exception.Message, $"Entry 0 of builds in UpstreamCurlBuilds.json has no {field}.");
    }

    [TestMethod]
    public void Parse_RequiredFieldIsBlank_ThrowsNamingTheField()
    {
        var entry = $$"""{ "platform": "win-x64", "defaultPath": " ", "sha256": "{{Sha256}}" }""";

        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(entry)));

        StringAssert.Contains(exception.Message, "Entry 0 of builds in UpstreamCurlBuilds.json has no defaultPath.");
    }

    [TestMethod]
    public void Parse_FieldIsNotAString_ThrowsNamingTheField()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(Entry(extra: "\"version\": 8"))));

        StringAssert.Contains(exception.Message, "The version of entry 0");
        StringAssert.Contains(exception.Message, "is not a string");
    }

    [TestMethod]
    [DataRow("ABC")]
    [DataRow("ZZ773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778")]
    public void Parse_Sha256NotSixtyFourHexDigits_ThrowsNamingSha256(string sha256)
    {
        var entry = $$"""{ "platform": "win-x64", "defaultPath": "curl", "sha256": "{{sha256}}" }""";

        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(entry)));

        StringAssert.Contains(exception.Message, "The sha256 of entry 0");
        StringAssert.Contains(exception.Message, "is not 64 hexadecimal digits");
    }

    [TestMethod]
    public void Parse_UnknownRole_ThrowsNamingTheRole()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(Entry(extra: "\"role\": \"primary\""))));

        StringAssert.Contains(exception.Message, "The role of entry 0");
        StringAssert.Contains(exception.Message, "'primary'");
    }

    [TestMethod]
    public void Parse_EntryWithoutKind_ReadsAsCurl()
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry()));

        Assert.AreEqual(UpstreamCurlBuildKind.Curl, pins[0].Kind);
    }

    [TestMethod]
    [DataRow("curl", UpstreamCurlBuildKind.Curl)]
    [DataRow("library", UpstreamCurlBuildKind.Library)]
    public void Parse_Kind_ReadsTheKind(string kind, UpstreamCurlBuildKind expected)
    {
        var pins = UpstreamCurlBuildPins.Parse(Builds(Entry(extra: $"\"kind\": \"{kind}\"")));

        Assert.AreEqual(expected, pins[0].Kind);
    }

    [TestMethod]
    public void Parse_UnknownKind_ThrowsNamingTheKind()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCurlBuildPins.Parse(Builds(Entry(extra: "\"kind\": \"dll\""))));

        StringAssert.Contains(exception.Message, "The kind of entry 0");
        StringAssert.Contains(exception.Message, "'dll', not curl or library");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_RepositoryPinFile_PinsTheReferenceBuildsLibcurl()
    {
        var path = Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName);

        var pins = UpstreamCurlBuildPins.Parse(File.ReadAllText(path));

        var library = pins.Single(pin => pin.Kind == UpstreamCurlBuildKind.Library);
        Assert.AreEqual("win-x64", library.Platform);
        Assert.AreEqual(UpstreamCurlBuildRole.Reference, library.Role);
        Assert.AreEqual(@"C:\Program Files\Git\mingw64\bin\libcurl-4.dll", library.DefaultPath);
        Assert.AreEqual("799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574", library.Sha256);
    }

    [TestMethod]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlBuildPins.Parse(null!));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_RepositoryPinFile_ParsesWithoutFailure()
    {
        var path = Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName);

        var pins = UpstreamCurlBuildPins.Parse(File.ReadAllText(path));

        Assert.IsNotEmpty(pins);
        Assert.IsTrue(pins.Any(pin => pin.Platform == "win-x64" && pin.Role == UpstreamCurlBuildRole.Reference));
    }

    private static Dictionary<string, string> RequiredFields() => new()
    {
        ["platform"] = "win-x64",
        ["defaultPath"] = "curl",
        ["sha256"] = Sha256,
    };

    private static string Entry(string defaultPath = "curl", string? extra = null) =>
        $$"""{ "platform": "win-x64", "defaultPath": "{{defaultPath}}", "sha256": "{{Sha256}}"{{(extra is null ? string.Empty : ", " + extra)}} }""";

    private static string Builds(params string[] entries) =>
        $$"""{ "builds": [ {{string.Join(", ", entries)}} ] }""";

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Surl.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Surl.slnx was not found above the test output directory.");
    }
}
