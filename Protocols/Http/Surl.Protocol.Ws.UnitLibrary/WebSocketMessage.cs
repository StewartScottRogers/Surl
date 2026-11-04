namespace Surl.Protocol.Ws;

/// <summary>
/// A whole data message, its fragments joined (RFC 6455 section 5.4).
/// </summary>
/// <param name="Opcode"><see cref="WebSocketOpcode.Text"/> or <see cref="WebSocketOpcode.Binary"/>, from the message's first frame.</param>
/// <param name="Payload">Every fragment's payload, in order; valid UTF-8 when <paramref name="Opcode"/> is text.</param>
internal sealed record WebSocketMessage(WebSocketOpcode Opcode, byte[] Payload);
