using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class FtpCommandTests
{
    private const string NoSuchDirectory = "550 No such directory\r\n";
    private const string DirectoryChanged = "250 Directory changed\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("PWD")]
    [DataRow("XPWD")]
    [DataRow("pwd")]
    public async Task Pwd_AtLogin_NamesTheRoot(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual("257 \"/\" is the current directory\r\n", written);
    }

    [TestMethod]
    [DataRow("CWD dir\r\nCWD sub\r\n", DisplayName = "One level at a time")]
    [DataRow("CWD dir/sub\r\n", DisplayName = "Two levels at once")]
    [DataRow("XCWD /dir/sub/\r\n", DisplayName = "Absolute, with a trailing slash")]
    [DataRow("CWD dir\r\nCWD ./sub/../sub\r\n", DisplayName = "Dot segments")]
    public async Task Cwd_ToAnExistingDirectory_ChangesTheCurrentDirectory(string commands)
    {
        var written = await ServeLoggedInAsync(commands + "PWD\r\n", TestContext.CancellationToken);

        Assert.EndsWith(DirectoryChanged + "257 \"/dir/sub\" is the current directory\r\n", written);
    }

    [TestMethod]
    [DataRow("CWD nodir", DisplayName = "A missing directory")]
    [DataRow("CWD .hidden", DisplayName = "A hidden directory")]
    [DataRow("CWD /.surl", DisplayName = "The service-state folder")]
    [DataRow("CWD .SURL", DisplayName = "The service-state folder in another case")]
    [DataRow("CWD a.txt", DisplayName = "A file")]
    [DataRow("CWD ..", DisplayName = "Above the root")]
    [DataRow("CWD dir/../../dir", DisplayName = "Above the root and back")]
    [DataRow("CWD c:", DisplayName = "A name the content store refuses")]
    public async Task Cwd_ToAnythingButAnExposedDirectory_Answers550AndStays(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\nPWD\r\n", TestContext.CancellationToken);

        Assert.AreEqual(NoSuchDirectory + "257 \"/\" is the current directory\r\n", written);
    }

    [TestMethod]
    public async Task Cwd_PathNotUtf8_Answers550()
    {
        byte[] line = [.. "CWD di"u8, 0xFF, (byte)'\r', (byte)'\n'];
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(AnonymousLogin), line]);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + AnonymousLoginReplies + NoSuchDirectory, Text(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("CWD")]
    [DataRow("CWD ")]
    [DataRow("TYPE")]
    [DataRow("MODE")]
    [DataRow("STRU")]
    [DataRow("OPTS")]
    public async Task Command_WithNoArgument_Answers501(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual("501 Syntax error in arguments\r\n", written);
    }

    [TestMethod]
    [DataRow("CDUP")]
    [DataRow("XCUP")]
    public async Task Cdup_FromASubdirectory_ChangesToItsParent(string command)
    {
        var written = await ServeLoggedInAsync($"CWD dir/sub\r\n{command}\r\nPWD\r\n", TestContext.CancellationToken);

        Assert.AreEqual(DirectoryChanged + DirectoryChanged + "257 \"/dir\" is the current directory\r\n", written);
    }

    [TestMethod]
    public async Task Cdup_AtTheRoot_StaysAtTheRoot()
    {
        var written = await ServeLoggedInAsync("CDUP\r\nPWD\r\n", TestContext.CancellationToken);

        Assert.AreEqual(DirectoryChanged + "257 \"/\" is the current directory\r\n", written);
    }

    [TestMethod]
    [DataRow("TYPE I", "200 Type set to I")]
    [DataRow("TYPE L 8", "200 Type set to I")]
    [DataRow("type i", "200 Type set to I")]
    [DataRow("TYPE A", "200 Type set to A")]
    [DataRow("TYPE A N", "200 Type set to A")]
    [DataRow("TYPE E", "504 Type not supported")]
    [DataRow("TYPE A C", "504 Type not supported")]
    [DataRow("MODE S", "200 Mode set to S")]
    [DataRow("MODE s", "200 Mode set to S")]
    [DataRow("MODE B", "504 Mode not supported")]
    [DataRow("STRU F", "200 Structure set to F")]
    [DataRow("STRU R", "504 Structure not supported")]
    [DataRow("OPTS utf8 on", "200 UTF8 set to on")]
    [DataRow("OPTS MLST type;", "501 Option not understood")]
    [DataRow("ALLO 1024", "202 Not needed")]
    [DataRow("ACCT billing", "202 Not needed")]
    [DataRow("SYST", "215 UNIX Type: L8")]
    [DataRow("NOOP", "200 NOOP ok")]
    public async Task Command_AfterLogin_IsAnsweredFromTheTable(string command, string reply)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual(reply + "\r\n", written);
    }

    [TestMethod]
    [DataRow("FROB")]
    [DataRow("REIN")]
    [DataRow("SMNT /")]
    [DataRow("STOU")]
    [DataRow("STAT")]
    [DataRow("SITE CHMOD 644 a.txt")]
    [DataRow("RETR a.txt")]
    [DataRow("EPSV")]
    [DataRow("")]
    public async Task Command_Unknown_Answers502(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual("502 Command not implemented\r\n", written);
    }

    [TestMethod]
    public async Task Command_WordNotPrintableAscii_Answers502()
    {
        byte[] line = [0xC3, 0xA9, (byte)'\r', (byte)'\n'];
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(AnonymousLogin), line]);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + AnonymousLoginReplies + "502 Command not implemented\r\n", Text(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task Feat_ListsTheFeaturesThisServerHas()
    {
        var written = await ServeAsync("FEAT\r\n", TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "211-Features:\r\n TVFS\r\n UTF8\r\n211 End\r\n", written);
    }

    [TestMethod]
    public async Task Help_ListsTheCommandsAnswered()
    {
        var written = await ServeAsync("HELP\r\n", TestContext.CancellationToken);

        Assert.AreEqual(
            Greeting
            + "214-The following commands are recognized:\r\n"
            + " ACCT ALLO CDUP CWD FEAT HELP MODE NOOP OPTS PASS PWD QUIT STRU SYST TYPE USER XCUP XCWD XPWD\r\n"
            + "214 End\r\n",
            written);
    }

    [TestMethod]
    public async Task Quit_Answers221CompletesWritesAndReadsNoFurther()
    {
        var connection = new InMemoryConnection(Ascii(AnonymousLogin + "QUIT\r\nNOOP\r\n"), peerHalfClosesWhenExhausted: false);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + AnonymousLoginReplies + "221 Goodbye\r\n", Text(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task ClientClosesBetweenCommands_EndsTheExchangeWithNoNote()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(AnonymousLogin, TestContext.CancellationToken, log: log);

        Assert.AreEqual(Greeting + AnonymousLoginReplies, written);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ClientClosesMidLine_EndsTheExchangeWithANote()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync("NOO", TestContext.CancellationToken, log: log);

        Assert.AreEqual(Greeting, written);
        CollectionAssert.AreEqual(new[] { "The client closed the connection part way through a command line." }, log.Notes.ToArray());
    }

    [TestMethod]
    public void Schemes_AreFtp()
    {
        CollectionAssert.AreEqual(new[] { "ftp" }, Server().Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolServer(null!, new AnonymousAuthenticationPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolServer(StandardContentStore(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var connection = new InMemoryConnection([]);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(connection, null!));
    }
}
