using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class SurlServerArgumentsTests
{
    [TestMethod]
    public void Compose_OnlyAListenUrl_IsTheListenUrlAlone()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");

        var arguments = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:0" }, arguments.ToArray());
    }

    [TestMethod]
    public void Parse_OnlyAListenUrl_ComposesTheSameCommandLineAsSurlWithNoOtherOption()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        var expected = CommandLineParser.Parse(["http://127.0.0.1:0"]).CommandLine!;

        var actual = SurlServerArguments.Parse(options, []);

        CollectionAssert.AreEqual(expected.ListenUrls.ToArray(), actual.ListenUrls.ToArray());
        Assert.AreEqual(expected with { ListenUrls = actual.ListenUrls }, actual);
    }

    [TestMethod]
    public void Compose_EveryContentPropertyAndALevel_IsEachOptionAfterTheListenUrlsThenTheAdditionalArguments()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0", "ftp://127.0.0.1:0");
        options.DataDirectory = "served";
        options.AllowUploads = true;
        options.ListDirectories = true;
        options.FollowSymlinks = true;
        options.ServeDotFiles = true;
        options.LogLevel = LogLevel.Verbose;
        options.AdditionalArguments.Add("--max-time");
        options.AdditionalArguments.Add("30");

        var arguments = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(
            new[]
            {
                "http://127.0.0.1:0", "ftp://127.0.0.1:0", "--directory", "served", "--allow-uploads", "--list-directories",
                "--follow-symlinks", "--serve-dot-files", "--log-level", "verbose", "--max-time", "30",
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void Parse_EveryContentProperty_SetsWhatEachOptionSets()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";
        options.AllowUploads = true;
        options.ListDirectories = true;
        options.FollowSymlinks = true;
        options.ServeDotFiles = true;
        options.LogLevel = LogLevel.None;

        var commandLine = SurlServerArguments.Parse(options, []);

        Assert.AreEqual("served", commandLine.DataDirectory);
        Assert.IsTrue(commandLine.AllowUploads && commandLine.ListDirectories && commandLine.FollowSymlinks && commandLine.ServeDotFiles);
        Assert.AreEqual(LogLevel.None, commandLine.LogLevel);
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--aihelp")]
    [DataRow("--manual")]
    [DataRow("--version")]
    public void Parse_AnOptionThatStartsNoServer_ThrowsArgument(string option)
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AdditionalArguments.Add(option);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.Parse(options, []));
    }

    [TestMethod]
    [DataRow("--log-file")]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    public void Parse_AFileOptionAHostOwns_ThrowsArgument(string option)
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AdditionalArguments.Add(option);
        options.AdditionalArguments.Add("written.log");

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.Parse(options, []));
    }

    [TestMethod]
    public void Parse_ARefusedOptionFollowedByTheTryHelpLine_ThrowsBothLines()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AdditionalArguments.Add("--no-such-option");
        var expected = CommandLineParser.Parse(["http://127.0.0.1:0", "--no-such-option"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.IsTrue(expected.FollowedByTryHelpLine);
        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        CollectionAssert.AreEqual(new[] { expected.Message, CommandLineFailure.TryHelpLine }, exception.Lines.ToArray());
        Assert.AreEqual(expected.Message + "\n" + CommandLineFailure.TryHelpLine, exception.Message);
    }

    [TestMethod]
    public void ToStartFailure_ARefusalWithoutTheTryHelpLine_IsTheMessageAlone()
    {
        var failure = SurlServerArguments.ToStartFailure(new CommandLineFailure(SurlExitCode.UnsupportedProtocol, "(1) refused"));

        CollectionAssert.AreEqual(new[] { "(1) refused" }, failure.Lines.ToArray());
    }

    [TestMethod]
    public void ThrowIfMisused_NullOptions_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SurlServerArguments.ThrowIfMisused(null!));
    }

    [TestMethod]
    public void ThrowIfMisused_NoListenUrl_ThrowsArgument()
    {
        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(new SurlServerOptions()));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullListenUrl_ThrowsArgument()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.ListenUrls.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullAdditionalArgument_ThrowsArgument()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AdditionalArguments.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ADataDirectoryAndFiles_ThrowsArgument()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";
        options.Files["a.txt"] = [1];

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ADataDirectoryAlone_IsAccepted()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";

        SurlServerArguments.ThrowIfMisused(options);

        Assert.AreEqual("served", options.DataDirectory);
    }

    [TestMethod]
    public void ThrowIfMisused_AMisusedFilePath_ThrowsArgument()
    {
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Files["../a.txt"] = [1];

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }
}
