namespace Surl.Protocol.Smb;

/// <summary>
/// What reading one NetBIOS session service frame produced.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="FrameType">The frame's type byte, when its header was read; 0 otherwise.</param>
/// <param name="Message">
/// The SMB message, without its NetBIOS header, when <paramref name="Outcome"/> is
/// <see cref="SmbFrameReadOutcome.MessageRead"/>; its first bytes, up to the 32-byte SMB header,
/// when it is <see cref="SmbFrameReadOutcome.MessageTooLarge"/>; empty otherwise.
/// </param>
/// <param name="MessageLength">The length the frame's header announced, when it was read; 0 otherwise.</param>
internal sealed record SmbFrameReadResult(SmbFrameReadOutcome Outcome, byte FrameType, byte[] Message, int MessageLength = 0)
{
    /// <summary>
    /// A read that produced no SMB message.
    /// </summary>
    /// <param name="outcome">How the read ended.</param>
    /// <param name="frameType">The frame's type byte, when its header was read.</param>
    /// <returns>The result.</returns>
    public static SmbFrameReadResult NoMessage(SmbFrameReadOutcome outcome, byte frameType = 0) => new(outcome, frameType, []);
}
