using System.Buffers;
using System.Text.Unicode;

namespace Surl.Protocol.Ws;

/// <summary>
/// Joins data frames into messages (RFC 6455 section 5.4): a text or binary frame starts a
/// message, continuation frames extend it in order, and the frame with <c>FIN</c> set ends it.
/// Control frames pass straight through, between fragments or not.
/// </summary>
/// <remarks>
/// A text message is validated as UTF-8 once it is whole (section 8.1), so a code point split
/// across fragments is accepted. It is not safe for concurrent calls, and after any outcome that
/// names an invalid message the caller stops handing frames over.
/// </remarks>
internal sealed class WebSocketMessageReassembler
{
    private readonly long maxMessageBytes;
    private readonly ArrayBufferWriter<byte> heldPayload = new();
    private WebSocketOpcode? heldOpcode;

    /// <summary>
    /// Creates a reassembler.
    /// </summary>
    /// <param name="maxMessageBytes">The most bytes a joined payload may hold; 0 means no limit.</param>
    public WebSocketMessageReassembler(long maxMessageBytes)
    {
        this.maxMessageBytes = maxMessageBytes;
    }

    /// <summary>
    /// Hands over the next frame the reader returned.
    /// </summary>
    /// <param name="frame">A frame the frame reader read.</param>
    /// <returns>What became of it.</returns>
    public WebSocketReassemblyStep Accept(WebSocketFrame frame)
    {
        if (frame.IsControl)
        {
            return new WebSocketReassemblyStep(WebSocketReassemblyOutcome.ControlFrame, null, frame);
        }

        var isContinuation = frame.Opcode == WebSocketOpcode.Continuation;
        if (isContinuation != heldOpcode.HasValue)
        {
            return WebSocketReassemblyStep.Of(
                isContinuation ? WebSocketReassemblyOutcome.ContinuationWithoutMessage : WebSocketReassemblyOutcome.DataFrameInsideMessage);
        }

        return HoldFragment(frame);
    }

    private WebSocketReassemblyStep HoldFragment(WebSocketFrame frame)
    {
        heldOpcode ??= frame.Opcode;
        if (maxMessageBytes > 0 && heldPayload.WrittenCount + frame.Payload.Length > maxMessageBytes)
        {
            return WebSocketReassemblyStep.Of(WebSocketReassemblyOutcome.MessageTooLarge);
        }

        heldPayload.Write(frame.Payload);
        return frame.Fin ? CompleteMessage() : WebSocketReassemblyStep.Of(WebSocketReassemblyOutcome.FragmentHeld);
    }

    private WebSocketReassemblyStep CompleteMessage()
    {
        var message = new WebSocketMessage(heldOpcode.GetValueOrDefault(), heldPayload.WrittenSpan.ToArray());
        heldOpcode = null;
        heldPayload.Clear();
        return message.Opcode == WebSocketOpcode.Text && !Utf8.IsValid(message.Payload)
            ? WebSocketReassemblyStep.Of(WebSocketReassemblyOutcome.TextNotUtf8)
            : new WebSocketReassemblyStep(WebSocketReassemblyOutcome.MessageComplete, message, null);
    }
}
