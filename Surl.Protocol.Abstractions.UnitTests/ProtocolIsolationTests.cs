using System.Xml.Linq;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Guards the rule that keeps the protocol servers independent (ADR-0002): a protocol server
/// references Abstractions and the horizontal libraries ADR-0002 lists, never another
/// protocol server, and each horizontal library references only what its row allows. The
/// project files are read from the repository, so a new reference fails here the moment it
/// is added.
/// </summary>
[TestClass]
public sealed class ProtocolIsolationTests
{
    private const string Abstractions = "Surl.Protocol.Abstractions.UnitLibrary";
    private const string Content = "Surl.Content.UnitLibrary";
    private const string Cryptography = "Surl.Cryptography.UnitLibrary";
    private const string BcryptPbkdf = "Surl.Cryptography.BcryptPbkdf.UnitLibrary";
    private const string ChaCha20 = "Surl.Cryptography.ChaCha20.UnitLibrary";
    private const string Curve25519 = "Surl.Cryptography.Curve25519.UnitLibrary";
    private const string Ed25519 = "Surl.Cryptography.Ed25519.UnitLibrary";
    private const string Poly1305 = "Surl.Cryptography.Poly1305.UnitLibrary";
    private const string LineProtocol = "Surl.LineProtocol.UnitLibrary";
    private const string MailStore = "Surl.MailStore.UnitLibrary";

    /// <summary>
    /// ADR-0002's table, with the rows ADR-0048, ADR-0050 and ADR-0051 add: each horizontal
    /// library a protocol server may reference, with the projects that library may itself
    /// reference.
    /// </summary>
    private static readonly Dictionary<string, string[]> HorizontalLibraries = new()
    {
        [Content] = [Abstractions],
        [Cryptography] = [],
        [BcryptPbkdf] = [],
        [ChaCha20] = [],
        [Curve25519] = [],
        [Ed25519] = [Curve25519],
        [Poly1305] = [],
        [LineProtocol] = [Abstractions],
        [MailStore] = [Abstractions, Content],
    };

    [TestMethod]
    public void EveryProtocolServer_ReferencesOnlyAbstractionsAndHorizontalLibraries()
    {
        var violations = new List<string>();

        foreach (var project in ProtocolServers())
        {
            foreach (var referenced in ForbiddenProtocolReferences(ProjectReferences(project)))
            {
                violations.Add($"{ProjectName(project)} -> {referenced}");
            }
        }

        Assert.IsEmpty(
            violations,
            "A protocol server may reference only Abstractions and ADR-0002's horizontal libraries: "
                + string.Join(", ", violations));
    }

    [TestMethod]
    public void EveryHorizontalLibrary_ReferencesOnlyItsRow()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

        foreach (var library in HorizontalLibraries.Keys)
        {
            var project = Path.Combine(root, library, library + ".csproj");

            foreach (var referenced in ForbiddenHorizontalReferences(library, ProjectReferences(project)))
            {
                violations.Add($"{library} -> {referenced}");
            }
        }

