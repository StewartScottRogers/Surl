using System.Formats.Asn1;
using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the LDAP server (BL-310): <c>ldap</c> and, through
/// <see cref="ImplicitTlsSchemeServer"/>, <c>ldaps</c>, <c>StartTLS</c> offered once a certificate
/// is configured (ADR-0072 decision 5), each bind judged by the one authentication policy, and the
/// directory - read from <c>&lt;data directory&gt;/.surl/ldap/directory.ldif</c> before any
/// listener binds with <c>--directory</c>, empty without it (ADR-0072 decision 1). No test here
/// touches the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerLdapTests
{
    private const int BindResponseTag = 1;

    private const int SearchResultEntryTag = 4;

    private const int SearchResultDoneTag = 5;

    private const int Success = 0;

    private const int NoSuchObject = 32;

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string DirectoryFilePath = Path.Join(Path.GetFullPath("served"), ".surl", "ldap", LdapDirectoryFile.FileName);

    // What pinned upstream curl 8.21.0 sends for `curl -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`:
    // a version 3 simple bind as alice, a base search of dc=example,dc=com, then an unbind
    // (Surl.Protocol.Ldap.UnitTests/Fixtures/simple-bind-base-search/request.bin).
    private static readonly byte[] CurlBindSearchUnbind = Convert.FromHexString(
        "30840000001B0201016084000000120201030405616C69636580067365637265743084000000"
        + "3E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100"
        + "020100010100870B6F626A656374436C61737330840000000030840000000502010342" + "00");

    // A base search of the root DSE for supportedExtension, filter (objectClass=*), then an unbind.
    private static readonly byte[] RootDseSupportedExtensionSearchThenUnbind = Convert.FromHexString(
        "3039020101633404000A01000A0100020100020100010100870B" + Convert.ToHexString("objectClass"u8)
        + "301404" + "12" + Convert.ToHexString("supportedExtension"u8)
        + "30050201024200");

    private static readonly byte[] ExampleDirectory = Encoding.UTF8.GetBytes(
        "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_LdapListenUrl_StartsAListenerAnsweredByTheLdapServerOverAnEmptyDirectory()
    {
        var run = await ServeOneConnectionAsync(
            CurlBindSearchUnbind, null, "-s", "--allow-plaintext-auth", "-u", "alice:secret", "ldap://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("ldap", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNull(run.TlsSettings);
        CollectionAssert.AreEqual(
            new[] { (BindResponseTag, Success), (SearchResultDoneTag, NoSuchObject) },
            ReadResponses(run.Written));
    }

    [TestMethod]
    public async Task RunAsync_LdapWithoutAllowPlaintextAuth_RefusesTheSimpleBindThroughTheAuthenticationPolicy()
    {
        var run = await ServeOneConnectionAsync(CurlBindSearchUnbind, null, "-s", "-u", "alice:secret", "ldap://127.0.0.1:0/");

        // confidentialityRequired (13), then the search refused for want of a bind: insufficientAccessRights (50).
        CollectionAssert.AreEqual(new[] { (BindResponseTag, 13), (SearchResultDoneTag, 50) }, ReadResponses(run.Written));
    }

    [TestMethod]
    public async Task RunAsync_LdapsListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheLdapServer()
    {
        var run = await ServeOneConnectionAsync(
            CurlBindSearchUnbind, null, "-s", "--self-signed", "-u", "alice:secret", "ldaps://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("ldaps", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        CollectionAssert.AreEqual(
            new[] { (BindResponseTag, Success), (SearchResultDoneTag, NoSuchObject) },
            ReadResponses(run.Written));
    }

    [TestMethod]
    public async Task RunAsync_LdapsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var run = new Run(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(run, null).RunAsync(["ldaps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) ldaps://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_LdapWithSelfSigned_MakesACertificateAndListsStartTlsInTheRootDse()
    {
        var run = await ServeOneConnectionAsync(RootDseSupportedExtensionSearchThenUnbind, null, "-s", "--self-signed", "ldap://127.0.0.1:0/");

        Assert.IsNotNull(run.TlsSettings);
        StringAssert.Contains(Encoding.Latin1.GetString(run.Written), "1.3.6.1.4.1.1466.20037");
    }

    [TestMethod]
    public async Task RunAsync_LdapWithoutACertificate_MakesNoTlsSettingsAndListsNoStartTlsInTheRootDse()
    {
        var run = await ServeOneConnectionAsync(RootDseSupportedExtensionSearchThenUnbind, null, "-s", "ldap://127.0.0.1:0/");

        Assert.IsNull(run.TlsSettings);
        Assert.DoesNotContain("1.3.6.1.4.1.1466.20037", Encoding.Latin1.GetString(run.Written));
        CollectionAssert.AreEqual(new[] { (SearchResultEntryTag, -1), (SearchResultDoneTag, Success) }, ReadResponses(run.Written));
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithADirectoryFile_AnswersTheSearchFromItsEntries()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[DirectoryFilePath] = ExampleDirectory;

        var run = await ServeOneConnectionAsync(
            CurlBindSearchUnbind, fileSystem, "-s", "--allow-plaintext-auth", "-u", "alice:secret", "--directory", "served", "ldap://127.0.0.1:0/");

        CollectionAssert.AreEqual(
            new[] { (BindResponseTag, Success), (SearchResultEntryTag, -1), (SearchResultDoneTag, Success) },
            ReadResponses(run.Written));
        StringAssert.Contains(Encoding.UTF8.GetString(run.Written), "dc=example,dc=com");
    }

    [TestMethod]
    public void ComposeLdapDirectoryFile_DataDirectory_KeepsItInTheDotSurlLdapFolderOfTheFullPath()
    {
        var file = CommandLineRunner.ComposeLdapDirectoryFile(
            ParseServing("--directory", "served", "ldap://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem());

        Assert.IsNotNull(file);
        Assert.AreEqual(DirectoryFilePath, file.FilePath);
    }

    [TestMethod]
    public void ComposeLdapDirectoryFile_NoDirectory_ComposesNone()
    {
        Assert.IsNull(CommandLineRunner.ComposeLdapDirectoryFile(ParseServing("ldap://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem()));
    }

    [TestMethod]
    public async Task LoadLdapServerAsync_MalformedFile_GivesCouldNotReadWithTheLineAndTheFault()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[DirectoryFilePath] = "objectClass: top\n"u8.ToArray();
        var commandLine = ParseServing("--directory", "served", "ldap://127.0.0.1:0/");

        var (ldapServer, failureMessage) = await CommandLineRunner.LoadLdapServerAsync(
            CommandLineRunner.ComposeLdapDirectoryFile(commandLine, fileSystem),
            AuthenticationComposition.ComposeWithoutAccounts(TimeProvider.System),
            isTlsUpgradeAvailable: false,
            TimeProvider.System,
            TestContext.CancellationToken);

        Assert.IsNull(ldapServer);
        Assert.AreEqual($"(37) Could not read {DirectoryFilePath}: line 1: a record without dn", failureMessage);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithAMalformedDirectory_WritesCouldNotReadAndReturnsCouldNotReadFileWithoutStartingAListener()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[DirectoryFilePath] = "dn: dc=example,dc=com\n"u8.ToArray();
        var factory = new FakeListenerFactory();
        var lockHolder = new FakeLockHolder();
        var events = new List<string>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory,
                _ => true,
                _ =>
                {
                    events.Add($"lock taken with {fileSystem.AccessedPaths.Count} paths read");
                    return DataDirectoryLockOutcome.Taken(lockHolder);
                },
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "ldap://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read {DirectoryFilePath}: line 1: an entry with no attribute" + NewLine, error.ToString());
        CollectionAssert.AreEqual(new[] { "lock taken with 0 paths read" }, events);
        Assert.IsEmpty(factory.StartedListenUrls);
        Assert.IsTrue(lockHolder.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectory_LoadsTheDirectoryBeforeAnyListenerStarts()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[DirectoryFilePath] = ExampleDirectory;
        var factory = new FakeListenerFactory();
        var accessedBeforeTheListenerFactory = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ =>
                {
                    accessedBeforeTheListenerFactory.AddRange(fileSystem.AccessedPaths);
                    return factory;
                },
                _ => true,
                _ => DataDirectoryLockOutcome.Taken(new FakeLockHolder()),
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "ldap://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(string.Empty, error.ToString());
        CollectionAssert.Contains(accessedBeforeTheListenerFactory, DirectoryFilePath);
    }

    // Each response's protocolOp tag and result code (-1 for an entry, which has none).
    private static (int Tag, int ResultCode)[] ReadResponses(byte[] written)
    {
        var responses = new List<(int, int)>();
        var reader = new AsnReader(written, AsnEncodingRules.BER);
        while (reader.HasData)
        {
            var message = reader.ReadSequence();
            message.ReadInteger();
            var tag = message.PeekTag();
            var operation = message.ReadSequence(tag);
            responses.Add((tag.TagValue, tag.TagValue == SearchResultEntryTag ? -1 : operation.ReadEnumeratedBytes().Span[0]));
        }

        return [.. responses];
    }

    private static SurlCommandLine ParseServing(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, UnitTestReadOnlyContentFileSystem? fileSystem, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run, fileSystem).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Written = connection.WrittenBytes;
        return run;
    }

    private static CommandLineRunner CreateRunner(Run run, UnitTestReadOnlyContentFileSystem? fileSystem) =>
        new(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, fileSystem);

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public byte[] Written { get; set; } = [];

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
