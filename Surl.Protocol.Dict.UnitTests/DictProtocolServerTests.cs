using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class DictProtocolServerTests
{
    private const string Banner = "220 surl DICT server <mime> <1@surl>\r\n";
    private const string Ok = "250 ok\r\n";
    private const string Bye = "221 bye\r\n";
    private const string NoMatch = "552 no match\r\n";
    private const string SyntaxError = "501 syntax error, illegal parameters\r\n";
    private const string InvalidDatabase = "550 invalid database, use \"SHOW DB\" for list of databases\r\n";
    private const string DatabaseLine = "surl \"Files served by surl\"\r\n";
    private const string MimeHeader = "Content-type: text/plain; charset=utf-8\r\nContent-transfer-encoding: 8bit\r\n\r\n";

    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-dict-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullContentStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DictProtocolServer(null!));
    }

    [TestMethod]
    public void Schemes_IsDictOnly()
    {
        var server = new DictProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));

        CollectionAssert.AreEqual(new[] { "dict" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new DictProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(connection, null!));
    }

    [TestMethod]
    [DataRow("define-hello", false)]
    [DataRow("define-hello", true)]
    [DataRow("match-hel", false)]
    [DataRow("match-hel", true)]
    [DataRow("bare-hello", false)]
    [DataRow("define-missing", false)]
    [DataRow("show-db", false)]
    [DataRow("show-db", true)]
    public async Task ServeAsync_RecordedRequest_SendsTheRepliesUpstreamCurlAccepted(string caseName, bool oneBytePerRead)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);

        var (connection, _) = await ServeAsync(chunks);

        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "stdout.bin"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_BareWordFromCurl_Answers500UnknownCommand()
    {
        var reply = await ServeTextAsync(Encoding.ASCII.GetString(RecordedFixture.ReadRequestBytes("bare-hello")));

        Assert.AreEqual(Banner + Ok + "500 unknown command\r\n" + Bye, reply);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("LOOKUP hello")]
    public async Task ServeAsync_UnknownCommandOrBlankLine_Answers500AndKeepsReading(string line)
    {
        var reply = await ServeTextAsync(line + "\r\nQUIT\r\n");

        Assert.AreEqual(Banner + "500 unknown command\r\n" + Bye, reply);
    }

    [TestMethod]
    [DataRow("DEFINE ! missing")]
    [DataRow("DEFINE ! .hidden")]
    [DataRow("DEFINE ! sub")]
    [DataRow("DEFINE ! a:b")]
    [DataRow("DEFINE ! \"\"")]
    [DataRow("DEFINE ! HELLO")]
    public async Task ServeAsync_WordWithNoDefinition_Answers552(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + NoMatch, reply);
    }

    [TestMethod]
    [DataRow("!")]
    [DataRow("*")]
    [DataRow("surl")]
    public async Task ServeAsync_DefineInEveryDatabaseName_SendsTheDefinition(string database)
    {
        var reply = await ServeTextAsync($"define {database} hello\r\n");

        Assert.AreEqual(Banner + Definition("hello", "A greeting.\r\n"), reply);
    }

    [TestMethod]
    public async Task ServeAsync_DefinitionText_IsSentWithCrlfLineEndsAndDotStuffing()
    {
        var fileSystem = StandardFileSystem()
            .AddFile(Path.Join(Root, "dots"), Encoding.UTF8.GetBytes(".one\nt.wo\r\n..three\rfour"), FileTime);

        var reply = await ServeTextAsync("DEFINE ! dots\r\n", fileSystem);

        Assert.AreEqual(Banner + Definition("dots", "..one\r\nt.wo\r\n...three\r\nfour\r\n"), reply);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyDefinition_SendsOnlyTheTerminatingDot()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "empty"), [], FileTime);

        var reply = await ServeTextAsync("DEFINE ! empty\r\n", fileSystem);

        Assert.AreEqual(Banner + Definition("empty", string.Empty), reply);
    }

    [TestMethod]
    public async Task ServeAsync_DefinitionShrinksWhileSent_EndsTheTextAtWhatWasRead()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "shrinks"), "abc"u8.ToArray(), FileTime, reportedLength: 5);

        var reply = await ServeTextAsync("DEFINE ! shrinks\r\n", fileSystem);

        Assert.AreEqual(Banner + Definition("shrinks", "abc\r\n"), reply);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_DefinitionUnreadableOnceAnnounced_AbortsTheConnectionAndNotesIt(bool accessDenied)
    {
        Exception failure = accessDenied ? new UnauthorizedAccessException("denied") : new FileNotFoundException("gone");
        var fileSystem = StandardFileSystem().AddUnreadableFile(Path.Join(Root, "vanishes"), failure);
        var log = new RecordingExchangeLog();

        var (connection, _) = await ServeAsync([Encoding.ASCII.GetBytes("DEFINE ! vanishes\r\nSTATUS\r\n")], fileSystem, log: log);

        Assert.AreEqual(Banner + "150 1 definitions retrieved\r\n151 \"vanishes\" " + DatabaseLine, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(
            $"{Path.Join(Root, "vanishes")} could not be read while it was sent ({failure.Message}); the connection was aborted.",
            log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_EscapedWordFromCurl_IsLookedUpUnescapedAndQuotedInTheReply()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "a b\"c"), "x"u8.ToArray(), FileTime);

        var reply = await ServeTextAsync("DEFINE db a\\ b\\\"c\r\nDEFINE ! a\\ b\\\"c\r\n", fileSystem);

        Assert.AreEqual(Banner + InvalidDatabase + Definition("a b\\\"c", "x\r\n"), reply);
    }

    [TestMethod]
    [DataRow("DEFINE !")]
    [DataRow("DEFINE ! a b")]
    [DataRow("MATCH ! .")]
    [DataRow("MATCH ! . a b")]
    [DataRow("CLIENT")]
    [DataRow("SHOW")]
    [DataRow("SHOW DB extra")]
    [DataRow("SHOW INFO")]
    [DataRow("SHOW NONSENSE")]
    [DataRow("OPTION")]
    [DataRow("OPTION PLAIN")]
    [DataRow("DEFINE ! \"unclosed")]
    [DataRow("DEFINE ! trailing\\")]
    public async Task ServeAsync_IllegalParameters_Answers501(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + SyntaxError, reply);
    }

    [TestMethod]
    [DataRow("DEFINE other hello")]
    [DataRow("MATCH other . hel")]
    [DataRow("SHOW INFO !")]
    [DataRow("SHOW INFO *")]
    [DataRow("SHOW INFO other")]
    public async Task ServeAsync_UnknownDatabase_Answers550(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + InvalidDatabase, reply);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownStrategy_Answers551()
    {
        var reply = await ServeTextAsync("MATCH ! soundex hel\r\n");

        Assert.AreEqual(Banner + "551 invalid strategy, use \"SHOW STRAT\" for a list of strategies\r\n", reply);
    }

    [TestMethod]
    [DataRow("MATCH ! prefix he", "surl \"hello\"\r\nsurl \"help\"\r\n", 2)]
    [DataRow("MATCH surl exact hello", "surl \"hello\"\r\n", 1)]
    [DataRow("MATCH * . w", "surl \"world\"\r\n", 1)]
    [DataRow("MATCH ! . \"\"", "surl \"hello\"\r\nsurl \"help\"\r\nsurl \"world\"\r\n", 3)]
    public async Task ServeAsync_Match_ListsTheMatchingFilesButNoDirectoryOrDotFile(string line, string matches, int count)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + $"152 {count} matches found\r\n" + matches + ".\r\n" + Ok, reply);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedMatchWithListingsOff_SendsTheRepliesUpstreamCurlAccepted()
    {
        var (connection, _) = await ServeAsync(
            RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("match-hel")),
            exposureOptions: new ContentExposureOptions { ListDirectories = false });

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("match-hel", "stdout.bin"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_MatchWithListingsOff_LeavesOutDirectoriesAndDotFiles(bool serveDotFiles)
    {
        var options = new ContentExposureOptions { ListDirectories = false, ServeDotFiles = serveDotFiles };

        var reply = await ServeTextAsync("MATCH ! . \"\"\r\n", exposureOptions: options);

        Assert.AreEqual(Banner + "152 3 matches found\r\nsurl \"hello\"\r\nsurl \"help\"\r\nsurl \"world\"\r\n.\r\n" + Ok, reply);
    }

    [TestMethod]
    public async Task ServeAsync_MatchWithListingsAndLinksOff_LeavesOutASymbolicLink()
    {
        var fileSystem = StandardFileSystem().AddSymbolicLink(Path.Join(Root, "helium"), Path.Join(Root, "hello"));

        var reply = await ServeTextAsync("MATCH ! prefix hel\r\n", fileSystem, new ContentExposureOptions());

        Assert.AreEqual(Banner + "152 2 matches found\r\nsurl \"hello\"\r\nsurl \"help\"\r\n.\r\n" + Ok, reply);
    }

    [TestMethod]
    public async Task ServeAsync_UnderDefaultExposureOptions_ServesTheDatabase()
    {
        var reply = await ServeTextAsync("DEFINE ! hello\r\nMATCH ! prefix hel\r\n", exposureOptions: new ContentExposureOptions());

        Assert.AreEqual(
            Banner
                + "150 1 definitions retrieved\r\n151 \"hello\" " + DatabaseLine + "A greeting.\r\n.\r\n" + Ok
                + "152 2 matches found\r\nsurl \"hello\"\r\nsurl \"help\"\r\n.\r\n" + Ok,
            reply);
    }

    [TestMethod]
    public async Task ServeAsync_MatchWithListingsOffAndLinksOn_ListsASymbolicLinkToAFile()
    {
        var fileSystem = StandardFileSystem().AddSymbolicLink(Path.Join(Root, "helium"), Path.Join(Root, "hello"));

        var reply = await ServeTextAsync("MATCH ! prefix hel\r\n", fileSystem, new ContentExposureOptions { FollowSymbolicLinks = true });

        Assert.AreEqual(Banner + "152 3 matches found\r\nsurl \"helium\"\r\nsurl \"hello\"\r\nsurl \"help\"\r\n.\r\n" + Ok, reply);
    }

    [TestMethod]
    [DataRow("MATCH ! exact hel")]
    [DataRow("MATCH ! prefix x")]
    [DataRow("MATCH ! prefix .hid")]
    [DataRow("MATCH ! prefix su")]
    public async Task ServeAsync_MatchOfNothing_Answers552(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + NoMatch, reply);
    }

    [TestMethod]
    [DataRow("SHOW DB")]
    [DataRow("show databases")]
    public async Task ServeAsync_ShowDb_ListsTheOneDatabase(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + "110 1 databases present\r\n" + DatabaseLine + ".\r\n" + Ok, reply);
    }

    [TestMethod]
    [DataRow("SHOW STRAT")]
    [DataRow("Show Strategies")]
    public async Task ServeAsync_ShowStrat_ListsExactAndPrefix(string line)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(
            Banner + "111 2 strategies available\r\nexact \"Match headwords exactly\"\r\nprefix \"Match prefixes\"\r\n.\r\n" + Ok,
            reply);
    }

    [TestMethod]
    public async Task ServeAsync_ShowInfo_DescribesTheDatabase()
    {
        var reply = await ServeTextAsync("SHOW INFO surl\r\n");

        Assert.AreEqual(
            Banner + "112 database information follows\r\n"
                + "The files directly in the directory surl serves.\r\n"
                + "Each file's name is a headword, and its contents are its definition.\r\n.\r\n" + Ok,
            reply);
    }

    [TestMethod]
    public async Task ServeAsync_ShowServer_NamesSurlWithNoVersion()
    {
        var reply = await ServeTextAsync("SHOW SERVER\r\n");

        Assert.AreEqual(Banner + "114 server information follows\r\nsurl DICT server\r\n.\r\n" + Ok, reply);
    }

    [TestMethod]
    public async Task ServeAsync_Help_ListsTheCommands()
    {
        var reply = await ServeTextAsync("HELP\r\n");

        Assert.StartsWith(Banner + "113 help text follows\r\nCLIENT info ", reply);
        Assert.EndsWith("QUIT                         -- terminate connection\r\n.\r\n" + Ok, reply);
    }

    [TestMethod]
    [DataRow("STATUS", "210 status ok\r\n")]
    [DataRow("CLIENT libcurl 8.21.0", Ok)]
    [DataRow("AUTH user secret", "502 command not implemented\r\n")]
    [DataRow("SASLAUTH PLAIN", "502 command not implemented\r\n")]
    [DataRow("SASLRESP abc", "502 command not implemented\r\n")]
    public async Task ServeAsync_OneLineCommand_AnswersItsStatus(string line, string expected)
    {
        var reply = await ServeTextAsync(line + "\r\n");

        Assert.AreEqual(Banner + expected, reply);
    }

    [TestMethod]
    public async Task ServeAsync_OptionMime_PutsMimeHeadersBeforeEveryText()
    {
        var reply = await ServeTextAsync("OPTION mime\r\nDEFINE ! hello\r\nSHOW DB\r\n");

        Assert.AreEqual(
            Banner + Ok
                + "150 1 definitions retrieved\r\n151 \"hello\" " + DatabaseLine + MimeHeader + "A greeting.\r\n.\r\n" + Ok
                + "110 1 databases present\r\n" + MimeHeader + DatabaseLine + ".\r\n" + Ok,
            reply);
    }

    [TestMethod]
    public async Task ServeAsync_Quit_ClosesWithoutReadingFurtherCommands()
    {
        var reply = await ServeTextAsync("quit\r\nDEFINE ! hello\r\n");

        Assert.AreEqual(Banner + Bye, reply);
    }

    [TestMethod]
    public async Task ServeAsync_LineEndingInLfOnly_IsAnswered()
    {
        var reply = await ServeTextAsync("SHOW SERVER\nQUIT\n");

        Assert.AreEqual(Banner + "114 server information follows\r\nsurl DICT server\r\n.\r\n" + Ok + Bye, reply);
    }

    [TestMethod]
    public async Task ServeAsync_LineOverTheLimit_Answers500AndClosesWithoutReadingOn()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var log = new RecordingExchangeLog();

        var (connection, _) = await ServeAsync(
            [Encoding.ASCII.GetBytes("DEFINE ! hello\r\nDEFINE ! toolong\r\nQUIT\r\n")],
            limits: limits,
            log: log);

        Assert.AreEqual(Banner + Definition("hello", "A greeting.\r\n") + "500 line too long\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("A command line was longer than 16 bytes; answered 500 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedMidCommand_SendsNoReplyAndNotesIt()
    {
        var log = new RecordingExchangeLog();

        var (connection, _) = await ServeAsync([Encoding.ASCII.GetBytes("DEFINE ! hel")], log: log);

        Assert.AreEqual(Banner, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("The client closed the connection part way through a command line.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedBetweenCommands_EndsQuietly()
    {
        var log = new RecordingExchangeLog();

        var (connection, _) = await ServeAsync([Encoding.ASCII.GetBytes("STATUS\r\n")], log: log);

        Assert.AreEqual(Banner + "210 status ok\r\n", Utf8(connection.WrittenBytes));
        Assert.IsEmpty(log.Notes);
    }

    private static string Definition(string quotedWord, string text) =>
        $"150 1 definitions retrieved\r\n151 \"{quotedWord}\" " + DatabaseLine + text + ".\r\n" + Ok;

    private static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static InMemoryContentFileSystem StandardFileSystem() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "sub"))
        .AddDirectory(Path.Join(Root, "sunday"))
        .AddFile(Path.Join(Root, "hello"), "A greeting.\n"u8.ToArray(), FileTime)
        .AddFile(Path.Join(Root, "help"), "Assistance.\n"u8.ToArray(), FileTime)
        .AddFile(Path.Join(Root, "world"), "The earth.\n"u8.ToArray(), FileTime)
        .AddFile(Path.Join(Root, ".hidden"), "Secret.\n"u8.ToArray(), FileTime);

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken = default) => new(
        1,
        new ListenUrl("dict", "127.0.0.1", 18628).WithBoundPort(18628),
        new IPEndPoint(IPAddress.Loopback, 18628),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken);

    private async Task<string> ServeTextAsync(
        string request,
        InMemoryContentFileSystem? fileSystem = null,
        ContentExposureOptions? exposureOptions = null)
    {
        var (connection, _) = await ServeAsync([Encoding.UTF8.GetBytes(request)], fileSystem, exposureOptions: exposureOptions);

        return Utf8(connection.WrittenBytes);
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks,
        InMemoryContentFileSystem? fileSystem = null,
        ExchangeLimits? limits = null,
        RecordingExchangeLog? log = null,
        ContentExposureOptions? exposureOptions = null)
    {
        var contentStore = new ContentStore(
            Root,
            fileSystem ?? StandardFileSystem(),
            exposureOptions ?? ContentExposureOptions.ServeEverythingInsideTheRoot);
        var server = new DictProtocolServer(contentStore);
        var connection = new InMemoryConnection(chunks);
        log ??= new RecordingExchangeLog();
        var context = Context(log, TestContext.CancellationToken) with { Limits = limits ?? ExchangeLimits.Default };

        await server.ServeAsync(connection, context);
        await connection.DisposeAsync();

        return (connection, log);
    }
}
