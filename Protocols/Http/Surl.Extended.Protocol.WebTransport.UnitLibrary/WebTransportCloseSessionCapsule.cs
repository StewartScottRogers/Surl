using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// <c>WT_CLOSE_SESSION</c> (draft-ietf-webtrans-http3-16 section 6): a 32-bit application error
/// code, big-endian, then a UTF-8 message of at most <see cref="MaxMessageLength"/> bytes filling
/// the rest of the value.
/// </summary>
public sealed record WebTransportCloseSessionCapsule : WebTransportCapsule
{
    /// <summary>The longest message, in UTF-8 bytes (WTH3 6's text, ADR-0117 decision 6).</summary>
    public const int MaxMessageLength = 1024;

    /// <summary>The length of the error code that starts the value.</summary>
    internal const int ErrorCodeLength = 4;

    /// <summary>Creates the capsule.</summary>
    /// <param name="errorCode">The application error code.</param>
    /// <param name="message">The message; at most <see cref="MaxMessageLength"/> bytes of UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="message"/> is longer than <see cref="MaxMessageLength"/> bytes in UTF-8.</exception>
    public WebTransportCloseSessionCapsule(uint errorCode, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (Encoding.UTF8.GetByteCount(message) > MaxMessageLength)
        {
            throw new ArgumentException($"A WT_CLOSE_SESSION message is at most {MaxMessageLength} bytes of UTF-8.", nameof(message));
        }

        ErrorCode = errorCode;
        Message = message;
    }

    /// <inheritdoc />
    public override WebTransportCapsuleType Type => WebTransportCapsuleType.CloseSession;

    /// <summary>The application error code.</summary>
    public uint ErrorCode { get; }

    /// <summary>The message; empty when there is none.</summary>
    public string Message { get; }

    /// <inheritdoc />
    internal override byte[] EncodeValue()
    {
        var value = new byte[ErrorCodeLength + Encoding.UTF8.GetByteCount(Message)];
        BinaryPrimitives.WriteUInt32BigEndian(value, ErrorCode);
        Encoding.UTF8.GetBytes(Message, value.AsSpan(ErrorCodeLength));
        return value;
    }
}
