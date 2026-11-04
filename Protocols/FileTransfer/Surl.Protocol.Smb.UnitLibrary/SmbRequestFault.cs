namespace Surl.Protocol.Smb;

/// <summary>
/// Why an SMB message could not be decoded into a request, or <see cref="None"/> when it was.
/// </summary>
internal enum SmbRequestFault
{
    /// <summary>The message was decoded.</summary>
    None,

    /// <summary>The message ends before its header, word count or byte count does.</summary>
    Truncated,

    /// <summary>The message does not open with <c>0xFF 'SMB'</c>.</summary>
    BadSignature,

    /// <summary>The word count is not one the command's request has.</summary>
    InconsistentWordCount,

    /// <summary>The byte count runs past the message, or the fields it holds run past the byte count.</summary>
    InconsistentByteCount,

    /// <summary>A string lacks its NUL terminator, or a dialect its <c>0x02</c> buffer format byte.</summary>
    MalformedString,

    /// <summary>A data offset and length point outside the message, or into its header or parameters.</summary>
    OffsetOutsideMessage,

    /// <summary>The request chains a second command through its AndX command, which curl never does.</summary>
    ChainedAndXCommand,

    /// <summary>The command is not one of the eight curl sends.</summary>
    UnsupportedCommand,
}
