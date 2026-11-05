namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The Kafka error codes the broker answers with (ADR-0105), with the numbers the Kafka protocol
/// guide's "Error Codes" table gives them. Each is written as an INT16.
/// </summary>
public enum KafkaErrorCode : short
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary><c>OFFSET_OUT_OF_RANGE</c>: the offset is outside the log.</summary>
    OffsetOutOfRange = 1,

    /// <summary><c>CORRUPT_MESSAGE</c>: a batch failed its checks.</summary>
    CorruptMessage = 2,

    /// <summary><c>UNKNOWN_TOPIC_OR_PARTITION</c>: no such topic or partition.</summary>
    UnknownTopicOrPartition = 3,

    /// <summary><c>MESSAGE_TOO_LARGE</c>: a record or batch is over a limit.</summary>
    MessageTooLarge = 10,

    /// <summary><c>INVALID_TOPIC_EXCEPTION</c>: the topic name is not a valid Kafka name.</summary>
    InvalidTopicException = 17,

    /// <summary><c>INVALID_REQUIRED_ACKS</c>: <c>acks</c> is not 0, 1 or -1.</summary>
    InvalidRequiredAcks = 21,

    /// <summary><c>TOPIC_AUTHORIZATION_FAILED</c>: the caller may not use the topic.</summary>
    TopicAuthorizationFailed = 29,

    /// <summary><c>UNSUPPORTED_SASL_MECHANISM</c>: the mechanism is not offered.</summary>
    UnsupportedSaslMechanism = 33,

    /// <summary><c>UNSUPPORTED_VERSION</c>: the API version is not served.</summary>
    UnsupportedVersion = 35,

    /// <summary><c>UNSUPPORTED_FOR_MESSAGE_FORMAT</c>: a batch format or attribute is not served.</summary>
    UnsupportedForMessageFormat = 43,

    /// <summary><c>POLICY_VIOLATION</c>: a store bound was reached.</summary>
    PolicyViolation = 44,

    /// <summary><c>SASL_AUTHENTICATION_FAILED</c>: the login was refused.</summary>
    SaslAuthenticationFailed = 58,

    /// <summary><c>FETCH_SESSION_ID_NOT_FOUND</c>: the broker keeps no fetch sessions.</summary>
    FetchSessionIdNotFound = 70,
}
