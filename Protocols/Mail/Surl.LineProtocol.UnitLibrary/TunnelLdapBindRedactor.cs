using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> the SOCKS server and the HTTP proxy share for a tunnel
/// whose first byte is <c>0x30</c>, a BER <c>SEQUENCE</c> as every <c>LDAPMessage</c> is (RFC 4511
/// section 4.1.1; ADR-0182, amendments 10 and 23). It frames each message and writes the
/// authentication of every <c>BindRequest</c> (a simple password, or a whole SASL credentials field,
/// mechanism included) as its tag, a length of 4 and <c>****</c>, an empty one as received; the
/// enclosing lengths stay as received. Every other message is logged as it arrives, never held past
/// its operation's tag, and so is every byte from one that does not frame as an
/// <c>LDAPMessage</c>.
/// </summary>
/// <remarks>
/// A bind that cannot be walked to its authentication, or is cut off by the end of the exchange, has
/// everything after its operation's tag written as <c>****</c>. A bind declaring more than
/// <see cref="MaxHeldBytes"/> is written as one <c>****</c> and its bytes are dropped.
/// </remarks>
public sealed class TunnelLdapBindRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a bind may be to be held: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const byte SequenceTag = 0x30;
    private const byte BindRequestTag = 0x60;
    private const int MaxMessageIdBytes = 16;

    private readonly List<byte> _held = [];
    private long _bytesToPass;
    private long _bytesToDrop;
    private long _bindMessageLength;
    private int _bindOperationTagEnd;
    private bool _isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !_isPassingThrough)
        {
            index += TakeSome(received[index..], output);
        }

        output.Write(received[index..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the far end sends changes what a bind holds.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_bindOperationTagEnd > 0)
        {
            WriteMaskedAfterOperationTag(CollectionsMarshal.AsSpan(_held), output);
            ForgetMessage();
        }
        else
        {
            // No operation has begun, so nothing held is a password.
            PassHeld(output);
        }
    }

    // Passes, drops or holds the next bytes and returns how many it took, at least one.
    private int TakeSome(ReadOnlySpan<byte> rest, ArrayBufferWriter<byte> output)
    {
        if (_bytesToPass > 0)
        {
            var passed = (int)Math.Min(_bytesToPass, rest.Length);
            output.Write(rest[..passed]);
            _bytesToPass -= passed;
            return passed;
        }

        if (_bytesToDrop > 0)
        {
            var dropped = (int)Math.Min(_bytesToDrop, rest.Length);
            _bytesToDrop -= dropped;
            return dropped;
        }

        _held.Add(rest[0]);
        if (_bindOperationTagEnd == 0)
        {
            IdentifyMessage(output);
        }

        WriteBindWhenWhole(output);
        return 1;
    }

    // Decides what the held start of a message is, once enough of it is held to tell.
    private void IdentifyMessage(ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(_held);
        var read = TunnelBerHeader.TryRead(bytes, 0, out var message);
        if (bytes[0] != SequenceTag || read == TunnelBerRead.Unreadable)
        {
            PassEverything(output);
        }
        else if (read == TunnelBerRead.Read)
        {
            IdentifyOperation(message, output);
        }
    }

    private void IdentifyOperation(TunnelBerHeader message, ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(_held);
        var read = TunnelBerHeader.TryRead(bytes, message.ValueStart, out var messageId);
        if (read == TunnelBerRead.Unreadable)
        {
            PassEverything(output);
        }
        else if (read == TunnelBerRead.Read && messageId.End < bytes.Length)
        {
            BeginOperation(message.End, bytes[(int)messageId.End], output);
        }
        else if (bytes.Length == message.End)
        {
            // The message ends before it names an operation.
            PassHeld(output);
        }
        else if (bytes.Length - message.ValueStart > MaxMessageIdBytes)
        {
            PassEverything(output);
        }
    }

    private void BeginOperation(long messageLength, byte operationTag, ArrayBufferWriter<byte> output)
    {
        if (operationTag != BindRequestTag)
        {
            _bytesToPass = messageLength - _held.Count;
            PassHeld(output);
        }
        else if (messageLength > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            _bytesToDrop = messageLength - _held.Count;
            ForgetMessage();
        }
        else
        {
            _bindMessageLength = messageLength;
            _bindOperationTagEnd = _held.Count;
        }
    }

    private void WriteBindWhenWhole(ArrayBufferWriter<byte> output)
    {
        // Held bytes grow one at a time, so a bind is whole exactly when the counts meet.
        if (_bindOperationTagEnd == 0 || _held.Count < _bindMessageLength)
        {
            return;
        }

        var message = CollectionsMarshal.AsSpan(_held);
        if (!TryFindAuthentication(message, out var start, out var authentication))
        {
            WriteMaskedAfterOperationTag(message, output);
        }
        else if (authentication.End == authentication.ValueStart)
        {
            // An empty password: there is nothing to withhold.
            output.Write(message);
        }
        else
        {
            output.Write(message[..(start + 1)]);
            output.Write([(byte)LineBytesRedactor.SecretMask.Length]);
            output.Write(LineBytesRedactor.SecretMask);
            output.Write(message[(int)authentication.End..]);
        }

        ForgetMessage();
    }

    // A BindRequest is version, name and authentication (RFC 4511 section 4.2), each whole.
    private bool TryFindAuthentication(ReadOnlySpan<byte> message, out int start, out TunnelBerHeader authentication)
    {
        start = 0;
        if (!TryReadWhole(message, _bindOperationTagEnd - 1, isEntered: true, out var next, out authentication))
        {
            return false;
        }

        for (var field = 0; field < 2; field++)
        {
            if (!TryReadWhole(message, next, isEntered: false, out next, out authentication))
            {
                return false;
            }
        }

        start = next;
        return TryReadWhole(message, start, isEntered: false, out _, out authentication);
    }

    // Reads a value that is wholly inside the message; next is its first inner value or the one after it.
    private static bool TryReadWhole(ReadOnlySpan<byte> message, int start, bool isEntered, out int next, out TunnelBerHeader header)
    {
        next = 0;
        if (TunnelBerHeader.TryRead(message, start, out header) != TunnelBerRead.Read || header.End > message.Length)
        {
            return false;
        }

        next = isEntered ? header.ValueStart : (int)header.End;
        return true;
    }

    private void WriteMaskedAfterOperationTag(ReadOnlySpan<byte> message, ArrayBufferWriter<byte> output)
    {
        output.Write(message[.._bindOperationTagEnd]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    private void PassEverything(ArrayBufferWriter<byte> output)
    {
        PassHeld(output);
        _isPassingThrough = true;
    }

    private void PassHeld(ArrayBufferWriter<byte> output)
    {
        output.Write(CollectionsMarshal.AsSpan(_held));
        ForgetMessage();
    }

    private void ForgetMessage()
    {
        _held.Clear();
        _bindMessageLength = 0;
        _bindOperationTagEnd = 0;
    }
}
