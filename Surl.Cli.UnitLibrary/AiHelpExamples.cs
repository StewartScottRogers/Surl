using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// ADR-0046 decision 7's examples, in its order: each <c>surl</c> command line with every line it
/// writes, its exit code and the upstream curl 8.21.0 command line that reaches it.
/// </summary>
public static class AiHelpExamples
{
    /// <summary>The <see cref="AiHelpExample.Topic"/> of the overview's own example.</summary>
    public const string OverviewTopic = "overview";

    private const string HttpListenUrl = "http://127.0.0.1:0/";
    private const string ListeningOnHttp = "Listening on http://127.0.0.1:<port>/";

    private static readonly AiHelpExample[] Table =
    [
        Serving(OverviewTopic, "Serve HTTP on an ephemeral port", [HttpListenUrl], [ListeningOnHttp], [], ["curl http://127.0.0.1:<port>/"]),
        Serving(
            "listen-urls",
            "Serve two listen URLs",
            [HttpListenUrl, "tftp://127.0.0.1:0/"],
            [ListeningOnHttp, "Listening on tftp://127.0.0.1:<port>/"],
            [],
            []),
        Refused("listen-urls", "A scheme this build does not serve", ["rtsp://127.0.0.1:0/"], ["surl: (1) Protocol \"rtsp\" not supported"], SurlExitCode.UnsupportedProtocol),
        Refused(
            "surl",
            "No listen URL",
            [],
            ["surl: " + CommandLineParser.NoUrlSpecified, "surl: " + CommandLineFailure.TryHelpLine],
            SurlExitCode.FailedInit),
        Serving("content", "Serve in memory, with listings", ["--list-directories", HttpListenUrl], [ListeningOnHttp], [], ["curl http://127.0.0.1:<port>/"]),
        Serving("content", "Serve a data directory", ["--directory", "<path>", HttpListenUrl], [ListeningOnHttp], [], ["curl http://127.0.0.1:<port>/<file>"])
            with { Precondition = AiHelpExamplePrecondition.DataDirectoryExists },
        Refused(
            "content",
            "A data directory another surl holds",
            ["--directory", "<path>", HttpListenUrl],
            ["surl: (124) Directory <path> is in use by another surl process"],
            SurlExitCode.DataDirectoryInUse)
            with { Precondition = AiHelpExamplePrecondition.DataDirectoryHeldByAnotherSurl },
        Serving("auth", "Require a login", ["-u", "alice:secret", HttpListenUrl], [ListeningOnHttp], [], ["curl --digest -u alice:secret http://127.0.0.1:<port>/"]),
        Serving(
            "auth",
            "Read Kerberos service keys for Negotiate",
            ["--auth", "negotiate", "--keytab", "http.keytab", "--user-file", "users.txt", "http://0.0.0.0:8080/"],
            ["Listening on http://0.0.0.0:<port>/"],
            ["surl: warning: --auth: accepted methods are negotiate"],
            [])
            with { Precondition = AiHelpExamplePrecondition.KeytabAndUserFileExist },
        Serving(
            "testing",
            "For a test: accept a password over plain HTTP",
            ["--allow-plaintext-auth", "-u", "alice:secret", HttpListenUrl],
            [ListeningOnHttp],
            ["surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections"],
            ["curl -u alice:secret http://127.0.0.1:<port>/"]),
        Serving(
            "tls",
            "For a test: serve HTTPS with a throwaway certificate",
            ["--self-signed", "https://127.0.0.1:0/"],
            ["Listening on https://127.0.0.1:<port>/"],
            ["surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)"],
            ["curl -k https://127.0.0.1:<port>/"]),
        Refused(
            "tls",
            "A secure listen URL with no certificate",
            ["https://127.0.0.1:0/"],
            ["surl: (58) https://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one"],
            SurlExitCode.CertificateProblem),
        Serving("logging", "Silent mode writes nothing", ["-s", HttpListenUrl], [], [], []),
        Refused(
            "limits",
            "An argument the option refuses",
            ["--max-time", "abc", HttpListenUrl],
            ["surl: option --max-time: " + OptionArgumentReader.NotANumber, "surl: " + CommandLineFailure.TryHelpLine],
            SurlExitCode.FailedInit),
        Serving("dict", "Serve DICT", ["dict://127.0.0.1:0/"], ["Listening on dict://127.0.0.1:<port>/"], [], ["curl dict://127.0.0.1:<port>/d:surl"]),
        Serving(
            "ftp",
            "For a test: serve FTP without accounts",
            ["--allow-anonymous", "ftp://127.0.0.1:0/"],
            ["Listening on ftp://127.0.0.1:<port>/"],
            ["surl: warning: --allow-anonymous: every request and login is accepted without checking credentials"],
            ["curl ftp://127.0.0.1:<port>/example.txt"]),
        Serving("gopher", "Serve Gopher", ["gopher://127.0.0.1:0/"], ["Listening on gopher://127.0.0.1:<port>/"], [], ["curl gopher://127.0.0.1:<port>/"]),
        Serving("http", "Serve HTTP", [HttpListenUrl], [ListeningOnHttp], [], ["curl -I http://127.0.0.1:<port>/"]),
        Serving(
            "imap",
            "For a test: read mail without accounts",
            ["--allow-anonymous", "imap://127.0.0.1:0/"],
            ["Listening on imap://127.0.0.1:<port>/"],
            ["surl: warning: --allow-anonymous: every request and login is accepted without checking credentials"],
            ["curl imap://127.0.0.1:<port>/"]),
        Serving(
            "mqtt",
            "For a test: serve MQTT without accounts",
            ["--allow-anonymous", "mqtt://127.0.0.1:0/"],
            ["Listening on mqtt://127.0.0.1:<port>/"],
            ["surl: warning: --allow-anonymous: every request and login is accepted without checking credentials"],
            ["curl mqtt://127.0.0.1:<port>/example"]),
        Serving(
            "pop3",
            "For a test: retrieve mail without accounts",
            ["--allow-anonymous", "pop3://127.0.0.1:0/"],
            ["Listening on pop3://127.0.0.1:<port>/"],
            ["surl: warning: --allow-anonymous: every request and login is accepted without checking credentials"],
            ["curl pop3://127.0.0.1:<port>/"]),
        Serving(
            "smtp",
            "For a test: receive mail without accounts",
            ["--allow-anonymous", "smtp://127.0.0.1:0/"],
            ["Listening on smtp://127.0.0.1:<port>/"],
            ["surl: warning: --allow-anonymous: every request and login is accepted without checking credentials"],
            ["curl --mail-from a@example.com --mail-rcpt b@example.com -T mail.txt smtp://127.0.0.1:<port>/example.com"]),
        Refused(
            "ssh",
            "An SFTP listen URL with no host key",
            ["sftp://127.0.0.1:0/"],
            ["surl: (2) sftp://127.0.0.1:0/ needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one"],
            SurlExitCode.FailedInit),
        Serving("telnet", "Serve TELNET", ["telnet://127.0.0.1:0/"], ["Listening on telnet://127.0.0.1:<port>/"], [], ["curl telnet://127.0.0.1:<port>/"]),
        Serving("tftp", "Serve TFTP", ["tftp://127.0.0.1:0/"], ["Listening on tftp://127.0.0.1:<port>/"], [], ["curl tftp://127.0.0.1:<port>/example.txt"]),
        Serving("websocket", "Serve WebSocket", ["ws://127.0.0.1:0/"], ["Listening on ws://127.0.0.1:<port>/"], [], ["curl ws://127.0.0.1:<port>/example.txt"]),
        Serving("websocket", "Echo WebSocket messages", ["--ws-echo", "ws://127.0.0.1:0/"], ["Listening on ws://127.0.0.1:<port>/"], [], []),
    ];

    /// <summary>Every example, in ADR-0046 decision 7's order.</summary>
    public static IReadOnlyList<AiHelpExample> All => Table;

    private static AiHelpExample Serving(
        string topic, string title, string[] arguments, string[] output, string[] error, string[] curlCommandLines) =>
        new(topic, title, AiHelpExamplePrecondition.None, arguments, output, error, SurlExitCode.Ok, ServesUntilStopped: true, curlCommandLines);

    private static AiHelpExample Refused(string topic, string title, string[] arguments, string[] error, SurlExitCode exitCode) =>
        new(topic, title, AiHelpExamplePrecondition.None, arguments, [], error, exitCode, ServesUntilStopped: false, []);
}
