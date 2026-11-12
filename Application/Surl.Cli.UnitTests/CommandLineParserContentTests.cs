using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--share</c>, <c>--share-access</c> and <c>--bucket</c> read into <see cref="SurlCommandLine.Shares"/>,
/// <see cref="SurlCommandLine.ShareAccess"/> and <see cref="SurlCommandLine.Buckets"/> from the command line and
/// from a <c>-K</c> file, and refused as ADR-0101 decision 5 says (BL-989).
/// </summary>
[TestClass]
public sealed class CommandLineParserContentTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoContentOption_HasNoSharesAccessOrBuckets()
    {
        var commandLine = Served(Url);

        Assert.IsEmpty(commandLine.Shares);
        Assert.IsEmpty(commandLine.ShareAccess);
        Assert.IsEmpty(commandLine.Buckets);
    }

    [TestMethod]
    public void Parse_ShareAccessAndBucket_AreReadInCommandLineOrder()
    {
        var commandLine = Served(
            "--share", "docs=d;readonly;protocols=ftp", "--share", "media=m", "--share-access", "docs=alice:rw,@staff:r",
            "--share-access", "media=bob:r", "--bucket", "photos=/media/photos", "--bucket", "logs=/docs", Url);

        Assert.AreEqual("docs", commandLine.Shares[0].Name);
        Assert.IsTrue(commandLine.Shares[0].IsReadOnly);
        CollectionAssert.AreEqual(new[] { "ftp" }, commandLine.Shares[0].Protocols!.ToArray());
        Assert.IsNull(commandLine.Shares[1].Protocols);
        CollectionAssert.AreEqual(new[] { "docs", "media" }, commandLine.ShareAccess.Select(access => access.Share).ToArray());
        Assert.AreEqual("rw", commandLine.ShareAccess[0].Grants[0].Access);
        Assert.IsTrue(commandLine.ShareAccess[0].Grants[1].Member.IsGroup);
        CollectionAssert.AreEqual(new[] { "photos", "logs" }, commandLine.Buckets.Select(bucket => bucket.Name).ToArray());
        Assert.AreEqual("/media/photos", commandLine.Buckets[0].Path);
    }

    [TestMethod]
    public void Parse_ContentOptionsInAConfigFile_AreRead()
    {
        var files = new FakeConfigFileSource(("c.cfg", "share = \"docs=d\"\nshare-access = \"docs=alice:r\"\nbucket = \"b=/docs\"\n"));

        var result = CommandLineParser.Parse(["-K", "c.cfg", Url], [], files);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.HasCount(1, result.CommandLine!.Shares);
        Assert.HasCount(1, result.CommandLine.ShareAccess);
        Assert.HasCount(1, result.CommandLine.Buckets);
    }

    [TestMethod]
    [DataRow("--share", "docs", "option --share: expected <name>=<path>[;readonly][;protocols=<scheme>,...]")]
    [DataRow("--share", "docs=d;nosuch", "option --share: unknown flag nosuch")]
    [DataRow("--share", "docs=d;protocols=nosuch", "option --share: unknown scheme nosuch")]
    [DataRow("--share-access", "docs", "option --share-access: expected <share>=<member>:<r|rw>,...")]
    [DataRow("--share-access", "docs=alice:x", "option --share-access: is badly used here")]
    [DataRow("--share-access", "docs=alice:r,alice:rw", "option --share-access: member alice is given twice")]
    [DataRow("--bucket", "photos", "option --bucket: expected <name>=<path>")]
    [DataRow("--bucket", "=/docs", "option --bucket: the name is empty")]
    public void Parse_MalformedItem_IsRefusedByTheOptionsReader(string option, string argument, string message) =>
        AssertOptionRefused([option, argument, Url], message);

    [TestMethod]
    public void Parse_ShareGivenTwice_IsRefusedAtTheSecondOneAsWritten() =>
        AssertOptionRefused(["--share", "docs=a", "--share", "docs=b", Url], "option --share: share docs is given twice");

    [TestMethod]
    public void Parse_ShareGivenTwiceInAConfigFile_IsRefusedWithoutAFilePrefix()
    {
        var files = new FakeConfigFileSource(("c.cfg", "share = \"docs=b\"\n"));

        var result = CommandLineParser.Parse(["--share", "docs=a", "-K", "c.cfg", Url], [], files);

        Assert.AreEqual("option --share: share docs is given twice", result.Failure!.Message);
    }

    [TestMethod]
    public void Parse_BucketGivenTwice_IsRefused() =>
        AssertOptionRefused(["--bucket", "b=/x", "--bucket", "b=/y", Url], "option --bucket: bucket b is given twice");

    [TestMethod]
    public void Parse_ShareAccessForOneShareGivenTwice_IsRefused() =>
        AssertOptionRefused(
            ["--share", "docs=d", "--share-access", "docs=a:r", "--share-access", "docs=b:r", Url],
            "option --share-access: share docs is given twice");

    [TestMethod]
    public void Parse_BucketNamedAsAShare_IsRefusedAtTheBucket() =>
        AssertOptionRefused(["--bucket", "docs=/x", "--share", "docs=d", Url], "option --bucket: bucket docs is also a --share");

    [TestMethod]
    public void Parse_ShareAccessNamingNoShare_IsRefused() =>
        AssertOptionRefused(["--share-access", "docs=a:r", Url], "option --share-access: share docs names no --share");

    [TestMethod]
    public void Parse_ADuplicateAndAConflict_RefusesTheDuplicateFirst() =>
        AssertOptionRefused(
            ["--share-access", "nosuch=a:r", "--bucket", "b=/x", "--bucket", "b=/y", Url],
            "option --bucket: bucket b is given twice");

    [TestMethod]
    public void Parse_ContentOptionsGivenTenThousandTimes_AreServed()
    {
        var commandLine = Served([.. Repeated("--share", 10000, index => $"s{index}=p"), .. Repeated("--bucket", 10000, index => $"b{index}=/p"), Url]);

        Assert.HasCount(10000, commandLine.Shares);
        Assert.HasCount(10000, commandLine.Buckets);
    }

    [TestMethod]
    public void Parse_ShareGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused([.. Repeated("--share", 10001, index => $"s{index}=p"), Url], "option --share: given more than 10000 times");

    [TestMethod]
    public void Parse_BucketGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused([.. Repeated("--bucket", 10001, index => $"b{index}=/p"), Url], "option --bucket: given more than 10000 times");

    [TestMethod]
    public void Parse_ShareAccessGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused([.. Repeated("--share-access", 10001, index => $"s{index}=a:r"), Url], "option --share-access: given more than 10000 times");

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
