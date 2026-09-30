namespace Surl.Protocol.Smb;

/// <summary>
/// How reading one NetBIOS session service frame ended (RFC 1002 section 4.3).
/// </summary>
internal enum SmbFrameReadOutcome
{
    /// <summary>A session message (type 0x00) was read whole; it carries one SMB message.</summary>
    MessageRead,

    /// <summary>A session keep-alive (type 0x85) was read and its body, if any, discarded.</summary>
    KeepAlive,

    /// <summary>A frame of any other type was read and its body discarded; the type is reported.</summary>
    UnexpectedFrameType,

    /// <summary>The frame announced a length over the caller's maximum; none of its body was read.</summary>
    MessageTooLarge,

    /// <summary>The client closed the connection between frames.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part way through a frame.</summary>
    ConnectionClosedMidFrame,
}
