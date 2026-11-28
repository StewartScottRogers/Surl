using System.Reflection;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decisions 3, 4, 5, 8 and 9: what <c>surl --aihelp [topic]</c> writes.</summary>
[TestClass]
public sealed partial class AiHelpTextTests
{
    private static readonly string NewLine = Environment.NewLine;

    // ADR-0046 decision 3's sixteen topics, smtp (BL-207), imap (BL-208), pop3 (BL-209), websocket (BL-303), smb (BL-299), rtsp (BL-317), ldap (BL-310), extended (BL-405), nats (BL-528), s3 (BL-421), control (BL-815), mail (BL-653, ADR-0101 decision 7) dns (BL-1032), amqp1 (BL-514) and messages (BL-992, ADR-0101 decision 7), in ordinal order.
    private static readonly string[] AdrTopicNames =
    [
        "amqp", "amqp1", "auth", "azure-storage", "coap", "content", "control", "dict", "dns", "exit-codes", "extended", "ftp", "gopher", "grpc", "gs", "http", "imap", "kafka", "ldap", "limits", "listen-urls",
        "logging", "mail", "mcp", "messages", "mqtt", "mqtt5", "nats", "oci", "pop3", "proxy", "redis", "rtsp", "s3", "security", "signalr", "smb", "smtp", "socketio", "socks", "ssh", "stomp", "surl", "telnet", "testing", "tftp", "tls", "websocket", "webtransport", "xmpp",
    ];

    // ADR-0046 decision 4's section headings, in order, on every topic page.
    private static readonly string[] TopicPageSections = ["## About", "## Schemes", "## Options", "## Exit codes", "## Examples"];

    private static readonly string[] TopicTableLines =
    [
        "| Topic | Covers |",
        "| --- | --- |",
        "| `amqp` | AMQP 0-9-1 and AMQPS protocol (extended) |",
        "| `amqp1` | AMQP 1.0 protocol (extended) |",
        "| `auth` | Accounts and authentication methods |",
        "| `azure-storage` | Azure Blob and Data Lake Storage protocol (extended) |",
        "| `coap` | CoAP and CoAPS protocol (extended) |",
        "| `content` | Served files and the data directory |",
        "| `control` | The loopback test control endpoint |",
        "| `dict` | DICT protocol |",
        "| `dns` | DNS protocol (extended) |",
        "| `exit-codes` | Exit codes and what to do next |",
        "| `extended` | Extended protocol servers, for schemes upstream curl does not have |",
        "| `ftp` | FTP and FTPS protocol |",
        "| `gopher` | GOPHER and GOPHERS protocol |",
        "| `grpc` | gRPC and gRPC over TLS protocol (extended) |",
        "| `gs` | Google Cloud Storage protocol (extended) |",
        "| `http` | HTTP and HTTPS protocol |",
        "| `imap` | IMAP and IMAPS protocol |",
        "| `kafka` | Kafka and Kafka over TLS protocol (extended) |",
        "| `ldap` | LDAP protocol |",
        "| `limits` | Connection, time and size limits |",
        "| `listen-urls` | Listen URLs, ports and the Listening on line |",
        "| `logging` | Log levels, tracing and the log file |",
        "| `mail` | Mail domains, mailboxes and aliases |",
        "| `mcp` | MCP over Streamable HTTP and over TLS protocol (extended) |",
        "| `messages` | Topics, queues and topic access |",
        "| `mqtt` | MQTT and MQTTS protocol |",
        "| `mqtt5` | MQTT 5.0 protocol: mqtt5, mqtt5s and mqtt5+ws (extended) |",
        "| `nats` | NATS and NATSS protocol (extended) |",
        "| `oci` | OCI registry protocol (extended) |",
        "| `pop3` | POP3 and POP3S protocol |",
        "| `proxy` | HTTP and HTTPS proxy |",
        "| `redis` | Redis and Rediss protocol (extended) |",
        "| `rtsp` | RTSP protocol |",
        "| `s3` | Amazon S3 protocol (extended) |",
        "| `security` | Options that widen what a peer may do |",
        "| `signalr` | SignalR and SignalR over TLS protocol (extended) |",
        "| `smb` | SMB and SMBS protocol |",
        "| `smtp` | SMTP and SMTPS protocol |",
        "| `socketio` | Socket.IO and Socket.IO over TLS protocol (extended) |",
        "| `socks` | SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxy protocol |",
        "| `ssh` | SSH protocol |",
        "| `stomp` | STOMP and STOMPS protocol (extended) |",
        "| `surl` | The command line tool itself |",
        "| `telnet` | TELNET protocol |",
        "| `testing` | Options for tests; loosening ones warn |",
        "| `tftp` | TFTP protocol |",
        "| `tls` | TLS certificates and versions |",
        "| `websocket` | WebSocket protocol |",
        "| `webtransport` | WebTransport over HTTP/3 protocol (extended) |",
        "| `xmpp` | XMPP and XMPPS protocol (extended) |",
    ];

    [TestMethod]
    public void Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder()
    {
        CollectionAssert.AreEqual(AdrTopicNames, AiHelpTopics.All.Select(topic => topic.Name).ToArray());
    }

