namespace Surl.Cli;

[TestClass]
public sealed class HelpTextTests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly string[] CategoryListLines =
    [
        " auth      Accounts and authentication methods",
        " content   Served files and the data directory",
        " dict      DICT protocol",
        " gopher    GOPHER and GOPHERS protocol",
        " http      HTTP and HTTPS protocol",
        " limits    Connection, time and size limits",
        " logging   Log levels, tracing and the log file",
        " mqtt      MQTT and MQTTS protocol",
        " security  Options that widen what a peer may do",
        " surl      The command line tool itself",
        " telnet    TELNET protocol",
        " testing   Loosening options for tests (warned)",
        " tftp      TFTP protocol",
        " tls       TLS certificates and versions",
    ];

    // --help with no subject.

    [TestMethod]
    [DataRow(null, DisplayName = "No subject")]
    [DataRow("", DisplayName = "Empty subject")]
    public void Answer_NoSubject_IsTheShortListAndThePointerLines(string? subject)
    {
        var answer = HelpText.Answer(subject);

        AssertOutput(
            answer,
            "Usage: surl [options...] <url>...",
            "     --allow-uploads          Accept uploads into served files",
            "     --cert <file>            Server certificate file",
            "     --directory <directory>  Data directory, else in memory",
            " -h, --help <subject>         Get help for commands",
            "     --key <file>             Private key for --cert",
            "     --list-directories       Answer directory listings",
            " -v, --verbose                Log every exchange event",
            " -V, --version                Show version number and quit",
            "",
            "This is not the full help; this menu is split into categories.",
            "Use \"--help category\" to get an overview of all categories, which are:",
            "auth, content, dict, gopher, http, limits, logging, mqtt, security, surl,",
            "telnet, testing, tftp, tls.",
            "Use \"--help all\" to list all options",
            "Use \"--help [option]\" to view documentation for a given option");
    }

    // --help all and --help category.

    [TestMethod]
    [DataRow("all")]
    [DataRow("ALL", DisplayName = "Any case")]
    public void Answer_All_ListsEveryOptionWithDescriptionsInColumn46(string subject)
    {
        var answer = HelpText.Answer(subject);

        AssertOutput(
            answer,
            "     --allow-uploads                         Accept uploads into served files",
            "     --cacert <file>                         CA certificates for client certs",
            "     --cert <file>                           Server certificate file",
            "     --cert-type <type>                      Format of --cert: PEM, DER or P12",
            "     --directory <directory>                 Data directory, else in memory",
            "     --follow-symlinks                       Follow links that stay in the root",
            "     --head-timeout <seconds>                Time to send a request head",
            " -h, --help <subject>                        Get help for commands",
            "     --idle-timeout <seconds>                Close an exchange idle this long",
            "     --key <file>                            Private key for --cert",
            "     --key-type <type>                       Format of --key: PEM or DER",
            "     --list-directories                      Answer directory listings",
            "     --max-connections <number>              Connections at once, all listeners",
            "     --max-connections-per-address <number>  Connections at once per address",
            "     --max-filesize <bytes>                  Largest upload accepted",
            "     --max-line <bytes>                      Longest command line accepted",
            "     --max-message <bytes>                   Largest framed message accepted",
            "     --max-request-head <bytes>              Largest HTTP or RTSP request head",
            " -m, --max-time <seconds>                    Longest time one exchange may take",
            "     --pass <phrase>                         Passphrase for the private key",
            "     --serve-dot-files                       Serve names that start with a dot",
            "     --tls-max <version>                     Highest TLS version accepted",
            "     --tlsv1.0                               Accept TLS 1.0 or later",
            "     --tlsv1.1                               Accept TLS 1.1 or later",
            "     --tlsv1.2                               Accept TLS 1.2 or later (default)",
            "     --tlsv1.3                               Accept TLS 1.3 or later",
            " -v, --verbose                               Log every exchange event",
            " -V, --version                               Show version number and quit");
    }

    [TestMethod]
    [DataRow("category")]
    [DataRow("Category", DisplayName = "Any case")]
    public void Answer_Category_ListsEveryCategory(string subject) =>
        AssertOutput(HelpText.Answer(subject), CategoryListLines);

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("silent", DisplayName = "An option name without its dashes")]
    [DataRow("important", DisplayName = "A category curl keeps hidden")]
    [DataRow("http://127.0.0.1:1/", DisplayName = "A listen URL")]
    public void Answer_UnknownSubject_IsTheUnknownCategoryLineThenTheCategoryList(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            ["Unknown category provided, here is a list of all categories:", "", .. CategoryListLines]);

    // --help <category>.

    [TestMethod]
    [DataRow("auth", "auth: Accounts and authentication methods")]
    [DataRow("testing", "testing: Loosening options for tests (warned)")]
    public void Answer_CategoryWithNoOptionYet_IsItsHeadingAlone(string subject, string heading) =>
        AssertOutput(HelpText.Answer(subject), heading);

    [TestMethod]
    public void Answer_Content_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("content"),
            "content: Served files and the data directory",
            Row(31, "    --allow-uploads", "Accept uploads into served files"),
            Row(31, "    --directory <directory>", "Data directory, else in memory"),
            Row(31, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(31, "    --list-directories", "Answer directory listings"),
            Row(31, "    --serve-dot-files", "Serve names that start with a dot"));

    [TestMethod]
    public void Answer_Dict_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("dict"),
            "dict: DICT protocol",
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --max-line <bytes>", "Longest command line accepted"),
            Row(32, "    --serve-dot-files", "Serve names that start with a dot"));

    [TestMethod]
    public void Answer_Gopher_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("gopher"),
            "gopher: GOPHER and GOPHERS protocol",
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --list-directories", "Answer directory listings"),
            Row(32, "    --max-line <bytes>", "Longest command line accepted"),
            Row(32, "    --serve-dot-files", "Serve names that start with a dot"));

    [TestMethod]
    public void Answer_Http_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("http"),
            "http: HTTP and HTTPS protocol",
            Row(34, "    --directory <directory>", "Data directory, else in memory"),
            Row(34, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(34, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(34, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(34, "    --max-request-head <bytes>", "Largest HTTP or RTSP request head"),
            Row(34, "    --serve-dot-files", "Serve names that start with a dot"));

    [TestMethod]
    public void Answer_Limits_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("limits"),
            "limits: Connection, time and size limits",
            Row(46, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(46, "    --idle-timeout <seconds>", "Close an exchange idle this long"),
            Row(46, "    --max-connections <number>", "Connections at once, all listeners"),
            Row(46, "    --max-connections-per-address <number>", "Connections at once per address"),
            Row(46, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(46, "    --max-line <bytes>", "Longest command line accepted"),
            Row(46, "    --max-message <bytes>", "Largest framed message accepted"),
            Row(46, "    --max-request-head <bytes>", "Largest HTTP or RTSP request head"),
            Row(46, "-m, --max-time <seconds>", "Longest time one exchange may take"));

    [TestMethod]
    public void Answer_Logging_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("logging"),
            "logging: Log levels, tracing and the log file",
            " -v, --verbose  Log every exchange event");

    [TestMethod]
    public void Answer_Mqtt_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("mqtt"),
            "mqtt: MQTT and MQTTS protocol",
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(32, "    --max-message <bytes>", "Largest framed message accepted"));

    [TestMethod]
    public void Answer_Security_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("security"),
            "security: Options that widen what a peer may do",
            Row(26, "    --allow-uploads", "Accept uploads into served files"),
            Row(26, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(26, "    --list-directories", "Answer directory listings"),
            Row(26, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(26, "    --tlsv1.0", "Accept TLS 1.0 or later"),
            Row(26, "    --tlsv1.1", "Accept TLS 1.1 or later"));

    [TestMethod]
    public void Answer_Surl_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("surl"),
            "surl: The command line tool itself",
            " -h, --help <subject>  Get help for commands",
            " -V, --version         Show version number and quit");

    [TestMethod]
    public void Answer_Telnet_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("telnet"),
            "telnet: TELNET protocol",
            "     --max-line <bytes>  Longest command line accepted");

    [TestMethod]
    public void Answer_Tftp_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("tftp"),
            "tftp: TFTP protocol",
            Row(31, "    --allow-uploads", "Accept uploads into served files"),
            Row(31, "    --directory <directory>", "Data directory, else in memory"),
            Row(31, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(31, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(31, "    --serve-dot-files", "Serve names that start with a dot"));

    [TestMethod]
    [DataRow("tls")]
    [DataRow("TLS", DisplayName = "Any case")]
    public void Answer_Tls_ListsItsOptions(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "tls: TLS certificates and versions",
            "     --cacert <file>      CA certificates for client certs",
            "     --cert <file>        Server certificate file",
            "     --cert-type <type>   Format of --cert: PEM, DER or P12",
            "     --key <file>         Private key for --cert",
            "     --key-type <type>    Format of --key: PEM or DER",
            "     --pass <phrase>      Passphrase for the private key",
            "     --tls-max <version>  Highest TLS version accepted",
            "     --tlsv1.0            Accept TLS 1.0 or later",
            "     --tlsv1.1            Accept TLS 1.1 or later",
            "     --tlsv1.2            Accept TLS 1.2 or later (default)",
            "     --tlsv1.3            Accept TLS 1.3 or later");

    // --help <option>.

    [TestMethod]
    public void Answer_LongOption_IsItsPageWithItsDefaultAndCategories() =>
        AssertOutput(
            HelpText.Answer("--max-line"),
            "    --max-line <bytes>",
            "        Longest command line accepted. Default: 8192.",
            "",
            "        Categories: dict, gopher, limits, telnet.",
            "");

    [TestMethod]
    [DataRow("-h", DisplayName = "Short name")]
    [DataRow("--help", DisplayName = "Long name")]
    public void Answer_OptionWithNoDefault_IsItsPageWithoutADefault(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -h, --help <subject>",
            "        Get help for commands.",
            "",
            "        Categories: surl.",
            "");

    [TestMethod]
    [DataRow("--no-verbose", DisplayName = "Negated")]
    [DataRow("-v", DisplayName = "Short name")]
    public void Answer_NegatedOrShortOption_IsThatOptionsPage(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -v, --verbose",
            "        Log every exchange event. Default: off.",
            "",
            "        Categories: logging.",
            "");

    [TestMethod]
    public void Answer_Key_IsItsPageWithItsLongDefault() =>
        AssertOutput(
            HelpText.Answer("--key"),
            "    --key <file>",
            "        Private key for --cert. Default: the key in the --cert file.",
            "",
            "        Categories: tls.",
            "");

    [TestMethod]
    [DataRow("--nosuch")]
    [DataRow("--")]
    [DataRow("-")]
    [DataRow("-sv", DisplayName = "A bundle")]
    [DataRow("-x", DisplayName = "An unknown short name")]
    [DataRow("--max-time=x", DisplayName = "With a value")]
    [DataRow("--no-tlsv1.2", DisplayName = "Negating an option that cannot be negated")]
    [DataRow("--no-nosuch", DisplayName = "Negating an unknown option")]
    [DataRow("--HELP", DisplayName = "Long names match in their own case only")]
    public void Answer_OptionSubjectNamingNoOption_WritesTheIncorrectOptionLineToStderrOnly(string subject)
    {
        var answer = HelpText.Answer(subject);

        Assert.AreEqual(string.Empty, answer.Output);
        Assert.AreEqual("surl: Incorrect option name to show help for, see surl -h" + NewLine, answer.Error);
    }

    // Every option and every line.

    [TestMethod]
    public void Answer_EveryOption_AppearsInAllAndInAtLeastOneCategory()
    {
        var allLines = Lines(HelpText.Answer("all"));
        var categoryPages = HelpCategories.All.Select(category => Lines(HelpText.Answer(category.Name))).ToArray();

        Assert.HasCount(CommandLineOptions.All.Count, allLines);
        foreach (var option in CommandLineOptions.All)
        {
            var marker = $"--{option.LongName}";
            Assert.IsTrue(allLines.Any(line => ContainsOption(line, marker)), option.LongName);
            Assert.IsTrue(categoryPages.Any(page => page.Skip(1).Any(line => ContainsOption(line, marker))), option.LongName);
            Assert.IsTrue(option.Help.Categories.All(name => HelpCategories.TryFind(name, out _)), option.LongName);
        }
    }

    [TestMethod]
    public void Answer_EveryPage_HasNoTrailingSpaceAndNoLinePast79Columns()
    {
        string?[] subjects =
        [
            null, "all", "category", "nosuch",
            .. HelpCategories.All.Select(category => category.Name),
            .. CommandLineOptions.All.Select(option => "--" + option.LongName),
        ];

        foreach (var subject in subjects)
        {
            foreach (var line in Lines(HelpText.Answer(subject)))
            {
                Assert.AreEqual(line.TrimEnd(), line, subject);
                Assert.IsLessThanOrEqualTo(79, line.Length, subject);
            }
        }
    }

    private static bool ContainsOption(string line, string marker) =>
        line.Contains(marker + " ", StringComparison.Ordinal);

    private static string[] Lines(HelpAnswer answer) =>
        answer.Output.Split(NewLine)[..^1];

    /// <summary>An option line whose description starts in <paramref name="column"/> (1-based).</summary>
    private static string Row(int column, string leftSide, string description) =>
        " " + leftSide.PadRight(column - 4) + "  " + description;

    private static void AssertOutput(HelpAnswer answer, params string[] lines)
    {
        Assert.AreEqual(string.Concat(lines.Select(line => line + NewLine)), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }
}
