namespace Surl.Protocol.Ws;

/// <summary>
/// The result of handing one frame to a <see cref="WebSocketMessageReassembler"/>.
/// </summary>
/// <param name="Outcome">What the reassembler made of the frame.</param>
/// <param name="Message">The finished message when <paramref name="Outcome"/> is <see cref="WebSocketReassemblyOutcome.MessageComplete"/>; otherwise <see langword="null"/>.</param>
/// <param name="ControlFrame">The frame when <paramref name="Outcome"/> is <see cref="WebSocketReassemblyOutcome.ControlFrame"/>; otherwise <see langword="null"/>.</param>
internal sealed record WebSocketReassemblyStep(WebSocketReassemblyOutcome Outcome, WebSocketMessage? Message, WebSocketFrame? ControlFrame)
{
    /// <summary>
    /// A step carrying only <paramref name="outcome"/>.
    /// </summary>
    /// <param name="outcome">What the reassembler made of the frame.</param>
    /// <returns>A step with neither a message nor a control frame.</returns>
    public static WebSocketReassemblyStep Of(WebSocketReassemblyOutcome outcome) => new(outcome, null, null);
}