    [TestMethod]
    public void Answer_EveryHelpCategory_IsATopic()
    {
        foreach (var category in HelpCategories.All)
        {
            Assert.IsTrue(AiHelpTopics.TryFind(category.Name, out var topic), category.Name);
            Assert.AreEqual(category.Description, topic.Description, category.Name);
            CollectionAssert.AreEqual(category.Schemes.ToArray(), topic.Schemes.ToArray(), category.Name);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Answer_NoTopic_IsTheOverview(string? topic)
    {
        string[] expected =
        [
            "# surl --aihelp: Overview and topic list",
            "",
            "## About",
            "",
            "surl is the server-side mate of curl: for each request upstream curl makes, surl is the server that answers it. "
                + "Where `curl [options] <url>` names what to fetch, `surl [options] <url>` names what to listen on.",
            "",
            "`surl --version` names the schemes this build serves. Each protocol has a topic below with its schemes, "
                + "their default ports, its options and an upstream curl command line that reaches it.",
            "",
            "surl serves until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
            "",
            OverviewNuGetToolParagraph,
            "",
            OverviewHostsParagraph,
            "",
            "## Command line",
            "",
            "A command line is `surl [options] <url>...`, with at least one listen URL (see the listen-urls topic).",
            "",
            "Options and listen URLs may come in any order and are read left to right; the first error ends the reading, "
                + "and nothing is served. After `--` every argument is a listen URL.",
            "",
            "A long option's argument is the next argument (`--max-time 30`) or follows an equals sign (`--max-time=30`).",
            "",
            "Short options may be bundled: `-vs` is `-v -s`. An option that takes an argument ends the bundle and takes the rest "
                + "of it as its argument, so `-vm30` is `-v -m 30`; with nothing left, it takes the next argument.",
            "",
            "A flag whose Allowed values say `--no-<name> turns it off` is turned off with `--no-<name>`, such as `--no-verbose`.",
            "",
            "An option or argument the command line refuses writes `surl: option --<name>: <reason>` to stderr, then "
                + "`surl: try 'surl --help' or 'surl --manual' for more information`, and exits 2.",
            "",
            "## Conventions",
            "",
            "Every topic page has the same five sections, in this order: About, Schemes, Options, Exit codes and Examples. "
                + "A section with nothing in it holds the one line `Nothing for this topic.`.",
            "",
            "In the option table, Option is how the option is written; Argument type and Allowed values say what its argument "
                + "may be; Default is its value when it is not given (`not applicable` when it has none); Loosens security says "
                + "`yes, for tests only` for an option that loosens a secure default and writes a warning, "
                + "`yes, widens what a peer may do` for one that exposes more, and `no` otherwise; Categories names every topic "
                + "that lists it; Description is its `surl --help` line.",
            "",
            "In the exit-code table, Code is the process exit code, Name its name in surl's source, Meaning when surl returns it, "
                + "and What to do next the step that fixes it.",
            "",
            "In an example, `<port>` stands for the port surl bound and `<path>` for a directory; substitute them. "
                + "An example that serves keeps serving until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
            "",
            "`surl --help`, `surl --manual`, `surl --version` and these pages are written to stdout at every log level, `-s` included.",
            "",
            "## Topics",
            "",
            .. TopicTableLines,
            "",
            "Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.",
            "",
            "## Examples",
            "",
            "### Serve HTTP on an ephemeral port",
            "",
            "```",
            "surl http://127.0.0.1:0/",
            "```",
            "",
            "stdout:",
            "",
            "```",
            "Listening on http://127.0.0.1:<port>/",
            "```",
            "",
            "stderr: nothing.",
            "",
            "Exit code: 0 (`Ok`), once stopped with Ctrl+C or SIGTERM.",
            "",
            "Reach it with upstream curl:",
            "",
            "```",
            "curl http://127.0.0.1:<port>/",
            "```",
        ];

        var answer = AiHelpText.Answer(topic);

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    private const string OverviewNuGetToolParagraph =
        "Each surl release after 0.1.12 is published to nuget.org as the .NET tool `Surly.Surl.Console`, which needs the .NET 10 SDK: "
        + "`dotnet tool install -g Surly.Surl.Console` installs the `surl` command, and "
        + "`dotnet dnx Surly.Surl.Console -- <options> <url>` runs surl without that install, with surl's arguments after `--`.";

    private const string OverviewHostsParagraph =
        "The same servers start in-process from .NET through the `Surly.Surl.Api` package (namespace `Surl.Api`): `SurlServer.StartAsync` takes a "
        + "`SurlServerOptions`, whose properties are these options, and its help is the package README and XML documentation. "
        + "An MCP client starts them through `surl-mcp`, the `Surl.Mcp` executable, on standard input and output: its tools, "
        + "`start_server`, `list_servers`, `stop_server` and `get_exchanges` among them, describe themselves, and it serves "
        + "these pages as the `surl://aihelp` resources.";

    [TestMethod]
    public void Answer_Stomp_SaysATopicSubscriptionGetsLiveMessagesAndNoRetainedOne()
    {
        var stomp = AiHelpText.Answer("stomp").Output;

        foreach (var text in new[] { "receives only what is sent while it is subscribed", "never delivered to a STOMP subscriber", "sends it to a `/queue/`" })
        {
            Assert.Contains(text, stomp, text);
        }
    }

    [TestMethod]
    public void Answer_RedisTopic_SaysAClearTextAuthIsAnsweredWrongpassWhateverThePassword()
    {
        var redis = AiHelpText.Answer("redis").Output;

        foreach (var text in new[] { "Over `redis://` without `--allow-plaintext-auth` every `AUTH` is answered `-WRONGPASS", "a right password included", "`rediss://` or `--allow-plaintext-auth` logs in" })
        {
            Assert.Contains(text, redis, text);
        }
    }

    [TestMethod]
    public void Answer_KafkaAndXmppTopics_SayWhichSaslMechanismsLogInByDefault()
    {
        var kafka = AiHelpText.Answer("kafka").Output;
        var xmpp = AiHelpText.Answer("xmpp").Output;

        Assert.Contains("`SCRAM-SHA-256` and `SCRAM-SHA-512` log in as an account (`--user`, `--user-file`) on `kafka`, and `PLAIN` as well on `kafkas`", kafka);
        Assert.Contains("`SCRAM-SHA-256`, `SCRAM-SHA-1` and `PLAIN` log in as an account (`--user`, `--user-file`) once the stream is under TLS", xmpp);
    }

    [TestMethod]
    public void Answer_AzureStorageTopic_SaysWhenAPathSegmentIsAnAccountAndThatListingsNeedListDirectories()
    {
        var azure = AiHelpText.Answer("azure-storage").Output;

        foreach (var text in new[] { "only when the `Host` is an IP literal or `localhost` and that segment names a configured account", "the first segment is the container", "List Containers, List Blobs and the Data Lake lists need `--list-directories`", "`403 AuthorizationPermissionMismatch`" })
        {
            Assert.Contains(text, azure, text);
        }
    }

    [TestMethod]
    public void Answer_ContentTopic_NamesTheAzureListingsAmongThoseListDirectoriesTurnsOn()
    {
        StringAssert.Contains(AiHelpText.Answer("content").Output, "the Azure Storage List Containers, List Blobs and Data Lake listings");
    }

    [TestMethod]
    public void Answer_XmppTopic_SaysAListenUrlWithoutACertificateExits58UnlessLoosened()
    {
        StringAssert.Contains(
            AiHelpText.Answer("xmpp").Output,
            "With none of them `xmpp` never offers TLS, so an `xmpp` listen URL is then refused at start and exits 58, unless `--allow-plaintext-auth` or `--allow-anonymous` is given. "
            + "Under TLS-SRP that refusal does not happen, with or without a certificate.");
    }

    [TestMethod]
    [DataRow(
        "amqp1",
        "so an `amqp1s` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).",
        "which is answered, and the connection upgraded, with `--cert`, `--self-signed` or TLS-SRP; with none of them it is answered with the SASL protocol header and the connection is closed.")]
    [DataRow(
        "ftp",
        "With `--cert`, `--self-signed` or TLS-SRP, `ftp://` offers `AUTH TLS` (`curl --ssl-reqd`), and its data connections are then TLS after `PROT P`; with none of them `AUTH` is answered 534, and `curl --ssl-reqd` exits 64.",
        "so an `ftps` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow(
        "imap",
        "With `--cert`, `--self-signed` or TLS-SRP, `imap://` offers STARTTLS (`curl --ssl-reqd`); with none of them STARTTLS is answered `BAD STARTTLS not available`.",
        "so an `imaps` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow(
        "ldap",
        "With `--cert`, `--self-signed` or TLS-SRP, `ldap://` offers StartTLS; with none of them it is refused `protocolError`.",
        "so an `ldaps` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow(
        "pop3",
        "With `--cert`, `--self-signed` or TLS-SRP, `pop3://` offers STLS (`curl --ssl-reqd`); with none of them STLS is answered `-ERR STLS not available`.",
        "so a `pop3s` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow(
        "smtp",
        "With `--cert`, `--self-signed` or TLS-SRP, `smtp://` offers STARTTLS (`curl --ssl-reqd`); with none of them STARTTLS is answered 454.",
        "so an `smtps` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow(
        "xmpp",
        "With `--cert`, `--self-signed` or TLS-SRP an `xmpp` stream offers STARTTLS, and requires it before a login unless `--allow-plaintext-auth` is given (see the tls topic).",
        "`xmpps` needs `--cert`, `--self-signed` or TLS-SRP, and offers the ALPN protocol `xmpp-client`, except under TLS-SRP, which agrees only `http/1.1`: "
        + "a client that offers ALPN without it is refused with the alert `no_application_protocol`.")]
    public void Answer_TopicOfAServerThatUpgradesToTls_SaysTlsSrpServesItsUpgradeAndTlsSchemeWithoutACertificate(
        string topic, string firstSentences, string secondSentences)
    {
        var page = AiHelpText.Answer(topic).Output;

        Assert.Contains(firstSentences, page);
        Assert.Contains(secondSentences, page);
    }

    [TestMethod]
    [DataRow("amqp", "an `amqps`")]
    [DataRow("azure-storage", "an `az` or `abfss`")]
    [DataRow("gopher", "a `gophers`")]
    [DataRow("grpc", "a `grpcs`")]
    [DataRow("gs", "a `gs`")]
    [DataRow("http", "an `https`")]
    [DataRow("kafka", "a `kafkas`")]
    [DataRow("mcp", "an `mcps`")]
    [DataRow("mqtt", "an `mqtts`")]
    [DataRow("nats", "a `natss`")]
    [DataRow("oci", "an `oci`")]
    [DataRow("redis", "a `rediss`")]
    [DataRow("s3", "an `s3`")]
    [DataRow("signalr", "a `signalrs`")]
    [DataRow("socketio", "a `socketios`")]
    [DataRow("smb", "an `smbs`")]
    [DataRow("stomp", "a `stomps`")]
    [DataRow("websocket", "a `wss`")]
    public void Answer_TopicOfATlsScheme_SaysTlsSrpServesItWithoutACertificate(string topic, string listener)
    {
        Assert.Contains($"so {listener} listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic)", AiHelpText.Answer(topic).Output);
    }

    [TestMethod]
    [DataRow("mqtt5", "`mqtt5s` needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic).")]
    [DataRow("gs", "so a `gs` listen URL needs `--cert`, `--self-signed` or TLS-SRP (see the tls topic), and with none of them it exits 58;")]
    [DataRow("proxy", "An `https` proxy needs `--cert` and `--key`, `--self-signed` for a test, or TLS-SRP, under which it agrees only `http/1.1`, never `h2` (see the tls topic).")]
    [DataRow("tls", "so a listen URL of one of them needs a certificate or TLS-SRP (below).")]
    [DataRow("tls", "Without `--cert`, `--self-signed` and TLS-SRP, such a listen URL writes `surl: (58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway one` and exits 58")]
    [DataRow("webtransport", "so a `webtransport` listen URL needs `--cert` or `--self-signed`, under TLS-SRP too (see the tls topic);")]
    [DataRow("security", "give `--cert` and `--key` for secure schemes, never `--self-signed`, unless TLS-SRP secures them (see the tls topic);")]
    [DataRow(
        "testing",
        "without it, without --cert and without TLS-SRP (--tlsauthtype SRP or --tlsuser), such a URL is refused at start. TLS-SRP secures TCP only, so a "
        + "webtransport or DNS over QUIC URL, or a coaps one with no account, needs --cert or --self-signed even then.")]
    [DataRow("dns", "TLS 1.3 only, ALPN `doq`, it needs `--cert` or `--self-signed` (else exit 58), under TLS-SRP too,")]
    [DataRow(
        "coap",
        "A `coaps` listen URL needs `--cert`, `--self-signed` or an account, else it exits 58, under TLS-SRP too: TLS-SRP does not secure DTLS, "
        + "and its `--tlsuser` and `--srp-verifiers` accounts are not pre-shared keys.")]
    public void Answer_TopicWithItsOwnCertificateSentence_SaysWhatTlsSrpChangesThere(string topic, string sentence)
    {
        Assert.Contains(sentence, AiHelpText.Answer(topic).Output);
    }

    [TestMethod]
    [DataRow(
        "http",
        "Under TLS-SRP `h2` is never agreed, only `http/1.1`: every `https` connection is answered HTTP/1.x, "
        + "and a client that offers ALPN without `http/1.1`, `h2` alone included, is refused with the alert `no_application_protocol`.")]
    [DataRow(
        "grpc",
        "Under TLS-SRP `h2` is never agreed, only `http/1.1`: a client that offers ALPN without `http/1.1`, `h2` alone included, is refused with the alert `no_application_protocol`, "
        + "and one that offers `http/1.1` or no ALPN is served when it then sends the HTTP/2 preface.")]
    [DataRow(
        "dns",
        "and it needs `--cert`, `--self-signed` or TLS-SRP (else exit 58). The handshake agrees ALPN `dot`, accepts a client that offers none and ends one that offers only other protocols "
        + "with alert `no_application_protocol`, except under TLS-SRP, which never agrees `dot`, only `http/1.1`: a client that offers no ALPN, or offers `http/1.1`, is served, "
        + "and one that offers ALPN without `http/1.1`, `dot` alone included, is refused with the alert `no_application_protocol`.")]
    [DataRow("dns", "default port 443; it needs `--cert`, `--self-signed` or TLS-SRP (else exit 58),")]
    [DataRow(
        "dns",
        "Under TLS-SRP `h2` is never agreed, only `http/1.1`: a client that offers `http/1.1` or no ALPN is answered over HTTP/1.1, "
        + "and one that offers ALPN without `http/1.1`, `h2` alone included, is refused with the alert `no_application_protocol`.")]
    [DataRow(
        "dns",
        "TLS-SRP options need a listen URL that uses TLS, and a `dns` one counts when `--dns-transport` names `tls` or `https`, so it needs no other listen URL beside it (see the tls topic). "
        + "With only `udp`, `tcp` or `quic`, the default included, it does not count: with no other listen URL that uses TLS, surl exits 2.")]
    public void Answer_TopicOfAServerThatOffersItsOwnAlpn_SaysWhatTlsSrpAgreesInstead(string topic, string sentences)
    {
        Assert.Contains(sentences, AiHelpText.Answer(topic).Output);
    }

    [TestMethod]
    public void Answer_TlsTopic_NamesEverySchemeThatStartsWithATlsHandshakeInItsFirstParagraph()
    {
        var schemes = (IReadOnlyCollection<string>)typeof(TlsSchemes)
            .GetField("ImplicitTlsSchemes", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var firstParagraph = AiHelpProse.TopicAbout["tls"][0];
        var listed = firstParagraph[..firstParagraph.IndexOf(" start with a TLS handshake", StringComparison.Ordinal)];
        var missing = schemes.Where(scheme => !listed.Contains($"`{scheme}`", StringComparison.Ordinal)).ToArray();

        Assert.IsNotEmpty(schemes);
        Assert.IsEmpty(missing, $"Missing from the tls topic: {string.Join(", ", missing)}");
        Assert.Contains(firstParagraph, AiHelpText.Answer("tls").Output);
    }

    [TestMethod]
    public void Answer_TlsTopic_SaysQuicAndDtlsListenersStillNeedACertificateUnderTlsSrp()
    {
        var tls = AiHelpText.Answer("tls").Output;

        Assert.Contains("every TLS listen URL on TCP, and every STARTTLS upgrade, serve TLS 1.2 with the unsigned SRP suites and no certificate", tls);
        Assert.Contains(
            "TLS-SRP secures TCP listeners only: a `webtransport` listen URL, a DNS over QUIC one (`--dns-transport quic`) and a `coaps` one with no `--user` or `--user-file` account "
            + "still need `--cert` or `--self-signed`, else surl exits 58, and with one they serve that certificate.",
            tls);
    }

    [TestMethod]
    public void Answer_AmqpTopic_NamesCramMd5AsOfferedByDefaultAndWhatAllowAnonymousOffers()
    {
        var amqp = AiHelpText.Answer("amqp").Output;

        foreach (var text in new[] { "offers CRAM-MD5", "PLAIN and AMQPLAIN are offered only over TLS or with `--allow-plaintext-auth`", "`--allow-anonymous` offers PLAIN, AMQPLAIN and ANONYMOUS on every transport" })
        {
            Assert.Contains(text, amqp, text);
        }
    }

    [TestMethod]
    public void Answer_GrpcTopic_PrintsTheEchoProtoLineForLine()
    {
        var grpc = AiHelpText.Answer("grpc").Output;

        foreach (var line in new[]
        {
            "syntax = \"proto3\";", "package surl.test;", "message EchoRequest {", "  string message = 1;", "  uint32 count = 2;",
            "  int32 status_code = 3;", "  string status_message = 4;", "  uint32 delay_milliseconds = 5;", "message EchoReply {",
            "  uint32 index = 2;", "service Echo {", "  rpc Echo (EchoRequest) returns (EchoReply);",
            "  rpc Expand (EchoRequest) returns (stream EchoReply);", "  rpc Collect (stream EchoRequest) returns (EchoReply);",
            "  rpc Chat (stream EchoRequest) returns (stream EchoReply);",
        })
        {
            Assert.Contains("\n" + line + "\n", grpc.Replace("\r\n", "\n"), line);
        }
    }

    [TestMethod]
    public void Answer_WebTransportTopic_SaysAConnectWithoutALoginIsRefusedUnlessAllowAnonymous()
    {
        var webTransport = AiHelpText.Answer("webtransport").Output;

        Assert.Contains("A `CONNECT` without a login is refused `401` unless `--allow-anonymous` is given", webTransport);
        Assert.DoesNotContain("as an HTTP request is", webTransport);
    }

    [TestMethod]
    public void Answer_KafkaTopic_NamesTheGroupApisAndGroupAccess()
    {
        var kafka = AiHelpText.Answer("kafka").Output;

        foreach (var name in new[] { "JoinGroup", "OffsetCommit", "CreateTopics", "InitProducerId", "`$group/<id>`", "`sub`", "`kafkas`" })
        {
            Assert.Contains(name, kafka, name);
        }
    }

    [TestMethod]
    public void Answer_NoTopic_PointsAtSurlApisStartingTypeAndSurlMcpsExecutableAndTools()
    {
        var overview = AiHelpText.Answer(null).Output;

        var about = overview[..overview.IndexOf("## Command line", StringComparison.Ordinal)];
        Assert.Contains(OverviewHostsParagraph, about);
        foreach (var name in new[] { "`Surl.Api`", "`SurlServer.StartAsync`", "`Surl.Mcp`", "`surl-mcp`", "`start_server`", "`list_servers`", "`stop_server`", "`get_exchanges`", "`surl://aihelp`" })
        {
            Assert.Contains(name, about, name);
        }
    }

    [TestMethod]
    public void Answer_EveryTopic_IsInTheOverviewAndInAll()
    {
        var overview = AiHelpText.Answer(null).Output;
        var all = AiHelpText.Answer("all").Output;

        foreach (var topic in AiHelpTopics.All)
        {
            StringAssert.Contains(overview, $"| `{topic.Name}` | {topic.Description} |" + NewLine, topic.Name);
            StringAssert.Contains(all, AiHelpText.Answer(topic.Name).Output, topic.Name);
        }
    }

    [TestMethod]
    public void Answer_All_IsTheOverviewThenEveryTopicPageEachAfterAnEmptyLine()
    {
        var expected = AiHelpTopics.All.Aggregate(
            AiHelpText.Answer(null).Output,
            (text, topic) => text + NewLine + AiHelpText.Answer(topic.Name).Output);

        var answer = AiHelpText.Answer("all");

        Assert.AreEqual(expected, answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName()
    {
        var all = AiHelpText.Answer("all").Output;

        foreach (var option in CommandLineOptions.All)
        {
            var row = $"| `{HelpText.LeftSide(option).TrimStart()}` | {option.ArgumentType.Name} | ";
            StringAssert.Contains(all, row, option.LongName);

            foreach (var category in option.Help.Categories)
            {
                StringAssert.Contains(AiHelpText.Answer(category).Output, row, $"{option.LongName} in {category}");
            }
        }
    }

    [TestMethod]
    public void Answer_Topic_ListsOnlyTheOptionsOfItsCategory()
    {
        foreach (var topic in AiHelpTopics.All)
        {
            var optionRows = OptionRows(AiHelpText.Answer(topic.Name).Output);
            var expected = CommandLineOptions.All.Count(option => option.Help.Categories.Contains(topic.Name));

            Assert.HasCount(expected, optionRows, topic.Name);
        }
    }

    [TestMethod]
    public void Answer_Content_SaysAWritableShareIsLockedAndAReadonlyOneIsNot()
    {
        var page = AiHelpText.Answer("content").Output;

        StringAssert.Contains(page, "A share without `readonly` is locked as `--directory` is: surl holds `<path>/.surl/lock` on its folder while it runs");
        StringAssert.Contains(page, "as its `--directory` or as a writable share exits 124, and a `readonly` share takes no lock.");
    }

    [TestMethod]
    public void Answer_Messages_SaysWhichTopicFiltersAreAccepted()
    {
        var page = AiHelpText.Answer("messages").Output;

        StringAssert.Contains(page, "`#` may stand at any level here, not only the last as in an MQTT `SUBSCRIBE`");
        StringAssert.Contains(page, "`shop/#/orders` is accepted; `shop/a#` exits 2.");
    }

    [TestMethod]
    public void Answer_Messages_SaysASubscriptionQueueAlsoNeedsSubOnItsFilters()
    {
        var page = AiHelpText.Answer("messages").Output;

        StringAssert.Contains(page, "A queue keeping an AMQP 1.0 durable or shared subscription (`surl-subscription-...`) needs more: a get, consume, delete or purge of it by name also needs `sub` on every topic filter it is bound to");
        StringAssert.Contains(page, "(a get or consume still needs its `$queue/<name>` grant), and once any grant is given, one bound to no filter is refused.");
    }

    [TestMethod]
    public void Answer_ExitCodes_ListsEverySurlExitCodeMember()
    {
        var page = AiHelpText.Answer("exit-codes").Output;

        foreach (var code in Enum.GetValues<SurlExitCode>())
        {
            StringAssert.Contains(page, $"| {(int)code} | `{code}` | ", code.ToString());
        }
    }

    [TestMethod]
    public void Answer_EveryTopicPage_HasTheSameSectionsInOrder()
    {
        foreach (var topic in AiHelpTopics.All)
        {
            var headings = Lines(AiHelpText.Answer(topic.Name)).Where(line => line.StartsWith("# ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal)).ToArray();

            Assert.AreEqual($"# surl --aihelp {topic.Name}: {topic.Description}", headings[0], topic.Name);
            CollectionAssert.AreEqual(TopicPageSections, headings[1..], topic.Name);
        }
    }

    [TestMethod]
    public void Answer_EveryAnswer_HasNoEscapeTabOrTrailingSpaceAndWholeTableRows()
    {
        string?[] topics = [null, "all", "nosuch", .. AdrTopicNames];

        foreach (var topic in topics)
        {
            var answer = AiHelpText.Answer(topic);
            Assert.DoesNotContain("\u001b", answer.Output, topic);
            Assert.DoesNotContain("\t", answer.Output, topic);
            Assert.EndsWith(NewLine, answer.Output, topic);

            var lines = Lines(answer);
            Assert.IsFalse(lines.Any(line => line.EndsWith(' ')), topic);
            AssertEveryTableRowHasTheCellsOfItsHeader(lines, topic);
        }
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("category")]
    [DataRow("http://127.0.0.1:1/")]
    [DataRow("--user")]
    [DataRow("-h")]
    [DataRow("--")]
    [DataRow("allx")]
    public void Answer_UnknownOrOptionLikeTopic_IsTheUnknownTopicAnswer(string topic)
    {
        string[] expected =
        [
            "# surl --aihelp: unknown topic",
            "",
            "Unknown topic provided, here is a list of all topics:",
            "",
            .. TopicTableLines,
        ];

        var answer = AiHelpText.Answer(topic);

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    [DataRow("MQTT", "mqtt")]
    [DataRow("Exit-Codes", "exit-codes")]
    [DataRow("All", "all")]
    [DataRow("ALL", "all")]
    public void Answer_Topic_MatchesInAnyCase(string given, string topic)
    {
        Assert.AreEqual(AiHelpText.Answer(topic), AiHelpText.Answer(given));
    }

    [TestMethod]
    public void Answer_Testing_ListsEachExplanationAfterTheTable()
    {
        var page = AiHelpText.Answer("testing").Output;

        foreach (var option in CommandLineOptions.All.Where(option => option.Help.Explanation is not null && option.Help.Categories.Contains("testing")))
        {
            StringAssert.Contains(page, NewLine + NewLine + $"`--{option.LongName}`: {option.Help.Explanation}" + NewLine, option.LongName);
        }
    }

    [TestMethod]
    public void Answer_Testing_SaysARefusedPasswordNeverSpendsARefuseLoginRule()
    {
        var page = AiHelpText.Answer("testing").Output;

        StringAssert.Contains(page, "A `refuse-login` rule is asked only once a login's credentials match an account, so a wrong password is refused as usual and neither fires the rule nor spends one of its `times`.");
    }

    [TestMethod]
    public void Answer_Logging_SaysAMisplacedHttp2PrefaceWithholdsEveryLaterByte()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "Data bodies are logged as received too, with three exceptions: a line sent through a SOCKS or HTTP proxy tunnel that reads as one carrying a secret has that secret withheld, an HTTP/2 connection preface can make surl withhold the rest of a connection, and surl withholds the payload of some HTTP/2 `DATA` frames, as the next paragraphs say.");
        StringAssert.Contains(page, "On a `--proxy` listen URL, and on the `http` and `https` listen URLs of a start that names `--proxy`, while a connection is read as HTTP/1.x, an HTTP/2 connection preface that does not start where a request may start (one inside an upload body, partway through a line or after a HAProxy `PROXY` line sent into a tunnel) withholds every later byte the client sends on that connection, later requests included, behind one `****`.");
        StringAssert.Contains(page, "A preface that starts where a request may start (ALPN `h2`, prior knowledge in a `CONNECT` tunnel, or after an `h2c` upgrade request) is not that case: what follows is read as HTTP/2.");
        Assert.DoesNotContain("data bodies are not secrets in clear", page);
    }

    [TestMethod]
    public void Answer_Logging_SaysWhichHttp2DataPayloadsAreWithheld()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "Where the log reads a client's bytes as HTTP/2 frames (the HTTP and HTTPS servers, the HTTP proxy, gRPC, DNS over HTTPS and HTTP/2 inside a SOCKS tunnel), it withholds the payload of every `DATA` frame on a stream that carried a `CONNECT` header block, extended `CONNECT` included, because such a stream carries another protocol's bytes, credentials included.");
        StringAssert.Contains(page, "Once surl can no longer follow a connection's header blocks, or more than 4096 of its streams have carried a `CONNECT` block, every later `DATA` payload on that connection is withheld, on every stream.");
        StringAssert.Contains(page, "A withheld `DATA` frame is logged as its 9-byte frame header, as received, then `****`, and an empty one as its header alone.");
        StringAssert.Contains(page, "Every other `DATA` payload is logged as received, one sent on a stream before that stream's `CONNECT` header block, which a client may not do, included. The event stream keeps the withheld bytes, so a test that needs them reads it.");
        Assert.DoesNotContain("any credential tunnelled in them included", page);
    }

    [TestMethod]
    public void Answer_Logging_NamesEveryApiKeyHeaderAndTheServersThatWithholdThem()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "The S3, Azure Storage, Google Cloud Storage, OCI registry, MCP, SignalR, Socket.IO, gRPC, WebTransport and DNS over HTTPS servers also withhold the API-key and session-token headers");
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            StringAssert.Contains(page, $"`{name}`");
        }

        StringAssert.Contains(page, "The HTTP proxy, in the requests it forwards and in its `CONNECT` tunnels, and the SOCKS server, in a tunnel it reads as text or HTTP/2, withhold those five headers too, and so do the `http` and `https` listen URLs of a start that names `--proxy`, over HTTP/1.x and HTTP/2.");
        StringAssert.Contains(page, "The `http` and `https` listen URLs of a start without `--proxy`, the HTTP/3 log of the HTTP and HTTPS servers with or without it, and the WebSocket and RTSP servers log those five headers as received.");
        StringAssert.Contains(page, "A header surl withholds in an HTTP/1.x request head it withholds in the trailers of a chunked request body too.");
    }

    [TestMethod]
    public void Answer_Logging_SaysBytesThatDoNotOpenWithTheHttp2PrefaceAreWithheld()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "Where bytes read as HTTP/2 do not open with the client preface (on a `grpc` listen URL, or on an `http` listen URL of a start without `--proxy` after its `101` to `Upgrade: h2c`), surl logs the bytes that match it, then one `****`, and nothing more of what that client sends on the connection.");
        Assert.DoesNotContain("frames, and logged as received", page);
        Assert.DoesNotContain("`HEADERS` frame that QUIC delivers", page);
    }

