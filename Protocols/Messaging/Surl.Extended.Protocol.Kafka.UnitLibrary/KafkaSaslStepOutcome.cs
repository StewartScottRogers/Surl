namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Whether a <see cref="KafkaSaslStep"/> carries a challenge, or how the login ended.
/// </summary>
internal enum KafkaSaslStepOutcome
{
    /// <summary>Send the challenge and read the client's next message.</summary>
    Challenge,

    /// <summary>The login was accepted.</summary>
    Accepted,

    /// <summary>The login was refused.</summary>
    Refused,
}
