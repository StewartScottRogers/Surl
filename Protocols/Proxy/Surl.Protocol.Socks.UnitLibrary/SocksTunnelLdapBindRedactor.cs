using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for a SOCKS tunnel whose first byte is <c>0x30</c>, a
/// BER <c>SEQUENCE</c> as every <c>LDAPMessage</c> is (RFC 4511 section 4.1.1; ADR-0182, amendment
/// 10). It frames each message and writes the authentication of every <c>BindRequest</c> (a simple
/// password, or a whole SASL credentials field, mechanism included) as its tag, a length of 4 and
/// <c>****</c>, an empty one as received; the enclosing lengths stay as received. Every other message is logged as it arrives,
/// never held past its operation's tag, and so is every byte from one that does not frame as an
/// <c>LDAPMessage</c>.
/// </summary>
/// <remarks>
/// A bind that cannot be walked to its authentication, or is cut off by the end of the exchange, has
/// everything after its operation's tag written as <c>****</c>. A bind declaring more than
/// <see cref="MaxHeldBytes"/> is written as one <c>****</c> and its bytes are dropped.
/// </remarks>
internal sealed class SocksTunnelLdapBindRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a bind may be to be held: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const byte SequenceTag = 0x30;
    private const byte BindRequestTag = 0x60;
    private const int MaxMessageIdBytes = 16;

    private readonly List<byte> held = [];
    private long bytesToPass;
    private long bytesToDrop;
    private long bindMessageLength;
    private int bindOperationTagEnd;
    private bool isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !isPassingThrough)
        {
            index += TakeSome(received[index..], output);
        }

        output.Write(received[index..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the origin sends changes what a bind holds.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (bindOperationTagEnd > 0)
        {
            WriteMaskedAfterOperationTag(CollectionsMarshal.AsSpan(held), output);
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
        if (bytesToPass > 0)
        {
            var passed = (int)Math.Min(bytesToPass, rest.Length);
            output.Write(rest[..passed]);
            bytesToPass -= passed;
            return passed;
        }

        if (bytesToDrop > 0)
        {
            var dropped = (int)Math.Min(bytesToDrop, rest.Length);
            bytesToDrop -= dropped;
            return dropped;
        }

        held.Add(rest[0]);
        if (bindOperationTagEnd == 0)
        {
            IdentifyMessage(output);
        }

        WriteBindWhenWhole(output);
        return 1;
    }

    // Decides what the held start of a message is, once enough of it is held to tell.
    private void IdentifyMessage(ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(held);
        var read = SocksBerHeader.TryRead(bytes, 0, out var message);
        if (bytes[0] != SequenceTag || read == SocksBerRead.Unreadable)
        {
            PassEverything(output);
        }
        else if (read == SocksBerRead.Read)
        {
            IdentifyOperation(message, output);
        }
    }

    private void IdentifyOperation(SocksBerHeader message, ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(held);
        var read = SocksBerHeader.TryRead(bytes, message.ValueStart, out var messageId);
        if (read == SocksBerRead.Unreadable)
        {
            PassEverything(output);
        }
        else if (read == SocksBerRead.Read && messageId.End < bytes.Length)
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
            bytesToPass = messageLength - held.Count;
            PassHeld(output);
        }
        else if (messageLength > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            bytesToDrop = messageLength - held.Count;
            ForgetMessage();
        }
        else
        {
            bindMessageLength = messageLength;
            bindOperationTagEnd = held.Count;
        }
    }

    private void WriteBindWhenWhole(ArrayBufferWriter<byte> output)
    {
        // Held bytes grow one at a time, so a bind is whole exactly when the counts meet.
        if (bindOperationTagEnd == 0 || held.Count < bindMessageLength)
        {
            return;
        }

        var message = CollectionsMarshal.AsSpan(held);
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
    private bool TryFindAuthentication(ReadOnlySpan<byte> message, out int start, out SocksBerHeader authentication)
    {
        start = 0;
        if (!TryReadWhole(message, bindOperationTagEnd - 1, isEntered: true, out var next, out authentication))
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
    private static bool TryReadWhole(ReadOnlySpan<byte> message, int start, bool isEntered, out int next, out SocksBerHeader header)
    {
        next = 0;
        if (SocksBerHeader.TryRead(message, start, out header) != SocksBerRead.Read || header.End > message.Length)
        {
            return false;
        }

        next = isEntered ? header.ValueStart : (int)header.End;
        return true;
    }

    private void WriteMaskedAfterOperationTag(ReadOnlySpan<byte> message, ArrayBufferWriter<byte> output)
    {
        output.Write(message[..bindOperationTagEnd]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    private void PassEverything(ArrayBufferWriter<byte> output)
    {
        PassHeld(output);
        isPassingThrough = true;
    }

    private void PassHeld(ArrayBufferWriter<byte> output)
    {
        output.Write(CollectionsMarshal.AsSpan(held));
        ForgetMessage();
    }

    private void ForgetMessage()
    {
        held.Clear();
        bindMessageLength = 0;
        bindOperationTagEnd = 0;
    }
}
