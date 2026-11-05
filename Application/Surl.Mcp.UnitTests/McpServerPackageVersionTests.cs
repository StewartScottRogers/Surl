using System.Text.Json;
using System.Xml.Linq;

namespace Surl.Mcp;

/// <summary>
/// The Surl.Mcp tool package's version is stated twice, in Surl.Mcp.csproj and in .mcp/server.json, the file the
/// MCP registry reads (ADR-0103 decision 7); this holds the two equal.
/// </summary>
[TestClass]
public sealed class McpServerPackageVersionTests
{
    [TestMethod]
    public void ServerJson_Versions_EqualTheCsprojVersion()
    {
        var projectDirectory = Path.Combine(FindRepositoryRoot(), "Application", "Surl.Mcp");
        var csprojVersion = XDocument.Load(Path.Combine(projectDirectory, "Surl.Mcp.csproj"))
            .Descendants("Version").Single().Value;
        using var serverJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectDirectory, ".mcp", "server.json")));
        var package = serverJson.RootElement.GetProperty("packages").EnumerateArray().Single();

        Assert.AreEqual(csprojVersion, serverJson.RootElement.GetProperty("version").GetString());
        Assert.AreEqual(csprojVersion, package.GetProperty("version").GetString());
        Assert.AreEqual("Surl.Mcp", package.GetProperty("identifier").GetString());
        Assert.AreEqual("stdio", package.GetProperty("transport").GetProperty("type").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Surl.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Surl.slnx was not found above the test output directory.");
    }
}
