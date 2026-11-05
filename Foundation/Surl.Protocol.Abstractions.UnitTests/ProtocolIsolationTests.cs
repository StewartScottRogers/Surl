using System.Xml.Linq;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Guards the rule that keeps the protocol servers independent (ADR-0002): a protocol server
/// references Abstractions and the horizontal libraries ADR-0002 lists, never another
/// protocol server, and each horizontal library references only what its row allows. The
/// project files are read from the repository, so a new reference fails here the moment it
/// is added. The extended servers, <c>Surl.Extended.Protocol.*</c>, are protocol servers under
/// the same rule (ADR-0083), and no original server may reference one.
/// </summary>
[TestClass]
public sealed class ProtocolIsolationTests
{
    private const string Abstractions = "Surl.Protocol.Abstractions.UnitLibrary";
    private const string Content = "Surl.Content.UnitLibrary";
    private const string Cryptography = "Surl.Cryptography.UnitLibrary";
    private const string BcryptPbkdf = "Surl.Cryptography.BcryptPbkdf.UnitLibrary";
    private const string Blowfish = "Surl.Cryptography.Blowfish.UnitLibrary";
    private const string Cast128 = "Surl.Cryptography.Cast128.UnitLibrary";
    private const string ChaCha20 = "Surl.Cryptography.ChaCha20.UnitLibrary";
    private const string Curve25519 = "Surl.Cryptography.Curve25519.UnitLibrary";
    private const string Ed25519 = "Surl.Cryptography.Ed25519.UnitLibrary";
    private const string Poly1305 = "Surl.Cryptography.Poly1305.UnitLibrary";
    private const string Rc4 = "Surl.Cryptography.Rc4.UnitLibrary";
    private const string Ripemd160 = "Surl.Cryptography.Ripemd160.UnitLibrary";
    private const string Dtls = "Surl.Dtls.UnitLibrary";
    private const string Hpack = "Surl.Hpack.UnitLibrary";
    private const string HttpMessage = "Surl.HttpMessage.UnitLibrary";
    private const string HttpServing = "Surl.HttpServing.UnitLibrary";
    private const string Kerberos = "Surl.Kerberos.UnitLibrary";
    private const string LineProtocol = "Surl.LineProtocol.UnitLibrary";
    private const string MailStore = "Surl.MailStore.UnitLibrary";
    private const string McpServing = "Surl.McpServing.UnitLibrary";
    private const string MessageBus = "Surl.MessageBus.UnitLibrary";
    private const string MlKem = "Surl.Cryptography.MlKem.UnitLibrary";
    private const string Qpack = "Surl.Qpack.UnitLibrary";
    private const string Quic = "Surl.Quic.UnitLibrary";
    private const string Tls13 = "Surl.Tls13.UnitLibrary";
    private const string WebSocket = "Surl.WebSocket.UnitLibrary";
    private const string ExtendedRedis = "Surl.Extended.Protocol.Redis.UnitLibrary";
    private const string ExtendedServerPrefix = "Surl.Extended.Protocol.";
    private const string Composition = "Surl.Composition.UnitLibrary";

    /// <summary>
    /// The composition roots (ADR-0099, decision 11): the hosts and the composition they share,
    /// which reference every protocol server and so may be referenced by none of them.
    /// </summary>
    private static readonly string[] CompositionRoots = ["Surl.Console", Composition, "Surl.Api", "Surl.Mcp"];