        Assert.IsEmpty(
            violations,
            "A horizontal library may reference only its row of ADR-0002: " + string.Join(", ", violations));
    }

    [TestMethod]
    [DataRow(Abstractions)]
    [DataRow(Content)]
    [DataRow(Cryptography)]
    [DataRow(BcryptPbkdf)]
    [DataRow(ChaCha20)]
    [DataRow(Curve25519)]
    [DataRow(Ed25519)]
    [DataRow(Poly1305)]
    [DataRow(LineProtocol)]
    [DataRow(MailStore)]
    public void ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden(string referenced)
    {
        Assert.IsEmpty(ForbiddenProtocolReferences([referenced]));
    }

    [TestMethod]
    [DataRow("Surl.Protocol.Http.UnitLibrary")]
    [DataRow("Surl.Protocol.Ftp.UnitLibrary")]
    [DataRow("Surl.Networking.UnitLibrary")]
    [DataRow("Surl.Core.UnitLibrary")]
    [DataRow("Surl.Console")]
    [DataRow("Surl.Authentication.UnitLibrary")]
    [DataRow("Surl.Cli.UnitLibrary")]
    public void ForbiddenProtocolReferences_OtherProject_IsForbidden(string referenced)
    {
        var forbidden = ForbiddenProtocolReferences([Abstractions, referenced]);

        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    [DataRow(Content, Abstractions)]
    [DataRow(Ed25519, Curve25519)]
    [DataRow(LineProtocol, Abstractions)]
    [DataRow(MailStore, Abstractions)]
    [DataRow(MailStore, Content)]
    public void ForbiddenHorizontalReferences_ReferenceInItsRow_IsNotForbidden(
        string library, string referenced)
    {
        Assert.IsEmpty(ForbiddenHorizontalReferences(library, [referenced]));
    }

    [TestMethod]
    [DataRow(Cryptography, Abstractions)]
    [DataRow(Content, Cryptography)]
    [DataRow(Content, "Surl.Protocol.Http.UnitLibrary")]
    [DataRow(Content, "Surl.Networking.UnitLibrary")]
    [DataRow(Cryptography, "Surl.Core.UnitLibrary")]
    [DataRow(Curve25519, Ed25519)]
    [DataRow(ChaCha20, Poly1305)]
    [DataRow(Poly1305, ChaCha20)]
    [DataRow(Ed25519, Cryptography)]
    [DataRow(LineProtocol, Content)]
    [DataRow(LineProtocol, MailStore)]
    [DataRow(MailStore, LineProtocol)]
    [DataRow(MailStore, "Surl.Networking.UnitLibrary")]
    public void ForbiddenHorizontalReferences_ReferenceOutsideItsRow_IsForbidden(
        string library, string referenced)
    {
        var forbidden = ForbiddenHorizontalReferences(library, [referenced]);

        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    public void Abstractions_ReferencesNothing()
    {
        var project = Path.Combine(RepositoryRoot(), Abstractions, Abstractions + ".csproj");

        var references = ProjectReferences(project);

        Assert.IsEmpty(
            references,
            $"{Abstractions} must reference nothing: " + string.Join(", ", references));
    }

    [TestMethod]
    public void EveryProtocolServer_HasAMatchingTestProject()
    {
        var root = RepositoryRoot();

        foreach (var project in ProtocolServers())
        {
            var tests = ProjectName(project).Replace(".UnitLibrary", ".UnitTests");

            Assert.IsTrue(
                File.Exists(Path.Combine(root, tests, tests + ".csproj")),
                $"{tests} is missing.");
        }
    }

    /// <summary>
    /// Returns the references a protocol server may not have: everything except Abstractions
    /// and the horizontal libraries ADR-0002 lists.
    /// </summary>
    private static List<string> ForbiddenProtocolReferences(IEnumerable<string> references) =>
        references
            .Where(referenced => referenced != Abstractions
                && !HorizontalLibraries.ContainsKey(referenced))
            .ToList();

    /// <summary>
    /// Returns the references the named horizontal library may not have: everything outside
    /// its row of ADR-0002.
    /// </summary>
    private static List<string> ForbiddenHorizontalReferences(
        string library, IEnumerable<string> references) =>
        references
            .Where(referenced => !HorizontalLibraries[library].Contains(referenced))
            .ToList();

    private static IEnumerable<string> ProtocolServers() =>
        Directory.EnumerateFiles(RepositoryRoot(), "Surl.Protocol.*.UnitLibrary.csproj",
                SearchOption.AllDirectories)
            .Where(path => ProjectName(path) != Abstractions);

    private static string ProjectName(string projectPath) =>
        Path.GetFileNameWithoutExtension(projectPath);

    private static IReadOnlyList<string> ProjectReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToList();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Surl.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory);

        return directory!.FullName;
    }
}