    [TestMethod]
    public void Answer_Logging_SaysBothProxiesChooseTheMqttAndLdapRedactorsAtTheFirstByteOrAfterAProxyLine()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "Both proxies, the SOCKS server and the HTTP proxy in a `CONNECT` tunnel, choose the MQTT and LDAP redactors at the tunnel's first byte, or at the byte after a first line starting `PROXY ` (several such lines in a row are each followed).");
        StringAssert.Contains(page, "When that byte is `0x10`, an MQTT `CONNECT`, surl withholds its password (MQTT 3.1.1 and 5) and every MQTT 5 Authentication Data value, in `CONNECT` and `AUTH` packets.");
        StringAssert.Contains(page, "An MQTT `CONNECT` password, MQTT 5 Authentication Data and LDAP bind credentials are withheld there as in a SOCKS tunnel, the redactor chosen at the first byte after the `CONNECT` head or at the byte after a first line starting `PROXY `.");
    }

    [TestMethod]
    public void Answer_Logging_SaysWhatIsAndIsNotWithheldInEachTunnel()
    {
        var page = AiHelpText.Answer("logging").Output;

        StringAssert.Contains(page, "surl writes as `****` the argument of a `PASS` or `ACCT` line, its command word ended by a space or a tab and with or without spaces or tabs before it, the credentials of an `Authorization` or `Proxy-Authorization` header line, the whole value of an API-key or session-token header line, the password of an IMAP `LOGIN` line, with a tag or without one and its words separated by spaces or tabs, and each SASL `AUTH` or `AUTHENTICATE` initial response and each line answering a `334` or `+` continuation.");
        StringAssert.Contains(page, "A `PASS` or `ACCT` command and a header are each read at the start of a line and after every bare carriage return, with or without spaces or tabs before the command word or the header name; from where the first secret so read begins, a command's argument or a withheld header's, the rest of the line is `****`.");
        StringAssert.Contains(page, "has each frame payload of a header block holding `authorization`, `proxy-authorization` or an API-key or session-token header written as `****`");
        StringAssert.Contains(page, "Not withheld in a SOCKS tunnel: the NATS, STOMP, AMQP, Kafka and XMPP logins, and Redis ones other than the password of an inline `AUTH <user> <password>`, `AUTH <password>` or `HELLO <protover> AUTH <user> <password>` line (the password of an `AUTH <password>` line is logged when it is, unquoted, the name of a SASL or FTP security mechanism, such as `plain` or `tls`); and everything");
        StringAssert.Contains(page, "Inside an HTTP/1.x `CONNECT` tunnel through the HTTP proxy only some of those secrets are withheld too.");
        StringAssert.Contains(page, "makes surl judge it and every later line, and write as `****` the argument of a `PASS` or `ACCT` line, the credentials of an `Authorization` or `Proxy-Authorization` header line, the whole value of an API-key or session-token header line, the password of an IMAP `LOGIN` line, and each SASL `AUTH` or `AUTHENTICATE` initial response and each line answering a `334` or `+` continuation, bytes the client sends before the proxy's `200` included.");
        StringAssert.Contains(page, "Not withheld in a `CONNECT` tunnel: the NATS, STOMP, AMQP, Kafka and XMPP logins,");
        StringAssert.Contains(page, "Commands and headers after a bare carriage return, and spaces or tabs before a command or a header name, are read as in a SOCKS tunnel.");
        StringAssert.Contains(page, "(with the same exception as in a SOCKS tunnel).");
        Assert.DoesNotContain("follows no withheld header", page);
        StringAssert.Contains(page, "A tunnel whose first byte is not a letter, `1` to `9` or a line feed");
        StringAssert.Contains(page, "A first tunnelled line of three words whose last starts `HTTP/` or `RTSP/`");
        StringAssert.Contains(page, "In tunnelled text, what follows a `PASS` or `ACCT` word ended by a bare carriage return, not by a space or a tab, can be logged.");
        Assert.DoesNotContain("command that follows a bare carriage return", page);
        StringAssert.Contains(page, "so a client that crafts a `0x10` or `0x30` (the digit `0`) after a `PROXY` line can have a later login or header logged.");
        StringAssert.Contains(page, "An MQTT or LDAP login behind a PROXY protocol version 2 header, or behind other bytes, is not withheld.");
        Assert.DoesNotContain("trailers of a chunked HTTP/1.x request body is logged", page);
        StringAssert.Contains(page, "without `--proxy`, when it does not wait for the `101`; with `--proxy`, when it does not send the exact preface first.");
        Assert.DoesNotContain("later lines end with a bare line feed", page);
        StringAssert.Contains(page, "A SASL answer sent before the origin's continuation reaches the client is judged without it and logged");
        Assert.DoesNotContain("Not withheld in a tunnel: SASL", page);
    }

    [TestMethod]
    public void Answer_Security_SaysWhichSecretsAProxyTunnelsLogWithholds()
    {
        var page = AiHelpText.Answer("security").Output;

        StringAssert.Contains(page, "Inside a SOCKS tunnel and an HTTP proxy `CONNECT` tunnel alike, only the `PASS`, `ACCT`, IMAP `LOGIN`, SASL, inline Redis `AUTH`, `Authorization`, `Proxy-Authorization`, API-key and session-token secrets of a text protocol or HTTP, an MQTT `CONNECT` password and Authentication Data and LDAP bind credentials are withheld, so treat the log of a proxy listener as holding secrets.");
    }

    [TestMethod]
    public void Answer_Testing_SaysAnHttpChosen401CarriesTheServersChallenges()
    {
        var page = AiHelpText.Answer("testing").Output;

        StringAssert.Contains(page, "An `http` or `https` `error` rule that chooses `401` (`error;scheme=http;reply=401`) sends it with the challenges the server's own `401` would offer on that connection, as `WWW-Authenticate` fields (see the auth topic).");
        StringAssert.Contains(page, "When the server offers none on the connection, the `401` goes without one and the log says so.");
        StringAssert.Contains(page, "Over HTTP/2 and HTTP/3, a `401` whose only challenges are NTLM or Negotiate is reset instead (`HTTP_1_1_REQUIRED`, `H3_VERSION_FALLBACK`), so the client does not receive it or its `body=` text, though one of the rule's `times` is spent.");
    }

    [TestMethod]
    [DataRow("allow-anonymous", "yes, for tests only")]
    [DataRow("self-signed", "yes, for tests only")]
    [DataRow("allow-uploads", "yes, widens what a peer may do")]
    [DataRow("tlsv1.0", "yes, widens what a peer may do")]
    [DataRow("verbose", "no")]
    [DataRow("max-time", "no")]
    public void Answer_Option_SaysWhetherItLoosensSecurity(string longName, string loosens)
    {
        Assert.IsTrue(CommandLineOptions.TryFindLong(longName, out var option));

        var rows = OptionRows(AiHelpText.Answer("all").Output)
            .Where(line => line.StartsWith($"| `{HelpText.LeftSide(option).TrimStart()}` |", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        Assert.HasCount(1, rows);
        var row = rows[0];

        Assert.AreEqual(loosens, Cells(row)[4].Trim());
    }

    [TestMethod]
    public void Answer_Surl_WritesNotApplicableForAnOptionWithNoDefault()
    {
        StringAssert.Contains(
            AiHelpText.Answer("surl").Output,
            "| `-V, --version` | none | not applicable | none | no | surl | Show version number and quit |" + NewLine);
    }

    [TestMethod]
    public void Row_CellWithAPipe_IsEscapedSoTheRowKeepsItsCells()
    {
        var row = AiHelpText.Row(["`none|error|info|verbose|trace`", "a"]);

        Assert.AreEqual("| `none\\|error\\|info\\|verbose\\|trace` | a |", row);
        Assert.HasCount(2, Cells(row));
    }

    [TestMethod]
    public void Answer_Telnet_IsPinned()
    {
        string[] expected =
        [
            "# surl --aihelp telnet: TELNET protocol",
            "",
            "## About",
            "",
            "The TELNET server (RFC 854, RFC 855) negotiates options with the client, reports what the client tells it about "
                + "its terminal and environment, then echoes each line back until the client sends `quit`.",
            "",
            "`curl telnet://127.0.0.1:<port>/` carries the session: what curl reads from stdin is sent, and what the server "
                + "answers is written to stdout. `curl -t <option=value>` sets curl's side of the negotiation.",
            "",
            "## Schemes",
            "",
            "| Scheme | Default port |",
            "| --- | --- |",
            "| `telnet` | 23 |",
            "",
            "## Options",
            "",
            "| Option | Argument type | Default | Allowed values | Loosens security | Categories | Description |",
            "| --- | --- | --- | --- | --- | --- | --- |",
            "| `--max-line <bytes>` | bytes | 8192 | digits with an optional decimal point and more digits, then at most one suffix "
                + "k, m, g, t or p in either case, each 1024 times the one before; at most 9223372036854775807 bytes; 0 means no limit "
                + "| no | dict, ftp, gopher, imap, limits, nats, pop3, smtp, stomp, telnet | Longest command line accepted |",
            "",
            "## Exit codes",
            "",
            "Nothing for this topic.",
            "",
            "## Examples",
            "",
            "### Serve TELNET",
            "",
            "```",
            "surl telnet://127.0.0.1:0/",
            "```",
            "",
            "stdout:",
            "",
            "```",
            "Listening on telnet://127.0.0.1:<port>/",
            "```",
            "",
            "stderr: nothing.",
            "",
            "Exit code: 0 (`Ok`), once stopped with Ctrl+C or SIGTERM.",
            "",
            "Reach it with upstream curl:",
            "",
            "```",
            "curl telnet://127.0.0.1:<port>/",
            "```",
        ];

        var answer = AiHelpText.Answer("telnet");

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_Auth_SaysWhichListenersUseTheKeytabAndUnderWhichServices()
    {
        var page = AiHelpText.Answer("auth").Output;

        StringAssert.Contains(page, "where the service is `HTTP` for http and https, `smtp`, `imap`, `pop` or `ldap`, `amqp` for the schemes `amqp`, `amqps`, `amqp1` and `amqp1s`, and `rcmd` for a `socks5` or `socks5h` listener.");
        StringAssert.Contains(page, "the SASL `GSSAPI` logins of the SMTP, IMAP, POP3, LDAP, AMQP 0-9-1 and AMQP 1.0 servers use them too, and so does the LDAP server's `GSS-SPNEGO` bind (see the ldap topic).");
        StringAssert.Contains(page, "A `socks5` or `socks5h` listener's GSS-API login (method `01`, curl's `--socks5-gssapi`) uses the keys whatever `--auth` names (see the socks topic).");
    }

    [TestMethod]
    public void Answer_Auth_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits()
    {
        var page = AiHelpText.Answer("auth").Output;

        StringAssert.Contains(page, "The AMQP servers' service is `amqp`, and a ticket is checked only when its frame fits: at most 4096 bytes on AMQP 0-9-1 before `connection.tune-ok`, and 65536 for an AMQP 1.0 SASL frame (`--max-message` when smaller, never below 512; `--max-message 0` leaves 65536), so a larger one closes the connection unchecked.");
    }

    [TestMethod]
    public void Answer_Auth_SaysAuthSetsTheSaslMechanismsOfEveryServerThatReadsItForSaslTheAmqpOnesIncluded()
    {
        var page = AiHelpText.Answer("auth").Output;
        var manual = string.Join(' ', ManualText.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        StringAssert.Contains(page, "and for SASL logins to the SMTP, IMAP, POP3, LDAP, AMQP 0-9-1, AMQP 1.0, Kafka, XMPP and MQTT 5.0 servers each mechanism by its lower-case name,");
        StringAssert.Contains(page, "(the MQTT 5.0 server runs only scram-sha-256 and scram-sha-1, and only when `--auth` names them; see the mqtt5 topic)");
        StringAssert.Contains(page, "external and aws-sigv4, and the Kafka and XMPP servers accept SCRAM too.");
        StringAssert.Contains(manual, "For SASL logins to the SMTP, IMAP, POP3, LDAP, AMQP 0-9-1, AMQP 1.0, Kafka, XMPP and MQTT 5.0 servers, each mechanism by its name in lower case,");
        StringAssert.Contains(manual, "The MQTT 5.0 server (mqtt5, mqtt5s and mqtt5+ws) runs only scram-sha-256 and scram-sha-1, as a CONNECT's Authentication Method, and only when --auth names them.");
        StringAssert.Contains(manual, "external and aws-sigv4, and the Kafka and XMPP servers accept SCRAM too.");
    }

    [TestMethod]
    public void Answer_Mqtt5_ListsAuthAndSaysWhenEnhancedAuthenticationLogsIn()
    {
        var page = AiHelpText.Answer("mqtt5").Output;

        StringAssert.Contains(page, "an Authentication Method of `SCRAM-SHA-256` or `SCRAM-SHA-1` logs in as an account (`--user`, `--user-file`) when `--auth` names `scram-sha-256` or `scram-sha-1`");
        StringAssert.Contains(page, "answered CONNACK `0x8C` Bad authentication method.");
        StringAssert.Contains(page, "--auth <methods>");
        StringAssert.Contains(HelpText.Answer("mqtt5").Output, "--auth <methods>");
    }

    [TestMethod]
    public void Answer_Auth_SaysTheUnusedKeytabWarningIsNotWrittenForASocks5Listener()
    {
        var page = AiHelpText.Answer("auth").Output;

        StringAssert.Contains(page, "When `--auth` names neither negotiate nor gssapi (its default names neither) and no listen URL, positional or from `--proxy`, is a `socks5` or `socks5h` one, surl writes `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi`.");
    }

    [TestMethod]
    public void Answer_Auth_SaysWhichAccountEachKerberosLoginNeeds()
    {
        var page = AiHelpText.Answer("auth").Output;

        StringAssert.Contains(page, "An HTTP, SASL or `GSS-SPNEGO` Kerberos login is accepted only when an account of exactly that name exists, unless `--allow-anonymous` is given.");
        StringAssert.Contains(page, "A SOCKS5 GSS-API login needs a proxy account (`-U`, `--proxy-user-file`; never `--user` or `--user-file`) of exactly that name when the proxy was started with proxy accounts, even an empty `--proxy-user-file`; with none, any ticket the keys accept is accepted; `--allow-anonymous` skips the account match.");
        StringAssert.Contains(page, "No Kerberos login uses the account's password.");
    }

    [TestMethod]
    public void Answer_Auth_SaysHowLongADirectoryUsersOciOrMcpTokenIsHonoured()
    {
        var page = AiHelpText.Answer("auth").Output;

        var userLdapParagraph = page.Split('\n').Single(line => line.Contains("`--user-ldap <ldap-url>", StringComparison.Ordinal) && line.Contains("LDAP directory", StringComparison.Ordinal));
        StringAssert.Contains(userLdapParagraph, "An OCI or MCP token issued to a directory user is honoured, within the token's own lifetime, for at most one hour after the name's last directory login that server accepted: refreshing an MCP token is not a login, so it never extends the hour, and a refused login neither ends nor renews it.");
        StringAssert.Contains(userLdapParagraph, "Disabling or deleting the user in the directory ends no token already issued.");
        StringAssert.Contains(userLdapParagraph, "A reset of the sessions ends them all, and so does a restore: on the `--control` endpoint `POST /v1/reset` resets the sessions with every other store, but with `--directory` or `--share` it is refused and ends none.");
        StringAssert.Contains(userLdapParagraph, "A name the accounts have held, on any scheme, while surl runs is refused such tokens: its token is honoured only while the accounts name it for that server's scheme, so removing the account refuses its tokens even when the directory knows the name too.");
    }

    [TestMethod]
    public void Answer_Socks_SaysTheGssApiLoginIgnoresAuthAndWhichProxyAccountItNeeds()
    {
        var page = AiHelpText.Answer("socks").Output;

        StringAssert.Contains(page, "and SOCKS5 then requires a login, RFC 1929 username and password or GSS-API (below), unless `--allow-anonymous`;");
        StringAssert.Contains(page, "the server asks for no login, except that a SOCKS5 listener with `--keytab` checks the ticket of a client that offers GSS-API.");
        StringAssert.Contains(page, "An RFC 1929 password travels in clear, so that login is refused unless `--allow-plaintext-auth` is given.");
        StringAssert.Contains(page, "the service name is `rcmd`, and the keys are used whatever `--auth` names.");
        StringAssert.Contains(page, "A GSS-API login is the ticket's client principal, such as `user@EXAMPLE.COM`. It needs a proxy account (`-U`, `--proxy-user-file`; never `--user` or `--user-file`) of exactly that name when the proxy was started with proxy accounts, even an empty `--proxy-user-file`; with none, any ticket the keys accept is accepted; `--allow-anonymous` skips the account match.");
    }

    [TestMethod]
    public void Answer_Ldap_SaysKeytabAnswersANegotiateBindWithKerberosAndNtlmWithoutIt()
    {
        var page = AiHelpText.Answer("ldap").Output;

        StringAssert.Contains(page, "With `--keytab` and `--auth negotiate`, the Windows curl's Negotiate bind to `ldap://localhost:<port>`");
        StringAssert.Contains(page, "answered with Kerberos inside `GSS-SPNEGO`");
        StringAssert.Contains(page, "the service principal `ldap/<the machine's host name>:<port>`, with the port");
        StringAssert.Contains(page, "sealed in RFC 4121 wrap tokens");
        StringAssert.Contains(page, "without `--keytab` the Negotiate bind is answered with NTLM");
    }

    [TestMethod]
    public void Answer_ExitCodes_IsPinned()
    {
        string[] expected =
        [
            "# surl --aihelp exit-codes: Exit codes and what to do next",
            "",
            "## About",
            "",
            "Every exit code surl returns, with what it means and what to do next. The Exit codes section of every other topic lists "
                + "the codes tied to that topic; this page lists them all.",
            "",
            "A failure writes a `surl:` line naming what failed to stderr: `surl: (N) <message>` for a failure found once the "
                + "command line is read, or `surl: option --<name>: <reason>` and the `try` line for a refused command line.",
            "",
            "With `-s` and without `-S`, a failure found once the command line is read writes nothing, and only the exit code "
                + "says what failed; a refused command line is always written, except that a listen URL whose scheme is not served (exit 1) counts as a failure found once the command line is read.",
            "",
            "## Schemes",
            "",
            "Nothing for this topic.",
            "",
            "## Options",
            "",
            "Nothing for this topic.",
            "",
            "## Exit codes",
            "",
            "| Code | Name | Meaning | What to do next |",
            "| --- | --- | --- | --- |",
            "| 0 | `Ok` | Help, the manual or the version was written, or surl was stopped by Ctrl+C or SIGTERM "
                + "| Nothing: surl succeeded, or stopped cleanly when asked |",
            "| 1 | `UnsupportedProtocol` | A listen URL names a scheme this build does not serve "
                + "| Run surl --version to list the schemes this build serves, and use one of them |",
            "| 2 | `FailedInit` | The command line cannot be used: an option or its argument refused, "
                + "no listen URL, a malformed --user-file, --authorized-keys or --keytab file, "
                + "a --keytab with no key surl can use, a --hostkey or --hostcert file surl cannot use, "
                + "an scp or sftp listen URL with no host key, a --cacert file that does not exist, "
                + "or a --control listen URL that is not http on loopback, or without --control-token-file "
                + "| Read the surl: line on stderr, which names what was refused, and fix it; "
                + "the option tables give each option's allowed values |",
            "| 3 | `MalformedUrl` | A listen URL is malformed "
                + "| Write the listen URL as scheme://host[:port][/], with no user name, path, query or fragment |",
            "| 6 | `CouldNotResolveHost` | A listen URL names a host that resolves to nothing "
                + "| Use an IP address such as 127.0.0.1, or a host name that resolves |",
            "| 23 | `CouldNotWriteFile` | The .surl folder or its lock file cannot be created, a log or trace file cannot be opened, "
                + "or the trace file is the --log-file file "
                + "| Make the data directory writable by the user surl runs as, or give a log or trace file that can be opened "
                + "and is not the --log-file file |",
            "| 37 | `CouldNotReadFile` | The data directory cannot be opened, or the --user-file, a --mailbox-file, an --authorized-keys, --keytab, --hostkey or --hostcert file, a message bus file or the mail store "
                + "cannot be read | Check the path exists and the user surl runs as can read it; surl creates none of them |",
            "| 45 | `BindFailed` | A listener cannot bind its address and port "
                + "| Use another port, or port 0 and read the bound port from the Listening on line, and an address this machine has |",
            "| 58 | `CertificateProblem` | A secure listen URL has no certificate and TLS-SRP does not secure it, or the --cert or --key file cannot be used "
                + "| Give --cert and --key files that load, with --pass when the key needs one, or --self-signed in a test; TLS-SRP (see the tls topic) secures a TCP listen URL with no certificate |",
            "| 77 | `CaCertificateBadFile` | The --cacert file cannot be read | Give a --cacert file that holds certificates |",
            "| 124 | `DataDirectoryInUse` | Another surl holds the data directory or the folder of a writable --share "
                + "| Stop the other surl, give another --directory or share folder, mark the share ;readonly, or serve in memory without --directory |",
            "| 125 | `InternalError` | surl failed while serving "
                + "| Report it with the command line and the stderr line: it is a defect in surl |",
            "",
            "## Examples",
            "",
            "Nothing for this topic.",
        ];

        var answer = AiHelpText.Answer("exit-codes");

        Assert.AreEqual(Text(expected), answer.Output);
        Assert.AreEqual(string.Empty, answer.Error);
    }

    [TestMethod]
    public void Answer_EveryTopicAndTheOverview_HasItsAboutText()
    {
        string?[] answers = [null, .. AdrTopicNames];

        foreach (var topic in answers)
        {
            var lines = Lines(AiHelpText.Answer(topic));
            var about = Array.IndexOf(lines, "## About");

            Assert.AreNotEqual(AiHelpText.NothingForThisTopicLine, lines[about + 2], topic);
        }

        CollectionAssert.AreEquivalent(AdrTopicNames, AiHelpProse.TopicAbout.Keys.ToArray());
    }

    [TestMethod]
    public void Answer_EveryTopicButExitCodesAndSecurity_HasAnExample()
    {
        foreach (var topic in AdrTopicNames.Except(["exit-codes", "security"]).Append(AiHelpExamples.OverviewTopic))
        {
            Assert.IsTrue(AiHelpExamples.All.Any(example => example.Topic == topic), topic);
        }

        foreach (var example in AiHelpExamples.All)
        {
            Assert.IsTrue(example.Topic == AiHelpExamples.OverviewTopic || AiHelpTopics.TryFind(example.Topic, out _), example.Title);
        }

        StringAssert.Contains(AiHelpText.Answer("security").Output, "## Examples" + NewLine + NewLine + AiHelpText.NothingForThisTopicLine + NewLine);
    }

    [TestMethod]
    public void Answer_EveryExample_IsOnItsTopicsPage()
    {
        foreach (var example in AiHelpExamples.All)
        {
            var page = AiHelpText.Answer(example.Topic == AiHelpExamples.OverviewTopic ? null : example.Topic).Output;
            var command = string.Join(' ', example.Arguments.Prepend("surl"));

            StringAssert.Contains(page, $"### {example.Title}{NewLine}", example.Title);
            StringAssert.Contains(page, $"```{NewLine}{command}{NewLine}```{NewLine}", example.Title);
            StringAssert.Contains(page, $"Exit code: {(int)example.ExitCode} (`{example.ExitCode}`)", example.Title);
            foreach (var line in example.Output.Concat(example.Error).Concat(example.CurlCommandLines))
            {
                StringAssert.Contains(page, NewLine + line + NewLine, example.Title);
            }
        }
    }

    [TestMethod]
    public void Answer_Content_WritesEachPreconditionAndEachEmptyStream()
    {
        var page = AiHelpText.Answer("content").Output;

        StringAssert.Contains(page, NewLine + "Given: `<path>` is an existing directory no other surl holds." + NewLine);
        StringAssert.Contains(page, NewLine + "Given: another surl is serving `<path>` with `--directory`." + NewLine);
        StringAssert.Contains(page, NewLine + "Given: `<path>` is an existing directory." + NewLine);
        StringAssert.Contains(page, NewLine + "stdout: nothing." + NewLine);
        StringAssert.Contains(page, NewLine + "stderr: nothing." + NewLine);
        StringAssert.Contains(page, NewLine + "Exit code: 124 (`DataDirectoryInUse`)." + NewLine);
    }

    [TestMethod]
    public void Answer_EveryPage_NamesOnlyOptionsThatExist()
    {
        // surl's SSH host-key note and --throwaway-hostkey warning name the curl options that pin
        // the key (ADR-0051, decisions 8 and 11); the SOCKS and --keytab facts name curl's SOCKS options.
        string[] curlOptionsSurlNames = ["hostpubsha256", "hostpubmd5", "socks4", "socks4a", "socks5", "socks5-gssapi", "socks5-hostname"];

        foreach (var name in NamedLongOptions().Except(curlOptionsSurlNames))
        {
            var exists = CommandLineOptions.TryFindLong(name, out _)
                || (name.StartsWith("no-", StringComparison.Ordinal) && CommandLineOptions.TryFindLong(name[3..], out var negated) && negated.Negatable);

            Assert.IsTrue(exists, "--" + name);
        }
    }

    [TestMethod]
    public void Prose_EverySurlLineQuoted_IsOneTheCodeWrites()
    {
        // Written by Surl.Cli: each template is matched against what the parser writes.
        var writtenByTheParser = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["surl: option --mail-domain: not a domain name"] = "surl: " + CommandLineParser.Parse(["--mail-domain", "a..b", "http://127.0.0.1/"]).Failure!.Message,
            ["surl: (2) no URL specified"] = "surl: " + CommandLineParser.Parse([]).Failure!.Message,
            ["surl: try 'surl --help' or 'surl --manual' for more information"] = "surl: " + CommandLineFailure.TryHelpLine,
            ["surl: option --<name>: <reason>"] = "surl: " + CommandLineParser.Parse(["--max-time", "abc"]).Failure!.Message,
            ["surl: (3) URL rejected: <reason>"] = "surl: " + CommandLineParser.Parse(["http://user@127.0.0.1/"]).Failure!.Message,
            ["surl: (1) Protocol \"<scheme>\" not supported"] = "surl: " + CommandLineParser.Parse(["nosuch://127.0.0.1/"]).Failure!.Message,
            ["surl: (37) Could not read config file <file>"] = "surl: " + CommandLineParser.Parse(["-K", "missing.cfg"]).Failure!.Message,
            ["surl: a.cfg:2: option --log-file: requires parameter"] = "surl: " + CommandLineParser.Parse(["-K", "a.cfg"], [], new FakeConfigFileSource(("a.cfg", "verbose\nlog-file\n"))).Failure!.Message,
            ["surl: (2) --control needs http, not <scheme>"] = "surl: " + CommandLineParser.Parse(["--control", "ftp://127.0.0.1:0"]).Failure!.Message,
            ["surl: (2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): <url>"] = "surl: " + CommandLineParser.Parse(["--control", "http://0.0.0.0:0"]).Failure!.Message,
            ["surl: (2) --control needs --control-token-file"] = "surl: " + CommandLineParser.Parse(["--control", "http://127.0.0.1:0", "http://127.0.0.1:0/"]).Failure!.Message,
            ["surl: option --unix-socket: <reason>"] = "surl: " + CommandLineParser.Parse(["--unix-socket", "s", "tftp://localhost/"]).Failure!.Message,
            ["surl: option --random-seed: expected a proper numerical parameter"] = "surl: " + CommandLineParser.Parse(["--random-seed", "-1", "http://127.0.0.1:0/"]).Failure!.Message,
            ["surl: (2) --control-token-file without --control"] = "surl: " + CommandLineParser.Parse(["--control-token-file", "t", "http://127.0.0.1:0/"]).Failure!.Message,
        };

        // Written by Surl.Console, checked against its source (BL-140's Notes); the examples that
        // show them are run by Surl.Console.UnitTests (ADR-0046 decision 9, BL-142).
        string[] writtenBySurlConsole =
        [
            "surl: (N) <message>",
            "surl: (N)",
            "surl: warning:",
            "surl: (124) Directory <path> is in use by another surl process",
            "surl: (37) Could not open directory <path>",
            // Written by Surl.Composition when it opens a --share folder, whose tests pin it (BL-988).
            "surl: (37) Could not open directory <path> for share <name>",
            "surl: (37) Could not read <file>: line <n>: <what>",
            // Written by Surl.Composition's LdapAccountSourceComposition, whose tests pin it (BL-673).
            "surl: (2) --user-ldap plaintext needs --allow-plaintext-auth",
            "surl: (45) Could not bind <scheme>://<address>:<port>/: <reason>",
            "surl: (6) Could not resolve host: <host>",
            "surl: (58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway one",
            // Written by Surl.Composition's DnsTransportComposition, whose tests pin it (BL-1017).
            "surl: --dns-transport tcp and tls cannot share port <n>",
            "surl: --dns-transport udp and quic cannot share port <n>",
            "surl: --dns-transport quic cannot use port 53",
            "surl: --dns-transport tcp and https cannot share port <n>",
            "surl: --dns-transport tls and https cannot share port <n>",
            "surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)",
            "surl: warning: --proxy-allow-origin: the proxy connects to every origin address, not only loopback",
            "surl: warning: --s3-plaintext: s3 is served as plain HTTP, without TLS",
            "surl: warning: --oci-plaintext: oci is served as plain HTTP, without TLS",
            "surl: warning: --az-plaintext: az is served as plain HTTP, without TLS",
            "surl: warning: --gs-plaintext: gs is served as plain HTTP, without TLS",
            "surl: warning: --proxy-auth: accepted proxy methods are basic, ntlm",
            "surl: (37) Could not read host certificate <file>",
            "surl: (2) Host certificate <file>: not an OpenSSH host certificate",
            "surl: (2) Host certificate <file>: certifies no --hostkey key",
            "surl: (2) Host certificate <file>: a <type> host certificate is already given by <file>",
            "surl: (2) --ssh-ciphers: surl does not offer the SSH cipher <name>",
            "surl: (2) --ssh-ciphers: <name> needs --allow-weak-ssh-algorithms",
            "surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered",
            "surl: (37) Could not read authorized keys <file>",
            "surl: (37) Could not read keytab <file>",
            "surl: (2) Keytab <file> is malformed at byte <offset>",
            "surl: (2) Keytab <file> holds no key surl can use",
            "surl: warning: --keytab: skipped the <enctype> key of <principal>",
            "surl: (2) --auth gssapi needs --keytab",
            "surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi",
            "surl: (2) <url> needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one",
            "surl: warning: --throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 <base64>); clients must pin it or skip the check (curl -k)",
            // Written by Surl.Composition's AccountGroupExpansion, whose tests pin each (BL-646).
            "surl: (2) option --group: member <member> names no account or group",
            "surl: (2) option --group: group <name> includes itself",
            "surl: (2) option --group: group everyone is reserved",
            "surl: (2) option --group: member @everyone is not allowed here",
            // Written by Surl.Composition when it reads a --mailbox-file, whose tests pin each (BL-653).
            "surl: (37) Could not read mailbox file <file>",
            "surl: (2) Mailbox file <file>, line <n>: <reason>",
            // Written by Surl.Composition's LoadDnsZonesAsync, whose tests pin it (BL-1032).
            "surl: (37) Could not read <file>: <reason>",
            // Written by Surl.Composition's ReadableByOthersWarning, whose tests pin it (BL-684).
            "surl: warning: <file> is readable by other users and may hold passwords",
        ];

        foreach (var (template, written) in writtenByTheParser)
        {
            Assert.MatchesRegex("^" + Regex.Replace(Regex.Escape(template), "<[a-z]+>", ".+") + "$", written, template);
        }

        var quoted = ProseParagraphs()
            .SelectMany(paragraph => QuotedSurlLine().Matches(paragraph).Select(match => match.Groups[1].Value))
            .Distinct()
            .ToArray();
        CollectionAssert.AreEquivalent(writtenByTheParser.Keys.Concat(writtenBySurlConsole).ToArray(), quoted);
    }

    /// <summary>
    /// Every <c>--name</c> in the prose and in the examples' <c>surl</c> arguments and output,
    /// leaving out the upstream curl command lines, whose options are curl's.
    /// </summary>
    private static IEnumerable<string> NamedLongOptions()
    {
        var texts = ProseParagraphs()
            .Select(paragraph => CurlCodeSpan().Replace(paragraph, string.Empty))
            .Concat(AiHelpExamples.All.SelectMany(example => example.Arguments.Concat(example.Output).Concat(example.Error)));

        return texts.SelectMany(text => LongOptionName().Matches(text).Select(match => match.Groups[1].Value)).Distinct();
    }

    private static IEnumerable<string> ProseParagraphs() =>
        AiHelpProse.OverviewAbout
            .Concat(AiHelpProse.OverviewCommandLine)
            .Concat(AiHelpProse.OverviewConventions)
            .Concat(AiHelpProse.TopicAbout.Values.SelectMany(paragraphs => paragraphs));

    /// <summary>Asserts every row of each table in <paramref name="lines"/> has as many cells as the table's header.</summary>
    private static void AssertEveryTableRowHasTheCellsOfItsHeader(string[] lines, string? topic)
    {
        var headerCells = 0;
        var previousWasRow = false;
        foreach (var line in lines)
        {
            var isRow = line.StartsWith("| ", StringComparison.Ordinal);
            if (isRow && !previousWasRow)
            {
                headerCells = Cells(line).Length;
            }

            if (isRow)
            {
                Assert.AreEqual(headerCells, Cells(line).Length, $"{topic}: {line}");
                Assert.EndsWith(" |", line, $"{topic}: {line}");
            }

            previousWasRow = isRow;
        }
    }

    /// <summary>The cells of a table row, split at every <c>|</c> not written <c>\|</c>.</summary>
    private static string[] Cells(string row) => UnescapedPipe().Split(row)[1..^1];

    /// <summary>The option table's rows, header and <c>| --- |</c> row left out.</summary>
    private static string[] OptionRows(string page) =>
        [.. page.Split(NewLine).Where(line => line.StartsWith("| `-", StringComparison.Ordinal))];

    private static string[] Lines(HelpAnswer answer) => answer.Output.Split(NewLine)[..^1];

    private static string Text(IEnumerable<string> lines) => string.Concat(lines.Select(line => line + NewLine));

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex UnescapedPipe();

    // --name, not --<placeholder> nor the --no- of --no-<name>.
    [GeneratedRegex(@"(?<![\w-])--([a-z0-9][a-z0-9.-]*[a-z0-9])(?![\w<-])")]
    private static partial Regex LongOptionName();

    [GeneratedRegex("`curl [^`]*`")]
    private static partial Regex CurlCodeSpan();

    [GeneratedRegex("`(surl: [^`]*)`")]
    private static partial Regex QuotedSurlLine();
}
