namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// What <see cref="KafkaRequestFrameReader"/> found when it read the next request.
/// </summary>
public enum KafkaRequestFrameReadOutcome
{
    /// <summary>A whole request was read.</summary>
    RequestRead,

    /// <summary>The client closed the connection between requests.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part of the way through a request.</summary>
    ConnectionClosedMidRequest,

    /// <summary>
    /// The size prefix was zero, negative or over the request limit; the body was not read
    /// (ADR-0105 decision 7).
    /// </summary>
    SizeRefused,
}
