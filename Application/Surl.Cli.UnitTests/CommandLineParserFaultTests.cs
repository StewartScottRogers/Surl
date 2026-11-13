using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary><c>--fault</c> read into <see cref="SurlCommandLine.Faults"/> (ADR-0124 decision 4, BL-785).</summary>
[TestClass]
public sealed class CommandLineParserFaultTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoFault_HasNoFaults() =>
        Assert.IsEmpty(Served(Url).Faults);

    [TestMethod]
    public void Parse_TwoFaults_AreKeptInOrderWithEveryKey()
    {
        var faults = Served(
            "--fault", "delay;listener=http://127.0.0.1:8080/;account=alice;path=/slow;command=GET;stage=command;ms=500;times=3",
            Url,
            "--fault=ERROR;Scheme=smtp;account=bob;path=a%40b.example;command=RCPT;reply=451 4.3.0 try later;times=1").Faults;

        Assert.HasCount(2, faults);
        Assert.AreEqual(
            new ConfiguredFault(FaultKind.Delay)
            {
                Listener = "http://127.0.0.1:8080/",
                Account = "alice",
                Path = "/slow",
                Command = "GET",
                Stage = FaultStage.Command,
                DelayMilliseconds = 500,
                Times = 3,
            },
            faults[0]);
        Assert.AreEqual(
            new ConfiguredFault(FaultKind.Error)
            {
                Scheme = "smtp",
                Account = "bob",
                Path = "a@b.example",
                Command = "RCPT",
                Reply = "451 4.3.0 try later",
                Times = 1,
            },
            faults[1]);
    }

    [TestMethod]
    public void Parse_TheAdrsExamples_AreEachRead()
    {
        var faults = Served(
            "--fault", "drop;listener=smtp://127.0.0.1:2525;times=1",
            "--fault", "refuse-login;account=alice",
            "--fault", "delay;ms=500;stage=command;path=/slow",
            "--fault", "error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later",
            "--fault", "out-of-space;path=/uploads",
            Url).Faults;

        CollectionAssert.AreEqual(
            new[]
            {
                new ConfiguredFault(FaultKind.Drop) { Listener = "smtp://127.0.0.1:2525", Times = 1 },
                new ConfiguredFault(FaultKind.RefuseLogin) { Account = "alice" },
                new ConfiguredFault(FaultKind.Delay) { DelayMilliseconds = 500, Stage = FaultStage.Command, Path = "/slow" },
                new ConfiguredFault(FaultKind.Error) { Scheme = "smtp", Command = "RCPT", Reply = "451 4.3.0 try later" },
                new ConfiguredFault(FaultKind.OutOfSpace) { Path = "/uploads" },
            },
            faults.ToArray());
    }

    [TestMethod]
    public void Parse_DropAfterBytesAtAccept_IsRead() =>
        Assert.AreEqual(
            new ConfiguredFault(FaultKind.Drop) { AfterBytes = int.MaxValue, Stage = FaultStage.Accept, Scheme = "s3" },
            Served("--fault", "drop;after-bytes=2147483647;stage=ACCEPT;scheme=S3", Url).Faults.Single());

    [TestMethod]
    public void Parse_DropAtCommand_TakesACommandScope() =>
        Assert.AreEqual(
            new ConfiguredFault(FaultKind.Drop) { Stage = FaultStage.Command, Command = "RETR", Path = "/f" },
            Served("--fault", "drop;stage=command;command=RETR;path=/f", Url).Faults.Single());

    [TestMethod]
    [DataRow("delay;ms=0")]
    [DataRow("delay;ms=600000")]
    [DataRow("refuse-login;times=1000000")]
    [DataRow("drop;after-bytes=0")]
    public void Parse_NumberAtItsBound_IsServed(string item) =>
        Assert.HasCount(1, Served("--fault", item, Url).Faults);

    [TestMethod]
    [DataRow("http", "503")]
    [DataRow("https", "404 Not Found")]
    [DataRow("rtsp", "599 x")]
    [DataRow("ws", "1000")]
    [DataRow("wss", "4999 going")]
    [DataRow("ws", "1014")]
    [DataRow("ws", "3000")]
    [DataRow("ws", "4999 bye")]
    [DataRow("ws", "1003 x")]
    [DataRow("ws", "1007 x")]
    [DataRow("ftp", "421 bye")]
    [DataRow("ftps", "550 no")]
    [DataRow("smtp", "400 x")]
    [DataRow("smtps", "554 5.0.0 no")]
    [DataRow("imap", "NO try later")]
    [DataRow("imaps", "bad nope")]
    [DataRow("pop3", "try later")]
    [DataRow("pop3s", "x")]
    [DataRow("tftp", "0 x")]
    [DataRow("tftp", "7 x")]
    [DataRow("sftp", "2 no such file")]
    [DataRow("sftp", "8 x")]
    [DataRow("scp", "no")]
    [DataRow("smb", "C0000022")]
    [DataRow("smbs", "ffffffff")]
    [DataRow("mqtt", "128")]
    [DataRow("mqtts", "255")]
    [DataRow("ldap", "1 x")]
    [DataRow("ldaps", "80 other")]
    [DataRow("dict", "552 no match")]
    [DataRow("gopher", "not found")]
    [DataRow("gophers", "x")]
    [DataRow("telnet", "bye")]
    [DataRow("coap", "4.00")]
    [DataRow("coaps", "5.05 proxy down")]
    [DataRow("coap", "4.31 x")]
    [DataRow("mcp", "-32603 internal error")]
    [DataRow("mcps", "7 x")]
    [DataRow("signalr", "try later")]
    [DataRow("signalrs", "x")]
    [DataRow("s3", "503 SlowDown")]
    [DataRow("az", "503 ServerBusy")]
    [DataRow("abfs", "400 InvalidUri")]
    [DataRow("abfss", "599 x")]
    [DataRow("gs", "503 backendError")]
    [DataRow("amqp", "404 NOT_FOUND")]
    [DataRow("amqps", "320 CONNECTION_FORCED")]
    [DataRow("amqp1", "amqp:not-found no such node")]
    [DataRow("amqp1s", "amqp:internal-error boom")]
    [DataRow("kafka", "1")]
    [DataRow("kafkas", "127")]
    [DataRow("mqtt5", "128")]
    [DataRow("mqtt5s", "153")]
    [DataRow("mqtt5+ws", "255")]
    [DataRow("nats", "Authorization Violation")]
    [DataRow("natss", "Permissions Violation")]
    [DataRow("dns", "1")]
    [DataRow("dns", "5")]
    [DataRow("socks4", "91")]
    [DataRow("socks4a", "93")]
    [DataRow("socks5", "1")]
    [DataRow("socks5h", "8")]
    [DataRow("stomp", "Access refused")]
    [DataRow("stomps", "boom: it broke")]
    [DataRow("xmpp", "service-unavailable")]
    [DataRow("xmpps", "recipient-unavailable")]
    [DataRow("redis", "ERR boom")]
    [DataRow("rediss", "LOADING Redis is loading")]
    public void Parse_ErrorReplyFittingItsScheme_IsServed(string scheme, string reply) =>
        Assert.AreEqual(reply, Served("--fault", $"error;scheme={scheme};reply={reply}", Url).Faults.Single().Reply);

    [TestMethod]
    public void Parse_ErrorScopedToAListener_ChecksTheListenersScheme() =>
        Assert.AreEqual(
            "imap://127.0.0.1:0",
            Served("--fault", "error;listener=imap://127.0.0.1:0;reply=NO busy", Url).Faults.Single().Listener);

    [TestMethod]
    [DataRow("nosuch", "option --fault: unknown kind nosuch", DisplayName = "Unknown kind")]
    [DataRow("delay;ms=1;nosuch=1", "option --fault: unknown key nosuch", DisplayName = "Unknown key")]
    [DataRow("drop;readonly", "option --fault: unknown flag readonly", DisplayName = "Unknown flag")]
    [DataRow("refuse-login;ms=5", "option --fault: key ms does not apply to refuse-login", DisplayName = "Key not of the kind")]
    [DataRow("out-of-space;command=STOR", "option --fault: key command does not apply to out-of-space", DisplayName = "Command on out-of-space")]
    [DataRow("error;scheme=http;reply=503;stage=command", "option --fault: key stage does not apply to error", DisplayName = "Stage on error")]
    [DataRow("delay;reply=x;ms=1", "option --fault: key reply does not apply to delay", DisplayName = "Reply on delay")]
    [DataRow("drop;ms=1", "option --fault: key ms does not apply to drop", DisplayName = "Ms on drop")]
    [DataRow("delay", "option --fault: kind delay needs key ms", DisplayName = "Delay without ms")]
    [DataRow("error;scheme=http", "option --fault: kind error needs key reply", DisplayName = "Error without reply")]
    [DataRow("delay;ms=600001", "option --fault: key ms must be a whole number from 0 to 600000", DisplayName = "Ms too large")]
    [DataRow("delay;ms=-1", "option --fault: key ms must be a whole number from 0 to 600000", DisplayName = "Ms negative")]
    [DataRow("delay;ms=1.5", "option --fault: key ms must be a whole number from 0 to 600000", DisplayName = "Ms not whole")]
    [DataRow("delay;ms= 5", "option --fault: key ms must be a whole number from 0 to 600000", DisplayName = "Ms with a space")]
    [DataRow("drop;after-bytes=2147483648", "option --fault: key after-bytes must be a whole number from 0 to 2147483647", DisplayName = "After-bytes too large")]
    [DataRow("drop;times=0", "option --fault: key times must be a whole number from 1 to 1000000", DisplayName = "Times zero")]
    [DataRow("drop;times=1000001", "option --fault: key times must be a whole number from 1 to 1000000", DisplayName = "Times too large")]
    [DataRow("drop;times=99999999999999999999", "option --fault: key times must be a whole number from 1 to 1000000", DisplayName = "Times overflows")]
    [DataRow("drop;stage=login", "option --fault: key stage must be accept or command", DisplayName = "Stage login")]
    [DataRow("drop;scheme=rtmp", "option --fault: unknown scheme rtmp", DisplayName = "Unknown scheme")]
    [DataRow("drop;listener=http://127.0.0.1:99999/", "option --fault: key listener is not a listen URL", DisplayName = "Bad listener")]
    [DataRow("drop;listener=rtmp://127.0.0.1:0", "option --fault: key listener is not a listen URL", DisplayName = "Listener of an unknown scheme")]
    [DataRow("refuse-login;listener=http://127.0.0.1:0;scheme=http", "option --fault: key listener cannot be given with key scheme", DisplayName = "Listener with scheme")]
    [DataRow("drop;account=alice", "option --fault: key account cannot be given at stage accept", DisplayName = "Account on a drop at accept")]
    [DataRow("delay;ms=1;path=/slow", "option --fault: key path cannot be given at stage accept", DisplayName = "Path on a delay at accept")]
    [DataRow("delay;ms=1;stage=accept;command=GET", "option --fault: key command cannot be given at stage accept", DisplayName = "Command at a named accept")]
    [DataRow("drop;stage=command;after-bytes=5", "option --fault: key after-bytes cannot be given with stage=command", DisplayName = "After-bytes at command")]
    [DataRow("error;reply=503", "option --fault: kind error needs key scheme or key listener", DisplayName = "Error without scheme")]
    [DataRow("error;scheme=grpc;reply=503 x", "option --fault: scheme grpc has no error shape", DisplayName = "Error on an extended scheme")]
    [DataRow("error;listener=grpc://127.0.0.1:0;reply=503 x", "option --fault: scheme grpc has no error shape", DisplayName = "Error on an extended listener")]
    [DataRow("error;scheme=socks4;reply=90", "option --fault: key reply does not fit the socks4 error shape", DisplayName = "SOCKS4 granted code")]
    [DataRow("error;scheme=socks4a;reply=94", "option --fault: key reply does not fit the socks4a error shape", DisplayName = "SOCKS4a past 93")]
    [DataRow("error;scheme=socks5;reply=0", "option --fault: key reply does not fit the socks5 error shape", DisplayName = "SOCKS5 succeeded")]
    [DataRow("error;scheme=xmpp;reply=teapot", "option --fault: key reply does not fit the xmpp error shape", DisplayName = "XMPP not a condition")]
    [DataRow("error;scheme=xmpps;reply=Service-Unavailable", "option --fault: key reply does not fit the xmpps error shape", DisplayName = "XMPPS wrong case")]
    [DataRow("error;scheme=socks5h;reply=9", "option --fault: key reply does not fit the socks5h error shape", DisplayName = "SOCKS5h past 8")]
    [DataRow("error;scheme=socks5;reply=4 host down", "option --fault: key reply does not fit the socks5 error shape", DisplayName = "SOCKS5 with text")]
    [DataRow("error;scheme=s3;reply=200 OK", "option --fault: key reply does not fit the s3 error shape", DisplayName = "S3 success")]
    [DataRow("error;scheme=s3;reply=503", "option --fault: key reply does not fit the s3 error shape", DisplayName = "S3 without an error code")]
    [DataRow("error;scheme=az;reply=600 x", "option --fault: key reply does not fit the az error shape", DisplayName = "Azure past 599")]
    [DataRow("error;scheme=abfss;reply=399 x", "option --fault: key reply does not fit the abfss error shape", DisplayName = "ABFSS below 400")]
    [DataRow("error;scheme=gs;reply=SlowDown", "option --fault: key reply does not fit the gs error shape", DisplayName = "GCS without a status")]
    [DataRow("error;scheme=amqp;reply=999 nope", "option --fault: key reply does not fit the amqp error shape", DisplayName = "AMQP code off the list")]
    [DataRow("error;scheme=amqp;reply=404", "option --fault: key reply does not fit the amqp error shape", DisplayName = "AMQP no text")]
    [DataRow("error;scheme=amqps;reply=404 ", "option --fault: key reply does not fit the amqps error shape", DisplayName = "AMQPS empty text")]
    [DataRow("error;scheme=amqp;reply=nope text", "option --fault: key reply does not fit the amqp error shape", DisplayName = "AMQP no code")]
    [DataRow("error;scheme=amqp;reply=404 éééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééé", "option --fault: key reply does not fit the amqp error shape", DisplayName = "AMQP text past 255 bytes")]
    [DataRow("error;scheme=amqp1;reply=amqp:not-found", "option --fault: key reply does not fit the amqp1 error shape", DisplayName = "AMQP 1.0 no description")]
    [DataRow("error;scheme=amqp1;reply=amqp:not-found ", "option --fault: key reply does not fit the amqp1 error shape", DisplayName = "AMQP 1.0 empty description")]
    [DataRow("error;scheme=amqp1;reply= no symbol", "option --fault: key reply does not fit the amqp1 error shape", DisplayName = "AMQP 1.0 no condition")]
    [DataRow("error;scheme=amqp1s;reply=amqp:é bad", "option --fault: key reply does not fit the amqp1s error shape", DisplayName = "AMQP 1.0 non-printable condition")]
    [DataRow("error;scheme=kafka;reply=0", "option --fault: key reply does not fit the kafka error shape", DisplayName = "Kafka none")]
    [DataRow("error;scheme=kafkas;reply=128", "option --fault: key reply does not fit the kafkas error shape", DisplayName = "Kafka past 127")]
    [DataRow("error;scheme=kafka;reply=OFFSET_OUT_OF_RANGE", "option --fault: key reply does not fit the kafka error shape", DisplayName = "Kafka name")]
    [DataRow("error;scheme=mqtt5;reply=127", "option --fault: key reply does not fit the mqtt5 error shape", DisplayName = "MQTT5 127")]
    [DataRow("error;scheme=mqtt5+ws;reply=135 no", "option --fault: key reply does not fit the mqtt5+ws error shape", DisplayName = "MQTT5 with text")]
    [DataRow("error;scheme=nats;reply=it's bad", "option --fault: key reply does not fit the nats error shape", DisplayName = "NATS apostrophe")]
    [DataRow("error;scheme=dns;reply=0", "option --fault: key reply does not fit the dns error shape", DisplayName = "DNS NOERROR")]
    [DataRow("error;scheme=dns;reply=6", "option --fault: key reply does not fit the dns error shape", DisplayName = "DNS past 5")]
    [DataRow("error;scheme=dns;reply=12", "option --fault: key reply does not fit the dns error shape", DisplayName = "DNS two digits")]
    [DataRow("error;scheme=redis;reply=ERR%0D%0Aboom", "option --fault: a control character", DisplayName = "Redis CR LF")]
    [DataRow("error;scheme=rediss;reply=ERR%0Aboom", "option --fault: a control character", DisplayName = "Rediss LF")]
    [DataRow("error;scheme=http;reply=200 OK", "option --fault: key reply does not fit the http error shape", DisplayName = "HTTP success")]
    [DataRow("error;scheme=http;reply=503 ", "option --fault: key reply does not fit the http error shape", DisplayName = "HTTP empty reason")]
    [DataRow("error;scheme=http;reply=five", "option --fault: key reply does not fit the http error shape", DisplayName = "HTTP no code")]
    [DataRow("error;scheme=ws;reply=1006", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket 1006")]
    [DataRow("error;scheme=ws;reply=999", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket too small")]
    [DataRow("error;scheme=ws;reply=1004", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket reserved 1004")]
    [DataRow("error;scheme=ws;reply=1005", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket 1005")]
    [DataRow("error;scheme=wss;reply=1015", "option --fault: key reply does not fit the wss error shape", DisplayName = "WebSocket 1015")]
    [DataRow("error;scheme=ws;reply=1016", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket reserved 1016")]
    [DataRow("error;scheme=ws;reply=2999 x", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket reserved 2999")]
    [DataRow("error;scheme=ws;reply=5000", "option --fault: key reply does not fit the ws error shape", DisplayName = "WebSocket past 4999")]
    [DataRow("error;scheme=ftp;reply=451", "option --fault: key reply does not fit the ftp error shape", DisplayName = "FTP without text")]
    [DataRow("error;scheme=smtp;reply=250 ok", "option --fault: key reply does not fit the smtp error shape", DisplayName = "SMTP success")]
    [DataRow("error;scheme=imap;reply=OK fine", "option --fault: key reply does not fit the imap error shape", DisplayName = "IMAP OK")]
    [DataRow("error;scheme=imap;reply=NO", "option --fault: key reply does not fit the imap error shape", DisplayName = "IMAP without text")]
    [DataRow("error;scheme=imap;reply=NO ", "option --fault: key reply does not fit the imap error shape", DisplayName = "IMAP empty text")]
    [DataRow("error;scheme=tftp;reply=8 x", "option --fault: key reply does not fit the tftp error shape", DisplayName = "TFTP 8")]
    [DataRow("error;scheme=sftp;reply=1 eof", "option --fault: key reply does not fit the sftp error shape", DisplayName = "SFTP 1")]
    [DataRow("error;scheme=smb;reply=00000000", "option --fault: key reply does not fit the smb error shape", DisplayName = "SMB success")]
    [DataRow("error;scheme=smb;reply=C000002", "option --fault: key reply does not fit the smb error shape", DisplayName = "SMB seven digits")]
    [DataRow("error;scheme=smb;reply=C000002G", "option --fault: key reply does not fit the smb error shape", DisplayName = "SMB not hex")]
    [DataRow("error;scheme=mqtt;reply=127", "option --fault: key reply does not fit the mqtt error shape", DisplayName = "MQTT 127")]
    [DataRow("error;scheme=mqtt;reply=135 no", "option --fault: key reply does not fit the mqtt error shape", DisplayName = "MQTT with text")]
    [DataRow("error;scheme=ldap;reply=10 referral", "option --fault: key reply does not fit the ldap error shape", DisplayName = "LDAP referral")]
    [DataRow("error;scheme=ldap;reply=14 x", "option --fault: key reply does not fit the ldap error shape", DisplayName = "LDAP bind in progress")]
    [DataRow("error;scheme=ldap;reply=81 x", "option --fault: key reply does not fit the ldap error shape", DisplayName = "LDAP 81")]
    [DataRow("error;scheme=dict;reply=450 x", "option --fault: key reply does not fit the dict error shape", DisplayName = "DICT 450")]
    [DataRow("error;scheme=coap;reply=2.05", "option --fault: key reply does not fit the coap error shape", DisplayName = "CoAP success")]
    [DataRow("error;scheme=coaps;reply=5.06", "option --fault: key reply does not fit the coaps error shape", DisplayName = "CoAP past 5.05")]
    [DataRow("error;scheme=coap;reply=4.32", "option --fault: key reply does not fit the coap error shape", DisplayName = "CoAP detail past 31")]
    [DataRow("error;scheme=coap;reply=404", "option --fault: key reply does not fit the coap error shape", DisplayName = "CoAP without a dot")]
    [DataRow("error;scheme=coap;reply=4.04 ", "option --fault: key reply does not fit the coap error shape", DisplayName = "CoAP empty text")]
    [DataRow("error;scheme=coap;reply=4.x4", "option --fault: key reply does not fit the coap error shape", DisplayName = "CoAP detail not a number")]
    [DataRow("error;scheme=mcp;reply=-32000", "option --fault: key reply does not fit the mcp error shape", DisplayName = "MCP without a message")]
    [DataRow("error;scheme=mcps;reply=x y", "option --fault: key reply does not fit the mcps error shape", DisplayName = "MCP without a code")]
    [DataRow("error;scheme=mcp;reply=-32000 ", "option --fault: key reply does not fit the mcp error shape", DisplayName = "MCP empty message")]
    [DataRow("error;listener=signalr://127.0.0.1:0;reply=", "option --fault: key reply needs a value", DisplayName = "SignalR empty")]
    [DataRow("delay;ms=1;ms=2", "option --fault: key ms is given twice", DisplayName = "A key twice")]
    [DataRow(";ms=1", "option --fault: the name is empty", DisplayName = "No kind")]
    [DataRow("delay;ms=", "option --fault: key ms needs a value", DisplayName = "Empty value")]
    [DataRow("error;scheme=http;reply=503 a%0Ab", "option --fault: a control character", DisplayName = "A control character in the reply")]
    public void Parse_FaultRefused_IsRefusedWithItsReason(string item, string message) =>
        AssertOptionRefused(["--fault", item, Url], message);

    [TestMethod]
    public void Parse_WebSocketCloseReasonOf123Utf8Bytes_IsServed()
    {
        var reply = "1000 " + new string('é', 61) + "a";

        Assert.AreEqual(reply, Served("--fault", $"error;scheme=ws;reply={reply}", Url).Faults.Single().Reply);
    }

    [TestMethod]
    public void Parse_WebSocketCloseReasonOver123Utf8Bytes_IsRefused() =>
        AssertOptionRefused(
            ["--fault", "error;scheme=ws;reply=1000 " + new string('é', 62), Url],
            "option --fault: key reply does not fit the ws error shape");

    [TestMethod]
    public void Parse_FaultMissingItsArgument_RequiresParameter() =>
        AssertOptionRefused([Url, "--fault"], "option --fault: requires parameter");

    [TestMethod]
    public void Parse_FaultItemPastItsLengthBound_IsRefused() =>
        AssertOptionRefused(["--fault", "drop;path=/" + new string('a', 4096), Url], "option --fault: the item is longer than 4096 characters");

    [TestMethod]
    public void Parse_TwoHundredFiftySixFaults_AreServed() =>
        Assert.HasCount(256, Served([.. FaultArguments(256), Url]).Faults);

    [TestMethod]
    public void Parse_MoreThanTwoHundredFiftySixFaults_IsRefused() =>
        AssertOptionRefused([.. FaultArguments(257), Url], "option --fault: more than 256 fault rules");

    [TestMethod]
    public void Parse_FaultFromAConfigFile_EqualsTheSameItemOnTheCommandLine()
    {
        const string Item = "error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=2";
        var fromFile = CommandLineParser.Parse(["-K", "a.cfg", Url], [], new FakeConfigFileSource(("a.cfg", $"fault = \"{Item}\"\n")));
        var fromCommandLine = Served("--fault", Item, Url);

        Assert.AreEqual(CommandLineOutcome.Serve, fromFile.Outcome, fromFile.Failure?.Message);
        Assert.IsNotNull(fromFile.CommandLine);
        CollectionAssert.AreEqual(fromCommandLine.Faults.ToArray(), fromFile.CommandLine.Faults.ToArray());
    }

    [TestMethod]
    public void Parse_FaultRefusedInAConfigFile_IsLocated()
    {
        var result = CommandLineParser.Parse(["-K", "a.cfg", Url], [], new FakeConfigFileSource(("a.cfg", "verbose\nfault = nosuch\n")));

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual("a.cfg:2: option --fault: unknown kind nosuch", result.Failure?.Message);
    }

    [TestMethod]
    public void Parse_NegatedFault_CannotBeReversed() =>
        AssertOptionRefused(["--no-fault", Url], "option --no-fault: the given option cannot be reversed with a --no- prefix");

    private static IEnumerable<string> FaultArguments(int count) =>
        Enumerable.Range(0, count).SelectMany(index => new[] { "--fault", $"refuse-login;account=a{index}" });

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.IsNotNull(result.CommandLine);
        return result.CommandLine;
    }

    private static void AssertOptionRefused(string[] arguments, string message)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure.ExitCode);
        Assert.AreEqual(message, result.Failure.Message);
        Assert.IsTrue(result.Failure.FollowedByTryHelpLine);
    }
}
