namespace Surl.Protocol.Ws;

/// <summary>
/// One frame as RFC 6455 section 5.2 lays it out, with its payload already unmasked.
/// </summary>
/// <param name="Fin">The <c>FIN</c> bit: this frame ends its message.</param>
/// <param name="Rsv1">The <c>RSV1</c> bit, which only a negotiated extension may set.</param>
/// <param name="Rsv2">The <c>RSV2</c> bit, which only a negotiated extension may set.</param>
/// <param name="Rsv3">The <c>RSV3</c> bit, which only a negotiated extension may set.</param>
/// <param name="Opcode">What the frame carries.</param>
/// <param name="Masked">The <c>MASK</c> bit: the peer masked the payload, as every client must (section 5.1).</param>
/// <param name="Payload">The payload, unmasked.</param>
internal sealed record WebSocketFrame(bool Fin, bool Rsv1, bool Rsv2, bool Rsv3, WebSocketOpcode Opcode, bool Masked, byte[] Payload)
{
    /// <summary>
    /// Whether the frame is a control frame: close, ping or pong (section 5.5).
    /// </summary>
    public bool IsControl => WebSocketOpcodes.IsControl(Opcode);
}
