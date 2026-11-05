namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The answer to an ApiVersions request above the served range (ADR-0105 decision 1; the
/// Kafka protocol guide, "Retrieving Supported API versions"): a version 0 ApiVersions response
/// with error code <c>UNSUPPORTED_VERSION</c> (35) and every served API's range, so the client
/// can retry at the highest version both support.
/// </summary>
public static class KafkaApiVersionsFallback
{
    /// <summary>The response body: <c>error_code</c>, then the <c>api_versions</c> ARRAY of
    /// <c>api_key</c>, <c>min_version</c> and <c>max_version</c>, each an INT16.</summary>
    /// <returns>The body, without the response header.</returns>
    public static byte[] EncodeBody()
    {
        var body = new KafkaPrimitiveWriter();
        body.WriteInt16((short)KafkaErrorCode.UnsupportedVersion);
        body.WriteArrayLength(KafkaServedApis.Ranges.Count);
        foreach (var range in KafkaServedApis.Ranges)
        {
            body.WriteInt16((short)range.ApiKey);
            body.WriteInt16(range.MinVersion);
            body.WriteInt16(range.MaxVersion);
        }

        return body.ToArray();
    }
}
