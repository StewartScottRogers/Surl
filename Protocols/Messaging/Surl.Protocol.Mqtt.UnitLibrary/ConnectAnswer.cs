namespace Surl.Protocol.Mqtt;

/// <summary>
/// The four bytes an MQTT test-case server half writes in place of a <c>CONNACK</c> (ADR-0155
/// decisions 1 to 3), and how its log line names them.
/// </summary>
/// <param name="LoggedName">The packet name the log line shows.</param>
/// <param name="LoggedRemainingLength">The remaining length the log line shows: the header's, not the bytes present.</param>
/// <param name="Bytes">The bytes written.</param>
internal sealed record ConnectAnswer(string LoggedName, int LoggedRemainingLength, byte[] Bytes);
