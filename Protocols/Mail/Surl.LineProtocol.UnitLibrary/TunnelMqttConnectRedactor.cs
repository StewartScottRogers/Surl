using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> the SOCKS server and the HTTP proxy share for a tunnel
/// whose first byte is <c>0x10</c>, an MQTT <c>CONNECT</c> (ADR-0182, amendments 10 and 23). It holds
/// that first packet until it is whole and writes it with the password (MQTT 3.1.1 and MQTT 5) as a
/// length of 4 and <c>****</c>, and with a Remaining Length that counts what is written, so the log
/// never shows the password's length. Every later byte is logged as it arrived.
/// </summary>
/// <remarks>
/// A <c>CONNECT</c> whose flags announce a password that is not the packet's last field, whole, has
/// everything after its flags and keep alive written as <c>****</c>. A packet declaring more than
/// <see cref="MaxHeldBytes"/> is written as one <c>****</c> and its bytes are dropped. A fixed header
/// whose Remaining Length runs past four bytes is not MQTT, so the tunnel is logged as received.
/// </remarks>
public sealed class TunnelMqttConnectRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a packet may be to be held: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    // The packet type byte and the four bytes a Remaining Length may take (MQTT 3.1.1 section 2.2.3).
    private const int MaxFixedHeaderBytes = 5;

    private readonly List<byte> _held = [];
    private int _headerLength;
    private int _packetLength;
    private int _bytesToDrop;
    private bool _isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !_isPassingThrough)
        {
            HoldNext(received[index++], output);
        }

        var dropped = Math.Min(_bytesToDrop, received.Length - index);
        _bytesToDrop -= dropped;
        output.Write(received[(index + dropped)..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the far end sends changes what a CONNECT holds.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_headerLength == 0)
        {
            // No field has begun, so nothing held is a password.
            output.Write(CollectionsMarshal.AsSpan(_held));
            _held.Clear();
        }
        else if (_held.Count > 0)
        {
            WriteHeldConnect(output);
        }
    }

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        _held.Add(next);
        if (_headerLength == 0 && !TryMeasurePacket(output))
        {
            return;
        }

        if (_packetLength > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            _bytesToDrop = _packetLength - _held.Count;
            _held.Clear();
            _isPassingThrough = true;
        }
        else if (_held.Count == _packetLength)
        {
            WriteHeldConnect(output);
            _isPassingThrough = true;
        }
    }

    // True once the Remaining Length is whole; one that never can be passes the tunnel through.
    private bool TryMeasurePacket(ArrayBufferWriter<byte> output)
    {
        var cursor = 1;
        if (TunnelMqttConnectPasswordFinder.TryReadVariableByteInteger(CollectionsMarshal.AsSpan(_held), ref cursor, out var remaining))
        {
            _headerLength = cursor;
            _packetLength = cursor + remaining;
            return true;
        }

        if (_held.Count == MaxFixedHeaderBytes)
        {
            output.Write(CollectionsMarshal.AsSpan(_held));
            _held.Clear();
            _isPassingThrough = true;
        }

        return false;
    }

    private void WriteHeldConnect(ArrayBufferWriter<byte> output)
    {
        var packet = CollectionsMarshal.AsSpan(_held);
        var body = packet[_headerLength..];
        if (TunnelMqttConnectPasswordFinder.TryFindSecret(body, out var secretStart, out var isPasswordField))
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

        _held.Clear();
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
