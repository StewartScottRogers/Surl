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

    /// <summary><c>ILLEGAL_GENERATION</c>: the request names a generation other than the group's.</summary>
    IllegalGeneration = 22,

    /// <summary><c>INCONSISTENT_GROUP_PROTOCOL</c>: an empty protocol type or list, or one the group's members do not share.</summary>
    InconsistentGroupProtocol = 23,

    /// <summary><c>INVALID_GROUP_ID</c>: the group id is empty.</summary>
    InvalidGroupId = 24,

    /// <summary><c>UNKNOWN_MEMBER_ID</c>: the group does not hold the member id.</summary>
    UnknownMemberId = 25,

    /// <summary><c>INVALID_SESSION_TIMEOUT</c>: the session timeout is outside 6000 to 1800000 milliseconds.</summary>
    InvalidSessionTimeout = 26,

    /// <summary><c>REBALANCE_IN_PROGRESS</c>: the group is preparing a rebalance; the member must rejoin.</summary>
    RebalanceInProgress = 27,

    /// <summary><c>TOPIC_AUTHORIZATION_FAILED</c>: the caller may not use the topic.</summary>
    TopicAuthorizationFailed = 29,

    /// <summary><c>GROUP_AUTHORIZATION_FAILED</c>: the caller may not use the consumer group.</summary>
    GroupAuthorizationFailed = 30,

    /// <summary><c>UNSUPPORTED_SASL_MECHANISM</c>: the mechanism is not offered.</summary>
    UnsupportedSaslMechanism = 33,

    /// <summary><c>UNSUPPORTED_VERSION</c>: the API version is not served.</summary>
    UnsupportedVersion = 35,

    /// <summary><c>TOPIC_ALREADY_EXISTS</c>: CreateTopics named a topic that has a log.</summary>
    TopicAlreadyExists = 36,

    /// <summary><c>INVALID_PARTITIONS</c>: a partition count other than one.</summary>
    InvalidPartitions = 37,

    /// <summary><c>INVALID_REPLICATION_FACTOR</c>: a replication factor other than one.</summary>
    InvalidReplicationFactor = 38,

    /// <summary><c>INVALID_REPLICA_ASSIGNMENT</c>: an assignment other than partition 0 on node 0.</summary>
    InvalidReplicaAssignment = 39,

    /// <summary><c>INVALID_REQUEST</c>: the request is malformed or contradicts itself, such as a topic named twice.</summary>
    InvalidRequest = 42,

    /// <summary><c>UNSUPPORTED_FOR_MESSAGE_FORMAT</c>: a batch format or attribute is not served.</summary>
    UnsupportedForMessageFormat = 43,

    /// <summary><c>POLICY_VIOLATION</c>: a store bound was reached.</summary>
    PolicyViolation = 44,

    /// <summary><c>OUT_OF_ORDER_SEQUENCE_NUMBER</c>: an idempotent batch's sequence is not one past the last taken.</summary>
    OutOfOrderSequenceNumber = 45,

    /// <summary><c>INVALID_PRODUCER_EPOCH</c>: an idempotent batch carries an older producer epoch than the one held.</summary>
    InvalidProducerEpoch = 47,

    /// <summary><c>SASL_AUTHENTICATION_FAILED</c>: the login was refused.</summary>
    SaslAuthenticationFailed = 58,

    /// <summary><c>FETCH_SESSION_ID_NOT_FOUND</c>: the broker keeps no fetch sessions.</summary>
    FetchSessionIdNotFound = 70,

    /// <summary><c>MEMBER_ID_REQUIRED</c>: a dynamic member must rejoin with the member id the answer carries.</summary>
    MemberIdRequired = 79,

    /// <summary><c>GROUP_MAX_SIZE_REACHED</c>: the group holds the most members it may.</summary>
    GroupMaxSizeReached = 81,

    /// <summary><c>FENCED_INSTANCE_ID</c>: a newer member id holds the request's group instance id.</summary>
    FencedInstanceId = 82,
}