    /// <summary>
    /// ADR-0002's table, with the rows ADR-0048, ADR-0050, ADR-0051, ADR-0057, ADR-0061,
    /// ADR-0070, ADR-0081, ADR-0082, ADR-0084, ADR-0087, ADR-0100 and ADR-0103 add: each horizontal library a protocol server may reference, with the projects
    /// that library may itself reference.
    /// </summary>
    private static readonly Dictionary<string, string[]> HorizontalLibraries = new()
    {
        [Content] = [Abstractions],
        [Cryptography] = [],
        [BcryptPbkdf] = [Blowfish],
        [Blowfish] = [],
        [Cast128] = [],
        [ChaCha20] = [],
        [Curve25519] = [],
        [Ed25519] = [Curve25519],
        [Poly1305] = [],
        [Rc4] = [],
        [Ripemd160] = [],
        [Dtls] = [Abstractions, Curve25519],
        [Hpack] = [],
        [HttpMessage] = [Abstractions],
        [HttpServing] = [Abstractions, HttpMessage, Content],
        [Kerberos] = [],
        [LineProtocol] = [Abstractions],
        [MailStore] = [Abstractions, Content],
        [McpServing] = [],
        [MessageBus] = [Abstractions, Content],
        [Qpack] = [Hpack],
        [Quic] = [Abstractions, Tls13, ChaCha20, Poly1305],
        [Tls13] = [Curve25519, MlKem],
        [WebSocket] = [Abstractions, HttpMessage],
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
    public void EveryOriginalProtocolServer_ReferencesNoExtendedServer()
    {
        var violations = new List<string>();

        foreach (var project in ProtocolServers().Where(path => !IsExtendedServer(ProjectName(path))))
        {
            foreach (var referenced in ExtendedServerReferences(ProjectReferences(project)))
            {
                violations.Add($"{ProjectName(project)} -> {referenced}");
            }
        }

        Assert.IsEmpty(
            violations,
            "An original protocol server may not reference an extended server (ADR-0083): "
                + string.Join(", ", violations));
    }

    [TestMethod]
    [DataRow(ExtendedRedis, "Surl.Protocol.Http.UnitLibrary")]
    [DataRow(ExtendedRedis, "Surl.Extended.Protocol.Mqtt5.UnitLibrary")]
    [DataRow("Surl.Protocol.Http.UnitLibrary", ExtendedRedis)]
    public void ForbiddenProtocolReferences_ServerReferencingAnotherServer_IsForbidden(
        string server, string referenced)
    {
        var forbidden = ForbiddenProtocolReferences([Abstractions, referenced]);

        Assert.HasCount(1, forbidden, $"{server} -> {referenced} should be forbidden.");
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    [DataRow(ExtendedRedis, true)]
    [DataRow("Surl.Protocol.Http.UnitLibrary", false)]
    [DataRow(Abstractions, false)]
    [DataRow(HttpMessage, false)]
    public void IsExtendedServer_ProjectName_SaysWhetherItIsAnExtendedServer(string projectName, bool expected)
    {
        Assert.AreEqual(expected, IsExtendedServer(projectName));
    }

    [TestMethod]
    public void ExtendedServerReferences_OriginalServerReferencingAnExtendedServer_ReturnsIt()
    {
        var found = ExtendedServerReferences([Abstractions, HttpMessage, ExtendedRedis]);

        Assert.HasCount(1, found);
        Assert.AreEqual(ExtendedRedis, found[0]);
    }

    [TestMethod]
    public void EveryHorizontalLibrary_ReferencesOnlyItsRow()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

        foreach (var library in HorizontalLibraries.Keys)
        {
            var project = ProjectFile(root, library);

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
    [DataRow(Blowfish)]
    [DataRow(Cast128)]
    [DataRow(ChaCha20)]
    [DataRow(Curve25519)]
    [DataRow(Ed25519)]
    [DataRow(Poly1305)]
    [DataRow(Rc4)]
    [DataRow(Ripemd160)]
    [DataRow(Dtls)]
    [DataRow(Hpack)]
    [DataRow(HttpMessage)]
    [DataRow(HttpServing)]
    [DataRow(Kerberos)]
    [DataRow(LineProtocol)]
    [DataRow(MailStore)]
    [DataRow(McpServing)]
    [DataRow(MessageBus)]
    [DataRow(Qpack)]
    [DataRow(Quic)]
    [DataRow(Tls13)]
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
    [DataRow("Surl.Composition.UnitLibrary")]
    [DataRow("Surl.Api")]
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
    [DataRow(BcryptPbkdf, Blowfish)]
    [DataRow(Ed25519, Curve25519)]
    [DataRow(HttpMessage, Abstractions)]
    [DataRow(Dtls, Abstractions)]
    [DataRow(Dtls, Curve25519)]
    [DataRow(HttpServing, Content)]
    [DataRow(HttpServing, HttpMessage)]
    [DataRow(LineProtocol, Abstractions)]
    [DataRow(MailStore, Abstractions)]
    [DataRow(MailStore, Content)]
    [DataRow(Qpack, Hpack)]
    [DataRow(Quic, Abstractions)]
    [DataRow(Quic, Tls13)]
    [DataRow(Quic, ChaCha20)]
    [DataRow(Quic, Poly1305)]
    [DataRow(Tls13, Curve25519)]
    [DataRow(Tls13, MlKem)]
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
    [DataRow(Blowfish, BcryptPbkdf)]
    [DataRow(Cast128, Blowfish)]
    [DataRow(Kerberos, Cryptography)]
    [DataRow(Kerberos, Abstractions)]
    [DataRow(Ed25519, Cryptography)]
    [DataRow(Hpack, Abstractions)]
    [DataRow(Hpack, HttpMessage)]
    [DataRow(HttpMessage, Content)]
    [DataRow(HttpMessage, LineProtocol)]
    [DataRow(HttpMessage, "Surl.Protocol.Http.UnitLibrary")]
    [DataRow(Dtls, Tls13)]
    [DataRow(HttpServing, "Surl.Protocol.Http.UnitLibrary")]
    [DataRow(HttpServing, LineProtocol)]
    [DataRow(HttpMessage, "Surl.Networking.UnitLibrary")]
    [DataRow(LineProtocol, Content)]
    [DataRow(LineProtocol, MailStore)]
    [DataRow(MailStore, LineProtocol)]
    [DataRow(MailStore, "Surl.Networking.UnitLibrary")]
    [DataRow(Qpack, Abstractions)]
    [DataRow(Qpack, Quic)]
    [DataRow(Quic, Hpack)]
    [DataRow(Tls13, Abstractions)]
    [DataRow(Tls13, Cryptography)]
    [DataRow(Tls13, "Surl.Protocol.Http.UnitLibrary")]
    public void ForbiddenHorizontalReferences_ReferenceOutsideItsRow_IsForbidden(
        string library, string referenced)
    {
        var forbidden = ForbiddenHorizontalReferences(library, [referenced]);

        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    public void EveryProjectOutsideTheHostsAndConformance_ReferencesNoCompositionRoot()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

        foreach (var project in ProjectsUnder(root, "Foundation", "Cryptography", "Protocols"))
        {
            foreach (var referenced in CompositionRootReferences(ProjectReferences(project)))
            {
                violations.Add($"{ProjectName(project)} -> {referenced}");
            }
        }

        Assert.IsEmpty(
            violations,
            "No protocol server or horizontal library may reference a composition root (ADR-0099, decision 11): "
                + string.Join(", ", violations));
    }

    [TestMethod]
    public void Composition_ReferencesNoHost()
    {
        var project = ProjectFile(RepositoryRoot(), Composition);

        var hosts = CompositionRootReferences(ProjectReferences(project));

        Assert.IsEmpty(
            hosts,
            $"{Composition} may reference no host (ADR-0099, decision 11): " + string.Join(", ", hosts));
    }

    [TestMethod]
    [DataRow("Surl.Console")]
    [DataRow(Composition)]
    [DataRow("Surl.Api")]
    [DataRow("Surl.Mcp")]
    public void CompositionRootReferences_CompositionRoot_IsFound(string referenced)
    {
        var found = CompositionRootReferences([Abstractions, referenced]);

        Assert.HasCount(1, found);
        Assert.AreEqual(referenced, found[0]);
    }

    [TestMethod]
    public void Abstractions_ReferencesNothing()
    {
        var project = ProjectFile(RepositoryRoot(), Abstractions);

        var references = ProjectReferences(project);

        Assert.IsEmpty(
            references,
            $"{Abstractions} must reference nothing: " + string.Join(", ", references));
    }

    [TestMethod]
    public void EveryProtocolServer_HasAMatchingTestProjectBesideIt()
    {
        foreach (var project in ProtocolServers())
        {
            var tests = ProjectName(project).Replace(".UnitLibrary", ".UnitTests");
            var categoryFolder = Path.GetDirectoryName(Path.GetDirectoryName(project))!;

            Assert.IsTrue(
                File.Exists(Path.Combine(categoryFolder, tests, tests + ".csproj")),
                $"{tests} is missing from {categoryFolder}.");
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

    /// <summary>
    /// Returns the references that name an extended server, which no original protocol server
    /// may have (ADR-0083).
    /// </summary>
    private static List<string> ExtendedServerReferences(IEnumerable<string> references) =>
        references.Where(IsExtendedServer).ToList();

    private static bool IsExtendedServer(string projectName) =>
        projectName.StartsWith(ExtendedServerPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Returns the references that name a composition root, which no protocol server, horizontal
    /// library or (for <c>Surl.Composition</c>) composition may have (ADR-0099, decision 11).
    /// </summary>
    private static List<string> CompositionRootReferences(IEnumerable<string> references) =>
        references.Where(referenced => CompositionRoots.Contains(referenced)).ToList();

    /// <summary>
    /// Returns every csproj under the named category folders of the repository, outside build output.
    /// </summary>
    private static IEnumerable<string> ProjectsUnder(string root, params string[] categoryFolders) =>
        categoryFolders
            .Select(folder => Path.Combine(root, folder))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !IsUnderBuildOutput(root, path));

    /// <summary>
    /// Returns every protocol server's csproj: the original servers,
    /// <c>Surl.Protocol.*.UnitLibrary</c> except Abstractions, and the extended servers,
    /// <c>Surl.Extended.Protocol.*.UnitLibrary</c> (ADR-0083).
    /// </summary>
    private static IEnumerable<string> ProtocolServers()
    {
        var root = RepositoryRoot();

        return new[] { "Surl.Protocol.*.UnitLibrary.csproj", ExtendedServerPrefix + "*.UnitLibrary.csproj" }
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(path => !IsUnderBuildOutput(root, path) && ProjectName(path) != Abstractions);
    }

    /// <summary>
    /// Returns the path of the named project's csproj, in whichever category folder holds
    /// it (CLAUDE.md, "Repository layout").
    /// </summary>
    private static string ProjectFile(string root, string projectName)
    {
        var matches = Directory.EnumerateFiles(root, projectName + ".csproj", SearchOption.AllDirectories)
            .Where(path => !IsUnderBuildOutput(root, path))
            .ToList();

        Assert.HasCount(1, matches, $"{projectName}.csproj should exist exactly once.");

        return matches[0];
    }

    private static bool IsUnderBuildOutput(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or ".git");

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
