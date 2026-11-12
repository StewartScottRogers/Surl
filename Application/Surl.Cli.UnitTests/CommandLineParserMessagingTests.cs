using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> and <c>--topic-access</c> read into
/// <see cref="SurlCommandLine.Topics"/>, <see cref="SurlCommandLine.Queues"/>, <see cref="SurlCommandLine.TopicMaps"/>
/// and <see cref="SurlCommandLine.TopicAccess"/> from the command line and from a <c>-K</c> file, and refused as
/// ADR-0101 decision 5 says (BL-992).
/// </summary>
[TestClass]
public sealed class CommandLineParserMessagingTests
{
    private const string Url = "mqtt://127.0.0.1:1883/";

    [TestMethod]
    public void Parse_NoMessagingOption_HasNoTopicsQueuesMapsOrAccess()
    {
        var commandLine = Served(Url);

        Assert.IsEmpty(commandLine.Topics);
        Assert.IsEmpty(commandLine.Queues);
        Assert.IsEmpty(commandLine.TopicMaps);
        Assert.IsEmpty(commandLine.TopicAccess);
    }

    [TestMethod]
    public void Parse_MessagingOptions_AreReadInCommandLineOrder()
    {
        var commandLine = Served(
            "--topic", "shop/#;retain;qos=1", "--topic", "news", "--queue", "jobs;durable", "--queue", "mail",
            "--topic-map", "amqp:orders=mqtt:shop/orders", "--topic-access", "shop/#=alice:pubsub,@staff:sub", Url);

        CollectionAssert.AreEqual(new[] { "shop/#", "news" }, commandLine.Topics.Select(topic => topic.Filter).ToArray());
        Assert.IsTrue(commandLine.Topics[0].IsRetained);
        Assert.AreEqual(1, commandLine.Topics[0].MaxQos);
        Assert.IsNull(commandLine.Topics[1].MaxQos);
        CollectionAssert.AreEqual(new[] { "jobs", "mail" }, commandLine.Queues.Select(queue => queue.Name).ToArray());
        Assert.IsTrue(commandLine.Queues[0].IsDurable);
        Assert.AreEqual("orders", commandLine.TopicMaps[0].FromName);
        Assert.AreEqual("mqtt", commandLine.TopicMaps[0].ToScheme);
        Assert.AreEqual("shop/#", commandLine.TopicAccess[0].Filter);
        Assert.IsTrue(commandLine.TopicAccess[0].Grants[1].Member.IsGroup);
    }

    [TestMethod]
    public void Parse_MessagingOptionsInAConfigFile_AreRead()
    {
        var files = new FakeConfigFileSource(("c.cfg", "topic = \"a/b\"\nqueue = \"q\"\ntopic-map = \"amqp:x=mqtt:y\"\ntopic-access = \"a/b=alice:pub\"\n"));

        var result = CommandLineParser.Parse(["-K", "c.cfg", Url], [], files);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.HasCount(1, result.CommandLine!.Topics);
        Assert.HasCount(1, result.CommandLine.Queues);
        Assert.HasCount(1, result.CommandLine.TopicMaps);
        Assert.HasCount(1, result.CommandLine.TopicAccess);
    }

    [TestMethod]
    [DataRow("--topic", "a;qos=3", "option --topic: is badly used here")]
    [DataRow("--topic", "a;nosuch", "option --topic: unknown flag nosuch")]
    [DataRow("--queue", "q;nosuch", "option --queue: unknown flag nosuch")]
    [DataRow("--topic-map", "amqp:orders", "option --topic-map: expected <scheme>:<name>=<scheme>:<name>")]
    [DataRow("--topic-map", "orders=mqtt:x", "option --topic-map: expected <scheme>:<name>=<scheme>:<name>")]
    [DataRow("--topic-map", "nosuch:a=mqtt:b", "option --topic-map: unknown scheme nosuch")]
    [DataRow("--topic-access", "a", "option --topic-access: expected <filter>=<member>:<pub|sub|pubsub>,...")]
    [DataRow("--topic-access", "a=alice:x", "option --topic-access: is badly used here")]
    [DataRow("--topic-access", "a=alice:pub,alice:sub", "option --topic-access: member alice is given twice")]
    public void Parse_MalformedItem_IsRefusedByTheOptionsReader(string option, string argument, string message) =>
        AssertOptionRefused([option, argument, Url], message);

    [TestMethod]
    public void Parse_TopicGivenTwice_IsRefusedAtTheSecondOneAsWritten() =>
        AssertOptionRefused(["--topic", "a", "--topic", "a;retain", Url], "option --topic: topic a is given twice");

    [TestMethod]
    public void Parse_QueueGivenTwiceInAConfigFile_IsRefusedWithoutAFilePrefix()
    {
        var files = new FakeConfigFileSource(("c.cfg", "queue = \"q\"\n"));

        var result = CommandLineParser.Parse(["--queue", "q", "-K", "c.cfg", Url], [], files);

        Assert.AreEqual("option --queue: queue q is given twice", result.Failure!.Message);
    }

    [TestMethod]
    public void Parse_TopicMapSourceGivenTwice_IsRefused() =>
        AssertOptionRefused(
            ["--topic-map", "amqp:a=mqtt:x", "--topic-map", "amqp:a=mqtt:y", Url],
            "option --topic-map: topic map amqp:a is given twice");

    [TestMethod]
    public void Parse_TopicAccessFilterGivenTwice_IsRefused() =>
        AssertOptionRefused(
            ["--topic-access", "a=alice:pub", "--topic-access", "a=bob:sub", Url],
            "option --topic-access: topic a is given twice");

    [TestMethod]
    public void Parse_MessagingOptionsGivenTenThousandTimes_AreServed()
    {
        var commandLine = Served([.. Repeated("--topic", 10000, index => $"t{index}"), .. Repeated("--queue", 10000, index => $"q{index}"), Url]);

        Assert.HasCount(10000, commandLine.Topics);
        Assert.HasCount(10000, commandLine.Queues);
    }

    [TestMethod]
    [DataRow("--topic", "t{0}")]
    [DataRow("--queue", "q{0}")]
    [DataRow("--topic-map", "amqp:a{0}=mqtt:b")]
    [DataRow("--topic-access", "t{0}=a:pub")]
    public void Parse_MessagingOptionGivenMoreThanTenThousandTimes_IsRefused(string option, string pattern) =>
        AssertOptionRefused(
            [.. Repeated(option, 10001, index => string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, index)), Url],
            $"option {option}: given more than 10000 times");

    private static IEnumerable<string> Repeated(string option, int count, Func<int, string> argument) =>
        Enumerable.Range(0, count).SelectMany(index => new[] { option, argument(index) });

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
