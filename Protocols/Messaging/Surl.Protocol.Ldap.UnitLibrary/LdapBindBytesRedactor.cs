using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for one LDAP connection: it parses the BER framing of each
/// <c>LDAPMessage</c> (RFC 4511 section 4.1.1) across chunk boundaries and writes the password of a
/// simple <c>BindRequest</c> (<c>[0]</c>, section 4.2) and the credentials of a SASL <c>PLAIN</c> bind
/// as <c>****</c> (ADR-0182). Every tag and length octet is logged as received except the secret's own length, which is
/// written as 4 (the length of <c>****</c>), so a log never shows how long the secret is; only the
/// secret's value bytes and that length change. The enclosing <c>BindRequest</c> and <c>LDAPMessage</c>
/// lengths are kept as received. Every other message is passed through as it arrives, and so is every byte after one
/// that does not start a <c>SEQUENCE</c> (a SASL security layer's buffers, or the TLS records after
/// <c>StartTLS</c>).
/// </summary>
/// <remarks>
/// A bind is held until it is whole, never more than <see cref="MaxHeldBytes"/>; a longer one is
/// logged as a single <c>****</c>. A bind cut off by the end of the exchange has everything from the
/// field it stopped in written as <c>****</c>.
/// </remarks>
internal sealed class LdapBindBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a bind is held for before it is written as one mask.</summary>
    internal const int MaxHeldBytes = 65536;

    private const byte SequenceTag = 0x30;
    private const byte BindRequestTag = 0x60;
    private const byte SimplePasswordTag = 0x80;
    private const byte SaslCredentialsTag = 0xA3;
    private const byte OctetStringTag = 0x04;
    private const int LongestUndecidedMessageIdBytes = 16;
    private const int MaxLengthOctets = 4;

    private static ReadOnlySpan<byte> Mask => "****"u8;

    private static ReadOnlySpan<byte> MaskLength => [4];

    private static ReadOnlySpan<byte> PlainMechanism => "PLAIN"u8;

    private readonly List<byte> held = [];
    private long passThroughRemaining;
    private long skipRemaining;
    private bool passesEverything;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        held.AddRange(received);
        while (held.Count > 0 && Advance(output))
        {
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // An LDAP secret is never preceded by something the server sent.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        WriteRedacted(CollectionsMarshal.AsSpan(held), output);
        held.Clear();
    }

    // Does the next step on the held bytes; false when more bytes are needed.
    private bool Advance(ArrayBufferWriter<byte> output)
    {
        if (passesEverything)
        {
            Consume(held.Count, output, isLogged: true);
            return false;
        }

        if (passThroughRemaining > 0)
        {
            passThroughRemaining -= Consume(passThroughRemaining, output, isLogged: true);
            return true;
        }

        if (skipRemaining > 0)
        {
            skipRemaining -= Consume(skipRemaining, output, isLogged: false);
            return true;
        }

        return AdvanceMessage(output);
    }

    private int Consume(long limit, ArrayBufferWriter<byte> output, bool isLogged)
    {
        var count = (int)Math.Min(limit, held.Count);
        if (isLogged)
        {
            output.Write(CollectionsMarshal.AsSpan(held)[..count]);
        }

        held.RemoveRange(0, count);
        return count;
    }

    // Decides the message at the front of the held bytes from its header and its operation's tag.
    private bool AdvanceMessage(ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(held);
        var message = ReadTlv(bytes, 0);
        if (bytes[0] != SequenceTag || message.IsMalformed)
        {
            passesEverything = true;
            return true;
        }

        var messageId = ReadTlv(bytes, message.ValueStart);
        if (!messageId.IsWhole)
        {
            passesEverything = bytes.Length - message.ValueStart > LongestUndecidedMessageIdBytes;
            return passesEverything;
        }

        if (messageId.End >= bytes.Length)
        {
            return false;
        }

        if (bytes[messageId.End] != BindRequestTag)
        {
            passThroughRemaining = message.DeclaredEnd;
            return true;
        }

        return AdvanceBind(output, message.DeclaredEnd);
    }

    private bool AdvanceBind(ArrayBufferWriter<byte> output, long messageEnd)
    {
        if (messageEnd > MaxHeldBytes)
        {
            output.Write(Mask);
            skipRemaining = messageEnd;
            return true;
        }

        if (held.Count < messageEnd)
        {
            return false;
        }

        WriteRedacted(CollectionsMarshal.AsSpan(held)[..(int)messageEnd], output);
        held.RemoveRange(0, (int)messageEnd);
        return true;
    }

    private static void WriteRedacted(ReadOnlySpan<byte> message, ArrayBufferWriter<byte> output)
    {
        if (FindSecret(message) is not var (start, end, isWholeField))
        {
            output.Write(message);
            return;
        }

        output.Write(message[..start]);
        if (isWholeField)
        {
            output.Write(MaskLength);
        }

        output.Write(Mask);
        output.Write(message[end..]);
    }

    // The byte range of the secret in a whole or cut-off message, or null when it has none.
    private static (int Start, int End, bool IsWholeField)? FindSecret(ReadOnlySpan<byte> message)
    {
        var sequence = ReadTlv(message, 0);
        var messageId = ReadTlv(message, sequence.ValueStart);
        var operation = ReadTlv(message, messageId.End);
        if (!messageId.IsWhole || operation.Tag != BindRequestTag)
        {
            return null;
        }

        var version = ReadTlv(message, operation.ValueStart);
        var name = ReadTlv(message, version.End);
        var authentication = ReadTlv(message, name.End);
        foreach (var field in (ReadOnlySpan<Tlv>)[version, name, authentication])
        {
            if (!field.IsWhole)
            {
                return (field.ValueStart, message.Length, false);
            }
        }

        return authentication.Tag switch
        {
            SimplePasswordTag => SecretOf(authentication),
            SaslCredentialsTag => FindSaslSecret(message, authentication),
            _ => null,
        };
    }

    private static (int Start, int End, bool IsWholeField)? FindSaslSecret(ReadOnlySpan<byte> message, Tlv sasl)
    {
        var mechanism = ReadTlv(message, sasl.ValueStart);
        if (!mechanism.IsWhole)
        {
            return (mechanism.ValueStart, message.Length, false);
        }

        var credentials = ReadTlv(message, mechanism.End);
        if (!Ascii.EqualsIgnoreCase(message[mechanism.ValueStart..mechanism.End], PlainMechanism)
            || credentials.Tag != OctetStringTag)
        {
            return null;
        }

        return credentials.IsWhole ? SecretOf(credentials) : (credentials.ValueStart, message.Length, false);
    }

    // The value of a whole field, unless it is empty (there is nothing to withhold).
    // The range starts after the tag, so the length octets are replaced too (ADR-0182 decision 1).
    private static (int Start, int End, bool IsWholeField)? SecretOf(Tlv field) =>
        field.End > field.ValueStart ? (field.Start + 1, field.End, true) : null;

    // Reads the tag and length at start. IsWhole: the value is held in full. IsMalformed: a length
    // BER's definite form cannot give (indefinite, or more than four octets). A field not yet
    // readable starts and ends at the end of the bytes.
    private static Tlv ReadTlv(ReadOnlySpan<byte> bytes, int start)
    {
        var tag = start < bytes.Length ? bytes[start] : (byte)0;
        if (start + 1 >= bytes.Length)
        {
            return Unreadable(tag, bytes.Length, isMalformed: false);
        }

        var first = bytes[start + 1];
        var lengthOctets = first >= 0x80 ? first - 0x80 : 0;
        if (first == 0x80 || lengthOctets > MaxLengthOctets)
        {
            return Unreadable(tag, bytes.Length, isMalformed: true);
        }

        var valueStart = start + 2 + lengthOctets;
        if (valueStart > bytes.Length)
        {
            return Unreadable(tag, bytes.Length, isMalformed: false);
        }

        long length = first;
        if (lengthOctets > 0)
        {
            length = 0;
            foreach (var octet in bytes[(start + 2)..valueStart])
            {
                length = (length << 8) | octet;
            }
        }

        var declaredEnd = valueStart + length;
        return declaredEnd <= bytes.Length
            ? new Tlv(tag, start, valueStart, (int)declaredEnd, declaredEnd, IsWhole: true, IsMalformed: false)
            : new Tlv(tag, start, valueStart, bytes.Length, declaredEnd, IsWhole: false, IsMalformed: false);
    }

    private static Tlv Unreadable(byte tag, int end, bool isMalformed) =>
        new(tag, end, end, end, end, IsWhole: false, IsMalformed: isMalformed);

    private readonly record struct Tlv(byte Tag, int Start, int ValueStart, int End, long DeclaredEnd, bool IsWhole, bool IsMalformed);
}
