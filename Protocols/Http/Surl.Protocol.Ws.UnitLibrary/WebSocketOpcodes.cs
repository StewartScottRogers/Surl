namespace Surl.Protocol.Ws;

/// <summary>
/// Classifies the opcode nibble of a frame's first byte (RFC 6455 section 5.2).
/// </summary>
internal static class WebSocketOpcodes
{
    /// <summary>
    /// Whether <paramref name="opcode"/> is one section 5.2 defines, rather than one of the
    /// reserved 3-7 and 0xB-0xF.
    /// </summary>
    /// <param name="opcode">The low four bits of a frame's first byte.</param>
    /// <returns><see langword="true"/> for 0, 1, 2, 8, 9 and 0xA.</returns>
    public static bool IsDefined(int opcode) => opcode is <= 0x2 or (>= 0x8 and <= 0xA);

    /// <summary>
    /// Whether <paramref name="opcode"/> names a control frame, whose most significant opcode bit is set (section 5.5).
    /// </summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns><see langword="true"/> for close, ping and pong.</returns>
    public static bool IsControl(WebSocketOpcode opcode) => ((byte)opcode & 0x8) != 0;
}
