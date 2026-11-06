using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapDirectoryFixture;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Loads the directory from <c>&lt;path&gt;/.surl/ldap/directory.ldif</c> over an in-memory file
/// system (ADR-0072 decision 1): the fixture file is searched, a missing file is an empty
/// directory, and a file that cannot be read or cannot be the directory is refused with its path
/// and the decision's text.
/// </summary>
[TestClass]
public sealed class LdapDirectoryFileTests
{
    private static readonly string StateFolderPath = Path.Join(InMemoryContentFileSystem.RootPath, ".surl", "ldap");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NoFileSystemOrNoFolder_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapDirectoryFile(null!, StateFolderPath));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapDirectoryFile(NewFileSystem(), null!));
        Assert.ThrowsExactly<ArgumentException>(() => new LdapDirectoryFile(NewFileSystem(), string.Empty));
    }

    [TestMethod]
    public void FilePath_IsDirectoryLdifInTheStateFolder()
    {
        Assert.AreEqual(Path.Join(StateFolderPath, "directory.ldif"), new LdapDirectoryFile(NewFileSystem(), StateFolderPath).FilePath);
    }

    [TestMethod]
    public async Task LoadAsync_TheFixtureFile_SearchesItsEntriesInFileOrder()
    {
        var directory = await LoadAsync(ReadDirectoryFile("people.ldif"));

        var outcome = directory.Search(Search("DC=Example,DC=Com", LdapSearchScope.WholeSubtree));

        Assert.AreEqual(
            "dc=example,dc=com|cn=alice,dc=example,dc=com|cn=béb,dc=example,dc=com|ou=staff,dc=example,dc=com|uid=carol,ou=staff,dc=example,dc=com",
            DnsOf(outcome));
        Assert.AreEqual(
            "objectClass=person;cn=alice,Alice;sn=Smith;description=A person whose description is folded over two lines.;mail=alice@example.com",
            AttributesOf(outcome.Entries[1]));
        Assert.AreEqual("objectClass=person;cn=béb;sn=Jones", AttributesOf(outcome.Entries[2]));
        Assert.AreEqual("objectClass=person;uid=carol;cn;lang-en=Carol;2.5.4.4=Ann", AttributesOf(outcome.Entries[4]));
        CollectionAssert.AreEqual(new[] { "dc=example,dc=com" }, directory.NamingContexts.ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_TheFixtureFile_FiltersByEachTypesMatchingRule()
    {
        var directory = await LoadAsync(ReadDirectoryFile("people.ldif"));

        var outcome = directory.Search(Search("dc=example,dc=com", LdapSearchScope.WholeSubtree, new LdapComparisonFilter(LdapComparison.EqualityMatch, "cn", Utf8("BÉB"))));

        Assert.AreEqual("cn=béb,dc=example,dc=com", DnsOf(outcome));
    }

    [TestMethod]
    public async Task LoadAsync_NoFile_IsAnEmptyDirectory()
    {
        var directory = await new LdapDirectoryFile(NewFileSystem(), StateFolderPath).LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(directory.Entries);
        Assert.AreEqual(LdapResultCode.NoSuchObject, directory.Search(Search("dc=example,dc=com", LdapSearchScope.BaseObject)).Result.ResultCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_TheFileCannotBeRead_IsRefusedWithItsPathAndTheFailure(bool isAccessDenied)
    {
        Exception failure = isAccessDenied ? new UnauthorizedAccessException("Access is denied.") : new IOException("The disk failed.");
        var file = new LdapDirectoryFile(new UnitTestUnreadableContentFileSystem(failure), StateFolderPath);

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => file.LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual(file.FilePath, exception.FilePath);
        Assert.AreEqual(failure.Message, exception.Message);
        Assert.AreSame(failure, exception.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_AFailureThatIsNotStorage_IsNotCaught()
    {
        var file = new LdapDirectoryFile(new UnitTestUnreadableContentFileSystem(new InvalidOperationException("A defect.")), StateFolderPath);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => file.LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("dn: o=a\no: a\n\nversion: 2\n", "line 4: a record without dn")]
    [DataRow("dn: o=a\no: a\n\ndn: o=b\n", "line 4: an entry with no attribute")]
    [DataRow("dn:\no: a\n", "line 1: a record without dn")]
    [DataRow("dn: o=a\no: a\n\n# again\ndn: O=A\no: b\n", "line 5: a duplicate DN")]
    [DataRow("dn: o=a\no: a\n\ndn: cn=x,ou=missing,o=a\ncn: x\n", "line 4: a parent DN that is not in the file")]
    public async Task LoadAsync_AFileThatCannotBeTheDirectory_IsRefusedWithItsPathLineAndText(string ldif, string reason)
    {
        var file = WriteDirectoryFile(Encoding.UTF8.GetBytes(ldif));

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => file.LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual(file.FilePath, exception.FilePath);
        Assert.AreEqual(reason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_MoreEntriesThanTheBound_IsPastTheDirectorysBoundsAtTheFirstEntryPastIt()
    {
        var file = WriteDirectoryFile("dn: o=a\no: a\n\ndn: ou=b,o=a\nou: b\n\ndn: ou=c,o=a\nou: c\n"u8.ToArray());

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => file.LoadAsync(TimeProvider.System, maxEntries: 2, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual("line 7: past the directory's bounds", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_MoreBytesThanTheBound_IsPastTheDirectorysBounds()
    {
        var file = WriteDirectoryFile("dn: o=a\no: a\n\ndn: ou=b,o=a\nou: b\n"u8.ToArray());

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => file.LoadAsync(TimeProvider.System, maxTotalBytes: 10, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual("line 4: past the directory's bounds", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AValuePastItsBound_IsPastTheDirectorysBounds()
    {
        var value = Convert.ToBase64String(new byte[LdapDirectory.MaxValueBytes + 1]);
        var file = WriteDirectoryFile(Encoding.ASCII.GetBytes($"dn: o=a\no: a\njpegPhoto:: {value}\n"));

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => file.LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual("line 1: past the directory's bounds", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AValueAtItsBound_IsHeld()
    {
        var value = Convert.ToBase64String(new byte[LdapDirectory.MaxValueBytes]);
        var file = WriteDirectoryFile(Encoding.ASCII.GetBytes($"dn: o=a\no: a\njpegPhoto:: {value}\n"));

        var directory = await file.LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(LdapDirectory.MaxValueBytes, directory.Entries[0].Attributes[1].Values[0]);
    }

    [TestMethod]
    public async Task ServerLoadAsync_NoArgument_Throws()
    {
        var file = WriteDirectoryFile([]);
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var sasl = new UnitTestSaslAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => LdapProtocolServer.LoadAsync(file, policy, null!, TimeProvider.System, cancellationToken: TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => LdapProtocolServer.LoadAsync(null!, policy, sasl, TimeProvider.System, cancellationToken: TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => LdapProtocolServer.LoadAsync(file, null!, sasl, TimeProvider.System, cancellationToken: TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => LdapProtocolServer.LoadAsync(file, policy, sasl, null!, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServerLoadAsync_TheFixtureFile_ServesItsEntries()
    {
        var server = await LdapProtocolServer.LoadAsync(
            WriteDirectoryFile(ReadDirectoryFile("people.ldif")),
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted),
            new UnitTestSaslAuthenticationPolicy(),
            TimeProvider.System,
            cancellationToken: TestContext.CancellationToken);
        var connection = new InMemoryConnection([
            Message(1, SimpleBind(3, "alice", "secret")),
            Message(2, Search(Present("objectClass"), baseObject: "ou=staff,dc=example,dc=com", scope: 1, attributes: "uid")),
        ]);

        await server.ServeAsync(connection, Context(TestContext.CancellationToken));

        var transcript = LdapResponseTranscript.Of(connection.WrittenBytes);
        Assert.AreEqual("#2 searchResultDone success", transcript[^1]);
        StringAssert.Contains(transcript[^2], "uid=carol,ou=staff,dc=example,dc=com");
    }

    [TestMethod]
    public async Task ServerLoadAsync_AMalformedFile_IsRefusedBeforeAnyServer()
    {
        var file = WriteDirectoryFile("dn: o=a\no:< file:///etc/passwd\n"u8.ToArray());

        var exception = await Assert.ThrowsExactlyAsync<LdapDirectoryLoadException>(() => LdapProtocolServer.LoadAsync(
            file, new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), new UnitTestSaslAuthenticationPolicy(), TimeProvider.System, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual("line 2: a URL value", exception.Message);
        Assert.IsInstanceOfType<LdifFormatException>(exception.InnerException);
    }

    private static InMemoryContentFileSystem NewFileSystem() => new(TimeProvider.System);

    private static byte[] ReadDirectoryFile(string name)
    {
        using var stream = typeof(LdapDirectoryFileTests).Assembly.GetManifestResourceStream($"DirectoryFiles/{name}")
            ?? throw new InvalidOperationException($"No embedded directory file DirectoryFiles/{name}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static LdapDirectoryFile WriteDirectoryFile(byte[] bytes)
    {
        var fileSystem = NewFileSystem();
        fileSystem.CreateDirectory(StateFolderPath);
        var file = new LdapDirectoryFile(fileSystem, StateFolderPath);
        using (var stream = fileSystem.CreateFileForAsyncWrite(file.FilePath))
        {
            stream.Write(bytes);
        }

        return file;
    }

    private Task<LdapDirectory> LoadAsync(byte[] bytes) =>
        WriteDirectoryFile(bytes).LoadAsync(TimeProvider.System, cancellationToken: TestContext.CancellationToken);
}
