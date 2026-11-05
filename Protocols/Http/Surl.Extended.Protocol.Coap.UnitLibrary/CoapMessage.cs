namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One CoAP message (RFC 7252 section 3): its type, code, Message ID, token, options in order of
/// their numbers, and payload. The version is always 1, the only one defined.
/// </summary>
public sealed class CoapMessage
{
    /// <summary>The longest token, 8 bytes (section 3: lengths 9 to 15 are reserved).</summary>
    public const int MaxTokenLength = 8;

    /// <summary>
    /// Creates a message. Options are kept in order of their numbers; options sharing a number
    /// keep the order they were given in (section 5.4.5).
    /// </summary>
    /// <param name="type">CON, NON, ACK or RST.</param>
    /// <param name="code">The request method, response code or 0.00 Empty.</param>
    /// <param name="messageId">The Message ID.</param>
    /// <param name="token">The token, 0 to <see cref="MaxTokenLength"/> bytes.</param>
    /// <param name="options">The options; none when <see langword="null"/>.</param>
    /// <param name="payload">The payload; none when empty.</param>
    /// <exception cref="ArgumentOutOfRangeException">The type is not one of the four, or the token is too long.</exception>
    /// <exception cref="ArgumentException">An Empty message has a token, an option or a payload (section 4.1).</exception>
    public CoapMessage(
        CoapMessageType type,
        CoapCode code,
        ushort messageId,
        ReadOnlyMemory<byte> token = default,
        IEnumerable<CoapOption>? options = null,
        ReadOnlyMemory<byte> payload = default)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)type, (uint)CoapMessageType.Reset, nameof(type));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(token.Length, MaxTokenLength, nameof(token));

        var ordered = (options ?? []).OrderBy(option => option.Number).ToArray();

        if (code.IsEmpty && (token.Length + ordered.Length + payload.Length) != 0)
        {
            throw new ArgumentException("An Empty message has no token, option or payload.", nameof(code));
        }

        Type = type;
        Code = code;
        MessageId = messageId;
        Token = token;
        Options = Array.AsReadOnly(ordered);
        Payload = payload;
    }

    /// <summary>The message type.</summary>
    public CoapMessageType Type { get; }

    /// <summary>The code.</summary>
    public CoapCode Code { get; }

    /// <summary>The Message ID.</summary>
    public ushort MessageId { get; }

    /// <summary>The token: opaque bytes the server makes no assumption about (section 5.3.1).</summary>
    public ReadOnlyMemory<byte> Token { get; }

    /// <summary>The options, in order of their numbers.</summary>
    public IReadOnlyList<CoapOption> Options { get; }

    /// <summary>The payload; empty when there is none.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// The Empty RST that rejects the message with <paramref name="messageId"/> (section 4.2).
    /// </summary>
    /// <param name="messageId">The Message ID of the message rejected.</param>
    /// <returns>The RST.</returns>
    public static CoapMessage EmptyReset(ushort messageId) =>
        new(CoapMessageType.Reset, CoapCode.Empty, messageId);
}
