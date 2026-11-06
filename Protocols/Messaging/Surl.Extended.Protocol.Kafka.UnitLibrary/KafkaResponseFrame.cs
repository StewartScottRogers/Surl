namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Writes one response as it goes on the wire (the Kafka protocol guide, "Common Request and
/// Response Structure"): an INT32 size of what follows, the response header, then the body.
/// Response header v0 is the <c>correlation_id</c>; v1 adds an empty tagged-field section.
/// </summary>
public static class KafkaResponseFrame
{
    /// <summary>Encodes a response that echoes <paramref name="correlationId"/>.</summary>
    /// <param name="correlationId">The request's correlation id.</param>
    /// <param name="headerVersion">0 or 1.</param>
    /// <param name="body">The response's own fields.</param>
    /// <returns>The size prefix, header and body.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="headerVersion"/> is not 0 or 1.</exception>
    public static byte[] Encode(int correlationId, short headerVersion, ReadOnlySpan<byte> body)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(headerVersion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(headerVersion, (short)1);

        var header = new KafkaPrimitiveWriter();
        header.WriteInt32(correlationId);
        if (headerVersion == 1)
        {
            header.WriteEmptyTaggedFields();
        }

        var frame = new KafkaPrimitiveWriter();
        frame.WriteInt32(header.Length + body.Length);
        frame.WriteRaw(header.ToArray());
        frame.WriteRaw(body);
        return frame.ToArray();
    }
}
