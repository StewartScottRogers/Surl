namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The result of reading one datagram as a CoAP message: the message, or why it is none, with the
/// header's type and Message ID whenever the header was readable, so a CON can be rejected.
/// </summary>
public sealed class CoapReadOutcome
{
    private CoapReadOutcome(CoapReadOutcomeKind kind, CoapMessage? message, CoapMessageType type, ushort messageId, string fault)
    {
        Kind = kind;
        Message = message;
        Type = type;
        MessageId = messageId;
        Fault = fault;
    }

    /// <summary>What the datagram was.</summary>
    public CoapReadOutcomeKind Kind { get; }

    /// <summary>The message read; <see langword="null"/> unless <see cref="Kind"/> is <see cref="CoapReadOutcomeKind.Message"/>.</summary>
    public CoapMessage? Message { get; }

    /// <summary>The header's type; meaningless for <see cref="CoapReadOutcomeKind.NotCoap"/>.</summary>
    public CoapMessageType Type { get; }

    /// <summary>The header's Message ID; meaningless for <see cref="CoapReadOutcomeKind.NotCoap"/>.</summary>
    public ushort MessageId { get; }

    /// <summary>Why the datagram is no message, as one sentence; empty for a message.</summary>
    public string Fault { get; }

    internal static CoapReadOutcome Read(CoapMessage message) =>
        new(CoapReadOutcomeKind.Message, message, message.Type, message.MessageId, string.Empty);

    internal static CoapReadOutcome NotCoap(string fault) =>
        new(CoapReadOutcomeKind.NotCoap, null, default, 0, fault);

    internal static CoapReadOutcome FormatError(CoapMessageType type, ushort messageId, string fault) =>
        new(CoapReadOutcomeKind.FormatError, null, type, messageId, fault);
}
