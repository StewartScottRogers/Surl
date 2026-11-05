using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers ApiVersions v0 to v3 (ADR-0105, decision 1; the Kafka protocol guide, ApiVersions):
/// error code 0 and one row per API in <see cref="KafkaServedApis"/>, with its lowest and highest
/// served version, and nothing else. v1 on add <c>throttle_time_ms</c>, always 0. v3 is flexible:
/// its request's <c>client_software_name</c> and <c>client_software_version</c> are read and
/// ignored, its array is compact, and it writes no tagged field, so <c>supported_features</c>
/// and <c>finalized_features</c> are empty and <c>zk_migration_ready</c> false. It may come at
/// any time, before a login included (decision 5).
/// </summary>
public sealed class KafkaApiVersionsAnswerer : IKafkaApiAnswerer
{
    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.ApiVersions;

    /// <inheritdoc/>
    public ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        if (request.Range.IsFlexible(version))
        {
            request.Fields.ReadCompactString();
            request.Fields.ReadCompactString();
            request.Fields.SkipTaggedFields();
        }

        return ValueTask.FromResult(KafkaAnswer.Respond(EncodeBody(version, KafkaErrorCode.None)));
    }

    /// <summary>
    /// The ApiVersions response body at <paramref name="version"/>: <c>error_code</c>, the
    /// <c>api_versions</c> array of <c>api_key</c>, <c>min_version</c> and <c>max_version</c>,
    /// then from v1 <c>throttle_time_ms</c> 0; v3 in its compact, tagged form.
    /// </summary>
    /// <param name="version">0 to 3.</param>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The body, without the response header.</returns>
    public static byte[] EncodeBody(short version, KafkaErrorCode errorCode)
    {
        var isFlexible = version >= 3;
        var body = new KafkaPrimitiveWriter();
        body.WriteInt16((short)errorCode);
        if (isFlexible)
        {
            body.WriteCompactArrayLength(KafkaServedApis.Ranges.Count);
        }
        else
        {
            body.WriteArrayLength(KafkaServedApis.Ranges.Count);
        }

        foreach (var range in KafkaServedApis.Ranges)
        {
            WriteRow(body, range, isFlexible);
        }

        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        if (isFlexible)
        {
            body.WriteEmptyTaggedFields();
        }

        return body.ToArray();
    }

    private static void WriteRow(KafkaPrimitiveWriter body, KafkaApiVersionRange range, bool isFlexible)
    {
        body.WriteInt16((short)range.ApiKey);
        body.WriteInt16(range.MinVersion);
        body.WriteInt16(range.MaxVersion);
        if (isFlexible)
        {
            body.WriteEmptyTaggedFields();
        }
    }
}
