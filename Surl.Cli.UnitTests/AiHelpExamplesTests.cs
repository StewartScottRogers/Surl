using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decision 7: the examples, and what the parser says of each command line.</summary>
[TestClass]
public sealed class AiHelpExamplesTests
{
    [TestMethod]
    public void Examples_AreTheAdrsNineteenSmtpsSshFtpKeytabAndImapInItsOrder()
    {
        string[] topics =
        [
            "overview", "listen-urls", "listen-urls", "surl", "content", "content", "content", "auth", "auth", "testing", "tls", "tls",
            "logging", "limits", "dict", "ftp", "gopher", "http", "imap", "mqtt", "smtp", "ssh", "telnet", "tftp",
        ];

        CollectionAssert.AreEqual(topics, AiHelpExamples.All.Select(example => example.Topic).ToArray());
    }

    [TestMethod]
    public void Examples_ARefusedCommandLine_WritesWhatTheParserRefusesWith()
    {
        foreach (var example in AiHelpExamples.All)
        {
            var parsed = CommandLineParser.Parse(example.Arguments.ToArray());
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
            var parsed = CommandLineParser.Parse(example.Arguments.Select(argument => argument.Replace("<path>", "data", StringComparison.Ordinal)).ToArray());
            Assert.AreEqual(CommandLineOutcome.Serve, parsed.Outcome, example.Title);

            var commandLine = parsed.CommandLine!;
            var expected = commandLine.LogLevel == Surl.Output.LogLevel.None
                ? []
                : commandLine.ListenUrls
                    .Select(listenUrl => Surl.Output.ListenerStatusLine.Format(listenUrl.WithBoundPort(boundPort)).Replace(":49731/", ":<port>/", StringComparison.Ordinal))
                    .ToArray();
            CollectionAssert.AreEqual(expected, example.Output.ToArray(), example.Title);
            Assert.AreEqual(SurlExitCode.Ok, example.ExitCode, example.Title);
        }
    }
}
