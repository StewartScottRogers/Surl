namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The served ranges and the header versions they choose (ADR-0105 decision 1; the Kafka protocol
/// guide, "Request and Response Headers").
/// </summary>
[TestClass]
public sealed class KafkaApiVersionRangeTests
{
    [TestMethod]
    [DataRow((short)2, false)]
    [DataRow((short)3, true)]
    [DataRow((short)8, true)]
    [DataRow((short)9, false)]
    public void Serves_ProduceVersion_IsTrueFromThreeToEight(short version, bool expected) =>
        Assert.AreEqual(expected, Range(KafkaApiKey.Produce).Serves(version));

    [TestMethod]
    [DataRow(KafkaApiKey.Produce, (short)8, (short)1, (short)0)]
    [DataRow(KafkaApiKey.Produce, (short)9, (short)2, (short)1)]
    [DataRow(KafkaApiKey.ApiVersions, (short)2, (short)1, (short)0)]
    [DataRow(KafkaApiKey.ApiVersions, (short)3, (short)2, (short)0)]
    [DataRow(KafkaApiKey.SaslHandshake, (short)1, (short)1, (short)0)]
    [DataRow(KafkaApiKey.SaslAuthenticate, (short)2, (short)2, (short)1)]
    public void HeaderVersions_Version_FollowFlexibilityAndApiVersionsKeepsResponseHeaderZero(
        KafkaApiKey apiKey,
        short version,
        short requestHeaderVersion,
        short responseHeaderVersion)
    {
        var range = Range(apiKey);

        Assert.AreEqual(requestHeaderVersion, range.RequestHeaderVersion(version));
        Assert.AreEqual(responseHeaderVersion, range.ResponseHeaderVersion(version));
    }

    [TestMethod]
    public void Ranges_AreAdr0105DecisionOnesTableInApiKeyOrder()
    {
        var expected = new (KafkaApiKey, short, short)[]
        {
            (KafkaApiKey.Produce, 3, 8),
            (KafkaApiKey.Fetch, 4, 11),
            (KafkaApiKey.ListOffsets, 1, 5),
            (KafkaApiKey.Metadata, 0, 8),
            (KafkaApiKey.SaslHandshake, 0, 1),
            (KafkaApiKey.ApiVersions, 0, 3),
            (KafkaApiKey.CreateTopics, 2, 4),
            (KafkaApiKey.SaslAuthenticate, 0, 1),
        };

        var actual = KafkaServedApis.Ranges.Select(range => (range.ApiKey, range.MinVersion, range.MaxVersion)).ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Find_UnservedApiKey_IsNull() => Assert.IsNull(KafkaServedApis.Find(11));

    private static KafkaApiVersionRange Range(KafkaApiKey apiKey) => KafkaServedApis.Find((short)apiKey)!;
}
