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
        " ftp       FTP and FTPS protocol",
        " gopher    GOPHER and GOPHERS protocol",
        " http      HTTP and HTTPS protocol",
        " limits    Connection, time and size limits",
        " logging   Log levels, tracing and the log file",
        " mqtt      MQTT and MQTTS protocol",
        " security  Options that widen what a peer may do",
        " smtp      SMTP and SMTPS protocol",
        " ssh       SSH protocol",
        " surl      The command line tool itself",
        " telnet    TELNET protocol",
        " testing   Loosening options for tests (warned)",
        " tftp      TFTP protocol",
        " tls       TLS certificates and versions",
    ];

    // ADR-0034 decision 5's paragraphs, as --help testing and each option's page lay them out.

    private static readonly string[] AllowAnonymousExplanationLines =
    [
        "        Accepts every request and every login without checking credentials:",
        "        HTTP serves every request as anonymous and sends no challenge, MQTT",
        "        answers every well-formed CONNECT with CONNACK 0 whatever credentials",
        "        it carries, SMTP takes mail with no login, FTP logs every USER and PASS",
        "        in, and SSH logs every client in, with any credential or none. A test",
        "        uses it to fetch or publish without setting up accounts. It is not the",
        "        default because anyone who can reach a listener then gets everything",
        "        surl serves, and can publish and subscribe over MQTT and send mail,",
        "        with no login at all. surl warns on every start while it is on, from",
        "        the info log level up.",
    ];

    private static readonly string[] AllowPlaintextAuthExplanationLines =
    [
        "        Accepts passwords and tokens sent over an unencrypted connection: HTTP",
        "        Basic and Bearer over http:// and an MQTT password over mqtt:// are",
        "        checked instead of refused unchecked (403 Forbidden, CONNACK 5), Basic",
        "        and Bearer are offered in a 401 over http://, SMTP offers PLAIN and",
        "        LOGIN over smtp:// without STARTTLS, and an FTP password over ftp://",
        "        without AUTH TLS is checked instead of refused (530). A test uses it to",
        "        log in without a certificate. It is not the default because anyone who",
        "        can watch the network reads the password as it is sent. surl warns on",
        "        every start while it is on, from the info log level up.",
    ];

    private static readonly string[] AuthExplanationLines =
    [
        "        Sets the authentication methods surl accepts and offers, a",
        "        comma-separated list in any case. For HTTP: negotiate, ntlm, digest,",
        "        basic, bearer and aws-sigv4. For SMTP, IMAP and POP3 logins, each SASL",
        "        mechanism by its name in lower case, as curl's login option AUTH=<mech>",
        "        names it: ntlm, digest-md5, cram-md5, plain, login, oauthbearer,",
        "        xoauth2 and external, and apop for POP3's APOP; of those three",
        "        protocols this build serves only SMTP yet. external logs in as the TLS",
        "        client certificate --cacert verifies, so it is offered only on a",
        "        connection that sent one. gssapi is read, but a start that gives it is",
        "        refused as needing --keytab without one and as not available in this",
        "        build with one (exit code 2). surl refuses a word outside the list as",
        "        an option badly used (exit code 2). A test uses it to offer one method",
        "        alone, such as --auth digest for curl's --digest. ntlm and negotiate",
        "        are not in the default because an NTLM response is built on MD4 and",
        "        HMAC-MD5 of the password and is open to relay and offline cracking, and",
        "        Negotiate carries NTLM; digest-md5 is not because RFC 6331 made it",
        "        Historic and curl picks it over every other mechanism; apop is not",
        "        because its MD5 construction leaks password characters to anyone who",
        "        can choose the timestamp it signs. surl warns on every start while",
        "        --auth is given, from the info log level up, naming the methods it",
        "        accepts.",
    ];

    private static readonly string[] SelfSignedExplanationLines =
    [
        "        Serves a throwaway self-signed certificate, made at start, for a listen",
        "        URL of a scheme that starts with TLS (such as https) when no --cert is",
        "        given; without it, and without --cert, such a URL is refused at start.",
        "        A test uses it to serve a secure scheme without a certificate file;",
        "        curl then needs -k. It is not the default because no client can verify",
        "        the certificate, so a client cannot tell surl from anyone else on the",
        "        path. It cannot be used with --cert. surl warns when it makes the",
        "        certificate, from the info log level up.",
    ];

    private static readonly string[] ThrowawayHostKeyExplanationLines =
    [
        "        Makes a throwaway RSA 3072-bit SSH host key at start for an scp or sftp",
        "        listen URL when no --hostkey is given; without it, and without",
        "        --hostkey, such a URL is refused at start. A test uses it to serve SSH",
        "        without a key file; curl then needs the key's SHA-256 hash pinned, or",
        "        -k. It is not the default because no client can know the key",
        "        beforehand, so a client cannot tell surl from anyone else on the path.",
        "        It cannot be used with --hostkey. surl warns when it makes the key,",
        "        from the info log level up, naming the hash to pin.",
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
            "     --aihelp <topic>         Markdown help for AI agents",
            "     --allow-uploads          Accept uploads into served files",
            "     --cert <file>            Server certificate file",
            "     --directory <directory>  Data directory, else in memory",
            " -h, --help <subject>         Get help for commands",
            "     --key <file>             Private key for --cert",
            "     --list-directories       Answer directory listings",
            " -s, --silent                 Silent mode",
            " -u, --user <user:password>   Add an account (repeatable)",
            "     --user-file <file>       Read accounts from a file",
            " -v, --verbose                Log every exchange event",
            " -V, --version                Show version number and quit",
            "",
            "This is not the full help; this menu is split into categories.",
            "Use \"--help category\" to get an overview of all categories, which are:",
            "auth, content, dict, ftp, gopher, http, limits, logging, mqtt, security, smtp,",
            "ssh, surl, telnet, testing, tftp, tls.",
            "Use \"--help all\" to list all options",
            "Use \"--help [option]\" to view documentation for a given option");
    }

    // --help all and --help category.

    [TestMethod]
    [DataRow("all")]
    [DataRow("ALL", DisplayName = "Any case")]
    public void Answer_All_ListsEveryOptionWithDescriptionsInColumn38(string subject)
    {
        var answer = HelpText.Answer(subject);

        AssertOutput(
            answer,
            Row(38, "    --aihelp <topic>", "Markdown help for AI agents"),
            Row(38, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(38, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(38, "    --allow-uploads", "Accept uploads into served files"),
            Row(38, "    --allow-weak-ssh-algorithms", "Offer weak SSH algorithms (warns)"),
            Row(38, "    --auth <methods>", "Authentication methods accepted"),
            Row(38, "    --authorized-keys <user:file>", "SSH public keys a user may use"),
            Row(38, "    --cacert <file>", "CA certificates for client certs"),
            Row(38, "    --cert <file>", "Server certificate file"),
            Row(38, "    --cert-type <type>", "Format of --cert: PEM, DER or P12"),
            Row(38, "    --directory <directory>", "Data directory, else in memory"),
            Row(38, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(38, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(38, "-h, --help <subject>", "Get help for commands"),
            Row(38, "    --hostcert <file>", "SSH host certificate file"),
            Row(38, "    --hostkey <file>", "SSH host private key file"),
            Row(38, "    --idle-timeout <seconds>", "Close an exchange idle this long"),
            Row(38, "    --key <file>", "Private key for --cert"),
            Row(38, "    --key-type <type>", "Format of --key: PEM or DER"),
            Row(35, "    --keytab <file>", "Read Kerberos service keys from a keytab file"),
            Row(38, "    --list-directories", "Answer directory listings"),
            Row(38, "    --log-file <file>", "Append the log to <file>"),
            Row(38, "    --log-level <level>", "Set the log level"),
            Row(38, "-M, --manual", "Display the full manual"),
            Row(38, "    --max-connections <number>", "Connections at once, all listeners"),
            Row(38, "    --max-connections-per-address <number>", "Connections at once per address"),
            Row(38, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(38, "    --max-line <bytes>", "Longest command line accepted"),
            Row(38, "    --max-message <bytes>", "Largest framed message accepted"),
            Row(38, "    --max-request-head <bytes>", "Largest HTTP or RTSP request head"),
            Row(38, "-m, --max-time <seconds>", "Longest time one exchange may take"),
            Row(38, "    --pass <phrase>", "Passphrase for --key and --hostkey"),
            Row(38, "    --self-signed", "Throwaway certificate (warns)"),
            Row(38, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(38, "-S, --show-error", "Show error even when -s is used"),
            Row(38, "-s, --silent", "Silent mode"),
            Row(38, "    --throwaway-hostkey", "Throwaway SSH host key (warns)"),
            Row(38, "    --tls-max <version>", "Highest TLS version accepted"),
            Row(38, "    --tlsv1.0", "Accept TLS 1.0 or later"),
            Row(38, "    --tlsv1.1", "Accept TLS 1.1 or later"),
            Row(38, "    --tlsv1.2", "Accept TLS 1.2 or later (default)"),
            Row(38, "    --tlsv1.3", "Accept TLS 1.3 or later"),
            Row(38, "    --trace <file>", "Write a debug trace to <file>"),
            Row(38, "    --trace-ascii <file>", "Like --trace, but without hex"),
            Row(38, "    --trace-time", "Add time stamps to log lines"),
            Row(38, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(38, "    --user-file <file>", "Read accounts from a file"),
            Row(38, "-v, --verbose", "Log every exchange event"),
            Row(38, "-V, --version", "Show version number and quit"));
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
    public void Answer_Auth_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("auth"),
            "auth: Accounts and authentication methods",
            Row(37, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(37, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(37, "    --auth <methods>", "Authentication methods accepted"),
            Row(37, "    --authorized-keys <user:file>", "SSH public keys a user may use"),
            Row(37, "    --hostcert <file>", "SSH host certificate file"),
            Row(37, "    --hostkey <file>", "SSH host private key file"),
            Row(35, "    --keytab <file>", "Read Kerberos service keys from a keytab file"),
            Row(37, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(37, "    --user-file <file>", "Read accounts from a file"));

    [TestMethod]
    public void Answer_Testing_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("testing"),
            [
                "testing: Loosening options for tests (warned)",
                Row(30, "    --allow-anonymous", "Accept any login, or none (warns)"),
                Row(30, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
                Row(30, "    --auth <methods>", "Authentication methods accepted"),
                Row(30, "    --self-signed", "Throwaway certificate (warns)"),
                Row(30, "    --throwaway-hostkey", "Throwaway SSH host key (warns)"),
                "",
                "    --allow-anonymous",
                .. AllowAnonymousExplanationLines,
                "",
                "    --allow-plaintext-auth",
                .. AllowPlaintextAuthExplanationLines,
                "",
                "    --auth <methods>",
                .. AuthExplanationLines,
                "",
                "    --self-signed",
                .. SelfSignedExplanationLines,
                "",
                "    --throwaway-hostkey",
                .. ThrowawayHostKeyExplanationLines,
                "",
            ]);

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
            Row(34, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(34, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(34, "    --auth <methods>", "Authentication methods accepted"),
            Row(34, "    --directory <directory>", "Data directory, else in memory"),
            Row(34, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(34, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(34, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(34, "    --max-request-head <bytes>", "Largest HTTP or RTSP request head"),
            Row(34, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(34, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(34, "    --user-file <file>", "Read accounts from a file"));

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
            "     --log-file <file>     Append the log to <file>",
            "     --log-level <level>   Set the log level",
            " -S, --show-error          Show error even when -s is used",
            " -s, --silent              Silent mode",
            "     --trace <file>        Write a debug trace to <file>",
            "     --trace-ascii <file>  Like --trace, but without hex",
            "     --trace-time          Add time stamps to log lines",
            " -v, --verbose             Log every exchange event");

    [TestMethod]
    [DataRow("--silent", DisplayName = "Long name")]
    [DataRow("-s", DisplayName = "Short name")]
    [DataRow("--no-silent", DisplayName = "Negated")]
    public void Answer_Silent_IsItsPage(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -s, --silent",
            "        Silent mode. Default: off.",
            "",
            "        Categories: logging.",
            "");

    [TestMethod]
    [DataRow("--show-error", DisplayName = "Long name")]
    [DataRow("-S", DisplayName = "Short name, upper case")]
    [DataRow("--no-show-error", DisplayName = "Negated")]
    public void Answer_ShowError_IsItsPage(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -S, --show-error",
            "        Show error even when -s is used. Default: off.",
            "",
            "        Categories: logging.",
            "");

    [TestMethod]
    [DataRow("--log-level", "    --log-level <level>", "Set the log level. Default: info.")]
    [DataRow("--trace", "    --trace <file>", "Write a debug trace to <file>. Default: none.")]
    [DataRow("--trace-ascii", "    --trace-ascii <file>", "Like --trace, but without hex. Default: none.")]
    [DataRow("--trace-time", "    --trace-time", "Add time stamps to log lines. Default: off.")]
    [DataRow("--no-trace-time", "    --trace-time", "Add time stamps to log lines. Default: off.")]
    [DataRow("--log-file", "    --log-file <file>", "Append the log to <file>. Default: stderr.")]
    public void Answer_LoggingOption_IsItsPage(string subject, string leftSide, string description) =>
        AssertOutput(
            HelpText.Answer(subject),
            leftSide,
            "        " + description,
            "",
            "        Categories: logging.",
            "");

    [TestMethod]
    [DataRow("--user", DisplayName = "Long name")]
    [DataRow("-u", DisplayName = "Short name")]
    public void Answer_User_IsItsPage(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -u, --user <user:password>",
            "        Add an account (repeatable). Default: no accounts.",
            "",
            "        Categories: auth, ftp, http, mqtt, smtp, ssh.",
            "");

    [TestMethod]
    public void Answer_UserFile_IsItsPage() =>
        AssertOutput(
            HelpText.Answer("--user-file"),
            "    --user-file <file>",
            "        Read accounts from a file. Default: none.",
            "",
            "        Categories: auth, ftp, http, mqtt, smtp, ssh.",
            "");

    [TestMethod]
    public void Answer_AllowAnonymous_IsItsPageWithItsExplanation() =>
        AssertOutput(
            HelpText.Answer("--allow-anonymous"),
            [
                "    --allow-anonymous",
                "        Accept any login, or none (warns). Default: off.",
                "",
                .. AllowAnonymousExplanationLines,
                "",
                "        Categories: auth, ftp, http, mqtt, security, smtp, ssh, testing.",
                "",
            ]);

    [TestMethod]
    [DataRow("--allow-plaintext-auth", DisplayName = "Long name")]
    [DataRow("--no-allow-plaintext-auth", DisplayName = "Negated")]
    public void Answer_AllowPlaintextAuth_IsItsPageWithItsExplanation(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            [
                "    --allow-plaintext-auth",
                "        Accept passwords in clear (warns). Default: off.",
                "",
                .. AllowPlaintextAuthExplanationLines,
                "",
                "        Categories: auth, ftp, http, mqtt, security, smtp, testing.",
                "",
            ]);

    [TestMethod]
    public void Answer_SelfSigned_IsItsPageWithItsExplanation() =>
        AssertOutput(
            HelpText.Answer("--self-signed"),
            [
                "    --self-signed",
                "        Throwaway certificate (warns). Default: off.",
                "",
                .. SelfSignedExplanationLines,
                "",
                "        Categories: security, testing, tls.",
                "",
            ]);

    [TestMethod]
    public void Answer_Auth_IsItsPageWithItsDefaultWrappedAndItsExplanation() =>
        AssertOutput(
            HelpText.Answer("--auth"),
            [
                "    --auth <methods>",
                "        Authentication methods accepted. Default: digest,cram-md5,basic,plain,",
                "        login,bearer,oauthbearer,xoauth2,external,aws-sigv4.",
                "",
                .. AuthExplanationLines,
                "",
                "        Categories: auth, http, security, smtp, testing.",
                "",
            ]);

    [TestMethod]
    [DataRow("--manual", DisplayName = "Long name")]
    [DataRow("-M", DisplayName = "Short name")]
    public void Answer_Manual_IsItsPage(string subject) =>
        AssertOutput(
            HelpText.Answer(subject),
            "    -M, --manual",
            "        Display the full manual.",
            "",
            "        Categories: surl.",
            "");

    [TestMethod]
    [DataRow("--no-user")]
    [DataRow("--no-user-file")]
    [DataRow("--no-auth")]
    [DataRow("--no-log-level")]
    [DataRow("--no-trace")]
    [DataRow("--no-trace-ascii")]
    [DataRow("--no-log-file")]
    public void Answer_NegatedOptionThatIsNotNegatable_IsTheIncorrectOptionAnswer(string subject) =>
        Assert.AreEqual(HelpText.Answer("--nosuch"), HelpText.Answer(subject));

    [TestMethod]
    public void Answer_Mqtt_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("mqtt"),
            "mqtt: MQTT and MQTTS protocol",
            Row(32, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(32, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(32, "    --max-message <bytes>", "Largest framed message accepted"),
            Row(32, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(32, "    --user-file <file>", "Read accounts from a file"));

    [TestMethod]
    public void Answer_Ftp_ListsEveryOptionTheFtpServerReads() =>
        AssertOutput(
            HelpText.Answer("ftp"),
            "ftp: FTP and FTPS protocol",
            Row(32, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(32, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(32, "    --allow-uploads", "Accept uploads into served files"),
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --list-directories", "Answer directory listings"),
            Row(32, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(32, "    --max-line <bytes>", "Longest command line accepted"),
            Row(32, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(32, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(32, "    --user-file <file>", "Read accounts from a file"));

    [TestMethod]
    public void Answer_Smtp_ListsEveryOptionTheSmtpServerReads() =>
        AssertOutput(
            HelpText.Answer("smtp"),
            "smtp: SMTP and SMTPS protocol",
            Row(32, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(32, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(32, "    --auth <methods>", "Authentication methods accepted"),
            Row(32, "    --directory <directory>", "Data directory, else in memory"),
            Row(32, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(32, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(32, "    --max-line <bytes>", "Longest command line accepted"),
            Row(32, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(32, "    --user-file <file>", "Read accounts from a file"));

    [TestMethod]
    public void Answer_Ssh_ListsEveryOptionTheSshServerReads() =>
        AssertOutput(
            HelpText.Answer("ssh"),
            "ssh: SSH protocol",
            Row(37, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(37, "    --allow-uploads", "Accept uploads into served files"),
            Row(37, "    --allow-weak-ssh-algorithms", "Offer weak SSH algorithms (warns)"),
            Row(37, "    --authorized-keys <user:file>", "SSH public keys a user may use"),
            Row(37, "    --directory <directory>", "Data directory, else in memory"),
            Row(37, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(37, "    --head-timeout <seconds>", "Time to send a request head"),
            Row(37, "    --hostcert <file>", "SSH host certificate file"),
            Row(37, "    --hostkey <file>", "SSH host private key file"),
            Row(37, "    --list-directories", "Answer directory listings"),
            Row(37, "    --max-filesize <bytes>", "Largest upload accepted"),
            Row(37, "    --max-message <bytes>", "Largest framed message accepted"),
            Row(37, "    --pass <phrase>", "Passphrase for --key and --hostkey"),
            Row(37, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(37, "    --throwaway-hostkey", "Throwaway SSH host key (warns)"),
            Row(37, "-u, --user <user:password>", "Add an account (repeatable)"),
            Row(37, "    --user-file <file>", "Read accounts from a file"));

    [TestMethod]
    public void Answer_Security_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("security"),
            "security: Options that widen what a peer may do",
            Row(35, "    --allow-anonymous", "Accept any login, or none (warns)"),
            Row(35, "    --allow-plaintext-auth", "Accept passwords in clear (warns)"),
            Row(35, "    --allow-uploads", "Accept uploads into served files"),
            Row(35, "    --allow-weak-ssh-algorithms", "Offer weak SSH algorithms (warns)"),
            Row(35, "    --auth <methods>", "Authentication methods accepted"),
            Row(35, "    --follow-symlinks", "Follow links that stay in the root"),
            Row(35, "    --list-directories", "Answer directory listings"),
            Row(35, "    --self-signed", "Throwaway certificate (warns)"),
            Row(35, "    --serve-dot-files", "Serve names that start with a dot"),
            Row(35, "    --throwaway-hostkey", "Throwaway SSH host key (warns)"),
            Row(35, "    --tlsv1.0", "Accept TLS 1.0 or later"),
            Row(35, "    --tlsv1.1", "Accept TLS 1.1 or later"));

    [TestMethod]
    public void Answer_Surl_ListsItsOptions() =>
        AssertOutput(
            HelpText.Answer("surl"),
            "surl: The command line tool itself",
            "     --aihelp <topic>  Markdown help for AI agents",
            " -h, --help <subject>  Get help for commands",
            " -M, --manual          Display the full manual",
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
            "     --pass <phrase>      Passphrase for --key and --hostkey",
            "     --self-signed        Throwaway certificate (warns)",
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
            "        Categories: dict, ftp, gopher, limits, smtp, telnet.",
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
    public void Answer_AiHelp_IsItsPageWithoutADefault() =>
        AssertOutput(
            HelpText.Answer("--aihelp"),
            "    --aihelp <topic>",
            "        Markdown help for AI agents.",
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
    public void Answer_Testing_ExplainsEveryLooseningOptionAndNamesOnlyOptionsThatExist()
    {
        var text = HelpText.Answer("testing").Output;

        foreach (var looseningOption in new[] { "--allow-anonymous", "--allow-plaintext-auth", "--auth <methods>", "--self-signed", "--throwaway-hostkey" })
        {
            StringAssert.Contains(text, NewLine + "    " + looseningOption + NewLine, looseningOption);
        }

        OptionNames.AssertEveryNamedOptionExists(text);
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
