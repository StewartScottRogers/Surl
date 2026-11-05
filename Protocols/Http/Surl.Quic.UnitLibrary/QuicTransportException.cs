namespace Surl.Quic;

/// <summary>
/// Thrown when received QUIC bytes are malformed. <see cref="ErrorCode"/> is the transport error
/// RFC 9000 names for the fault, and <see cref="FrameType"/> the type of the frame that carried it,
/// or <see langword="null"/> when the fault is in a packet header; together they are what the
/// caller's <c>CONNECTION_CLOSE</c> frame reports. A packet-header fault means the caller drops
/// the packet (RFC 9000 sections 5.2 and 17.2) rather than closing the connection.
/// </summary>
public sealed class QuicTransportException : Exception
{
    /// <summary>Creates the exception for a fault outside any frame.</summary>
    /// <param name="errorCode">The transport error RFC 9000 names.</param>
    /// <param name="message">What was wrong.</param>
    public QuicTransportException(QuicTransportErrorCode errorCode, string message)
        : this(errorCode, message, null)
    {
    }

    /// <summary>Creates the exception for a fault in a frame of the given type.</summary>
    /// <param name="errorCode">The transport error RFC 9000 names.</param>
    /// <param name="message">What was wrong.</param>
    /// <param name="frameType">The type of the frame at fault, or <see langword="null"/> outside any frame.</param>
    public QuicTransportException(QuicTransportErrorCode errorCode, string message, ulong? frameType)
        : base(message)
    {
        ErrorCode = errorCode;
        FrameType = frameType;
    }

    /// <summary>The transport error RFC 9000 names for the fault.</summary>
    public QuicTransportErrorCode ErrorCode { get; }

    /// <summary>The type of the frame at fault, or <see langword="null"/> when no frame was being read.</summary>
    public ulong? FrameType { get; }
}
