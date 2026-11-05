namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// One step of a <see cref="KafkaSaslLogin"/>, as the broker sends it.
/// </summary>
/// <param name="Outcome">Whether the login goes on, or how it ended.</param>
/// <param name="ServerMessage">
/// The server's message: the challenge, or the additional data with success (SCRAM's
/// server-final); empty when the login was refused.
/// </param>
internal sealed record KafkaSaslStep(KafkaSaslStepOutcome Outcome, ReadOnlyMemory<byte> ServerMessage);
