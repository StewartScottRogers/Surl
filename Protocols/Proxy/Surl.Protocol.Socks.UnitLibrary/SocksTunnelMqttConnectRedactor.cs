using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for a SOCKS tunnel whose first byte is <c>0x10</c>, an
/// MQTT <c>CONNECT</c> (ADR-0182, amendment 10). It holds that first packet until it is whole and
/// writes it with the password (MQTT 3.1.1 and MQTT 5) as a length of 4 and <c>****</c>, and with a
/// Remaining Length that counts what is written, so the log never shows the password's length. Every
/// later byte is logged as it arrived.
/// </summary>
/// <remarks>
/// A <c>CONNECT</c> whose flags announce a password that is not the packet's last field, whole, has
/// everything after its flags and keep alive written as <c>****</c>. A packet declaring more than
/// <see cref="MaxHeldBytes"/> is written as one <c>****</c> and its bytes are dropped. A fixed header
/// whose Remaining Length runs past four bytes is not MQTT, so the tunnel is logged as received.
/// </remarks>
internal sealed class SocksTunnelMqttConnectRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a packet may be to be held: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    // The packet type byte and the four bytes a Remaining Length may take (MQTT 3.1.1 section 2.2.3).
    private const int MaxFixedHeaderBytes = 5;

    private readonly List<byte> held = [];
    private int headerLength;
    private int packetLength;
    private int bytesToDrop;
    private bool isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !isPassingThrough)
        {
            HoldNext(received[index++], output);
        }

        var dropped = Math.Min(bytesToDrop, received.Length - index);
        bytesToDrop -= dropped;
        output.Write(received[(index + dropped)..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the origin sends changes what a CONNECT holds.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (headerLength == 0)
        {
            // No field has begun, so nothing held is a password.
            output.Write(CollectionsMarshal.AsSpan(held));
            held.Clear();
        }
        else if (held.Count > 0)
        {
            WriteHeldConnect(output);
        }
    }

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (headerLength == 0 && !TryMeasurePacket(output))
        {
            return;
        }

        if (packetLength > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            bytesToDrop = packetLength - held.Count;
            held.Clear();
            isPassingThrough = true;
        }
        else if (held.Count == packetLength)
        {
            WriteHeldConnect(output);
            isPassingThrough = true;
        }
    }

    // True once the Remaining Length is whole; one that never can be passes the tunnel through.
    private bool TryMeasurePacket(ArrayBufferWriter<byte> output)
    {
        var cursor = 1;
        if (SocksMqttConnectPasswordFinder.TryReadVariableByteInteger(CollectionsMarshal.AsSpan(held), ref cursor, out var remaining))
        {
            headerLength = cursor;
            packetLength = cursor + remaining;
            return true;
        }

        if (held.Count == MaxFixedHeaderBytes)
        {
            output.Write(CollectionsMarshal.AsSpan(held));
            held.Clear();
            isPassingThrough = true;
        }

        return false;
    }

    private void WriteHeldConnect(ArrayBufferWriter<byte> output)
    {
        var packet = CollectionsMarshal.AsSpan(held);
        var body = packet[headerLength..];
        if (SocksMqttConnectPasswordFinder.TryFindSecret(body, out var secretStart, out var isPasswordField))
        {
            var replacement = isPasswordField ? "\0\u0004****"u8 : LineBytesRedactor.SecretMask;
            output.Write(packet[..1]);
            WriteVariableByteInteger(secretStart + replacement.Length, output);
            output.Write(body[..secretStart]);
            output.Write(replacement);
        }
        else
        {
            output.Write(packet);
        }

        held.Clear();
    }

    private static void WriteVariableByteInteger(int value, ArrayBufferWriter<byte> output)
    {
        do
        {
            var next = value & 0x7F;
            value >>= 7;
            output.Write([(byte)(value > 0 ? next | 0x80 : next)]);
        }
        while (value > 0);
    }
}
