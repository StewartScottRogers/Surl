using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decision 7: the examples, and what the parser says of each command line.</summary>
[TestClass]
public sealed class AiHelpExamplesTests
{
    // The file AiHelpExamplePrecondition.ConfigFileExists names.
    // The extended schemes the nats and s3 topics' examples serve, as surl registers them (BL-528, BL-421).
    private static readonly string[] ExtendedSchemesServed = ["coap", "coaps", "dns", "nats", "natss", "s3", "socketio", "socketios", "webtransport", "xmpp", "xmpps"];

    private static readonly FakeConfigFileSource ExampleConfigFiles = new(("surl.cfg", "url = http://127.0.0.1:0/\n"));

    [TestMethod]
    public void Examples_AreTheAdrsExamplesAndEachAddedSinceInItsOrder()
    {
        string[] topics =
        [
            "overview", "listen-urls", "listen-urls", "surl", "surl", "surl", "content", "content", "content", "auth", "auth", "mail", "proxy", "proxy", "testing", "tls", "tls",
            "logging", "limits", "coap", "dict", "dns", "ftp", "gopher", "http", "http", "imap", "ldap", "mqtt", "nats", "pop3", "rtsp", "s3", "smb", "smtp", "socketio", "socks", "socks", "ssh", "telnet", "tftp",
            "websocket", "webtransport", "websocket", "xmpp", "extended", "control",
        ];

        CollectionAssert.AreEqual(topics, AiHelpExamples.All.Select(example => example.Topic).ToArray());
    }

    [TestMethod]
    public void Examples_ARefusedCommandLine_WritesWhatTheParserRefusesWith()
    {
        foreach (var example in AiHelpExamples.All)
        {
            var parsed = CommandLineParser.Parse(example.Arguments.ToArray(), ExtendedSchemesServed, ExampleConfigFiles);
            if (parsed.Outcome != CommandLineOutcome.Refused)
            {
                continue;
            }

            var failure = parsed.Failure!;
            string[] expected = failure.FollowedByTryHelpLine
                ? ["surl: " + failure.Message, "surl: " + CommandLineFailure.TryHelpLine]
                : ["surl: " + failure.Message];
            CollectionAssert.AreEqual(expected, example.Error.ToArray(), example.Title);
            Assert.AreEqual(failure.ExitCode, example.ExitCode, example.Title);
            Assert.IsEmpty(example.Output, example.Title);
        }
    }

    [TestMethod]
    public void Examples_AServingCommandLine_WritesOneListeningLinePerListenUrl()
    {
        const int boundPort = 49731;
        foreach (var example in AiHelpExamples.All.Where(example => example.ServesUntilStopped))
        {
            var parsed = CommandLineParser.Parse(example.Arguments.Select(argument => argument.Replace("<path>", "data", StringComparison.Ordinal)).ToArray(), ExtendedSchemesServed, ExampleConfigFiles);
            Assert.AreEqual(CommandLineOutcome.Serve, parsed.Outcome, example.Title);

            var commandLine = parsed.CommandLine!;
            var expected = commandLine.LogLevel == Surl.Output.LogLevel.None
                ? []
                : commandLine.ListenUrls.Concat(commandLine.ProxyListenUrls)
                    .Select(listenUrl => Surl.Output.ListenerStatusLine.Format(listenUrl.WithBoundPort(boundPort)).Replace(":49731/", ":<port>/", StringComparison.Ordinal))
                    .ToArray();
            CollectionAssert.AreEqual(expected, example.Output.ToArray(), example.Title);
            Assert.AreEqual(SurlExitCode.Ok, example.ExitCode, example.Title);
        }
    }
}
