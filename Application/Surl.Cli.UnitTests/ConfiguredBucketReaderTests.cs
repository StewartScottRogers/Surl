namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredBucketReader"/> reading <c>&lt;name&gt;=&lt;path&gt;</c> items (ADR-0101 decisions 3 to 5,
/// ADR-0102 decision 2.4).
/// </summary>
[TestClass]
public sealed class ConfiguredBucketReaderTests
{
    [TestMethod]
    public void Read_NameAndPath_ReadsBoth()
    {
        Assert.IsNull(ConfiguredBucketReader.Read("photos=/team/photos", out var bucket));

        Assert.AreEqual(new ConfiguredBucket("photos", "/team/photos"), bucket);
    }

    [TestMethod]
    public void Read_PercentEncodedFields_AreDecoded()
    {
        Assert.IsNull(ConfiguredBucketReader.Read("a%3Db=/p%3Bq", out var bucket));

        Assert.AreEqual(new ConfiguredBucket("a=b", "/p;q"), bucket);
    }

    [TestMethod]
    [DataRow("photos", "expected <name>=<path>", DisplayName = "No separator")]
    [DataRow("=/p", "the name is empty", DisplayName = "Empty name")]
    [DataRow("photos=", "an empty field", DisplayName = "Empty path")]
    [DataRow("photos=/p;readonly", "unknown flag readonly", DisplayName = "A flag")]
    [DataRow("photos=/p;k=v", "unknown key k", DisplayName = "A key")]
    [DataRow("pho%09tos=/p", "a control character", DisplayName = "Control character in the name")]
    [DataRow("photos=/a%00b", "a control character", DisplayName = "U+0000 in the path")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected)
    {
        var failure = ConfiguredBucketReader.Read(argument, out var bucket);

        Assert.AreEqual(expected, failure);
        Assert.AreEqual(new ConfiguredBucket(string.Empty, string.Empty), bucket);
    }

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredBucketReader.Shape.Reading.Type, ConfiguredBucketReader.Reading.Type);

    [TestMethod]
    public void BucketGivenTwice_NamesTheBucket() =>
        Assert.AreEqual("bucket photos is given twice", ConfiguredBucketReader.BucketGivenTwice("photos"));
}
