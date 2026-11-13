using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary><c>--kafka-fetch-compression</c> (ADR-0105 decision 4, BL-974).</summary>
[TestClass]
public sealed class KafkaFetchCompressionOptionTests
{
    private static SurlCommandLine Served(params string[] arguments) =>
        CommandLineParser.Parse(arguments, ["kafka"]).CommandLine!;

    [TestMethod]
    [DataRow("none")]
    [DataRow("gzip")]
    [DataRow("snappy")]
    [DataRow("lz4")]
    [DataRow("zstd")]
    public void Parse_EachKafkaFetchCompressionWord_IsRead(string word) =>
        Assert.AreEqual(word, Served("--kafka-fetch-compression", word, "kafka://127.0.0.1:0").KafkaFetchCompression);

    [TestMethod]
    public void Parse_KafkaFetchCompressionInAnyCase_IsLowerCased() =>
        Assert.AreEqual("zstd", Served("--kafka-fetch-compression=ZSTD", "kafka://127.0.0.1:0").KafkaFetchCompression);

    [TestMethod]
    public void Parse_NoKafkaFetchCompression_IsNone() =>
        Assert.AreEqual("none", Served("kafka://127.0.0.1:0").KafkaFetchCompression);

    [TestMethod]
    [DataRow("brotli")]
    [DataRow("")]
    public void Parse_UnknownKafkaFetchCompression_IsRefusedFailedInitWithTheOptionErrorText(string value)
    {
        var result = CommandLineParser.Parse(["--kafka-fetch-compression", value, "kafka://127.0.0.1:0"], ["kafka"]);

        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure!.ExitCode);
        StringAssert.Contains(result.Failure.Message, "--kafka-fetch-compression");
        StringAssert.Contains(result.Failure.Message, OptionArgumentReader.BadlyUsed);
    }

    [TestMethod]
    public void KafkaFetchCompression_AllowedValues_NameTheFiveCodecs()
    {
        foreach (var word in OptionArgumentReader.KafkaFetchCompressionWords)
        {
            StringAssert.Contains(OptionArgumentReader.KafkaFetchCompression.Type.AllowedValues, word);
        }

        Assert.AreEqual(5, OptionArgumentReader.KafkaFetchCompressionWords.Count);
    }
}
