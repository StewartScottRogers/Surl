using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> the SOCKS server and the HTTP proxy share for a tunnel's
/// bytes from a <c>0x10</c>, an MQTT <c>CONNECT</c>, where its login starts (ADR-0182, amendments 10,
/// 23, 25 and 28). It holds each <c>CONNECT</c> and <c>AUTH</c> packet until it is whole and writes it
/// with the password (MQTT 3.1.1 and 5) and every Authentication Data property value (MQTT 5) as a
/// length of 4 and <c>****</c>, and with a Property Length and a Remaining Length that count what is
/// written, so the log never shows a secret's length. Every other packet is logged as it arrived,
/// only its fixed header held until whole.
/// </summary>
/// <remarks>
/// A <c>CONNECT</c> that announces a password, or is level 5 or higher, whose fields do not lead
/// exactly to its end has everything after its keep alive written as <c>****</c>. An <c>AUTH</c>
/// whose properties do not end exactly where it does is written with <c>****</c> as its body. Such a
/// packet declaring more than <see cref="MaxHeldBytes"/> is written as one <c>****</c> and its bytes
/// are dropped. A fixed header whose Remaining Length runs past four bytes is not MQTT, so the rest
/// of the tunnel is logged as received.
/// </remarks>
public sealed class TunnelMqttLoginRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes a packet may be to be held: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    // The packet type byte and the four bytes a Remaining Length may take (MQTT 3.1.1 section 2.2.3).
    private const int MaxFixedHeaderBytes = 5;

    // The packet type is the first byte's high four bits; MQTT 3.1.1 reserves AUTH's.
    private const int PacketTypeShift = 4;
    private const int ConnectPacketType = 1;
    private const int AuthPacketType = 15;

    private readonly List<byte> _held = [];
    private int _headerLength;
    private int _packetLength;
    private int _bytesToPass;
    private int _bytesToDrop;
    private bool _isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !_isPassingThrough)
        {
            if (_bytesToPass + _bytesToDrop > 0)
            {
                index += PassOrDrop(received[index..], output);
            }
            else
            {
                HoldNext(received[index++], output);
            }
        }

        output.Write(received[index..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the far end sends changes what a CONNECT or an AUTH holds.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_headerLength == 0)
        {
            // No packet's body has begun, so nothing held is a secret.
            output.Write(CollectionsMarshal.AsSpan(_held));
            _held.Clear();
        }
        else
        {
            WriteHeldLoginPacket(output);
        }
    }

    // One of the two counts is zero: a packet is passed through or dropped, never both.
    private int PassOrDrop(ReadOnlySpan<byte> rest, ArrayBufferWriter<byte> output)
    {
        var passed = Math.Min(_bytesToPass, rest.Length);
        output.Write(rest[..passed]);
        _bytesToPass -= passed;

        var dropped = Math.Min(_bytesToDrop, rest.Length - passed);
        _bytesToDrop -= dropped;

        return passed + dropped;
    }

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        _held.Add(next);
        if (_headerLength != 0)
        {
            WriteIfWhole(output);
        }
        else if (TryMeasurePacket(output))
        {
            BeginPacket(output);
        }
    }

    // True once the Remaining Length is whole; one that never can be passes the tunnel through.
    private bool TryMeasurePacket(ArrayBufferWriter<byte> output)
    {
        var cursor = 1;
        if (TunnelMqttFields.TryReadVariableByteInteger(CollectionsMarshal.AsSpan(_held), ref cursor, out var remaining))
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

    // The fixed header is held and nothing of the body yet.
    private void BeginPacket(ArrayBufferWriter<byte> output)
    {
        if (_held[0] >> PacketTypeShift is not (ConnectPacketType or AuthPacketType))
        {
            output.Write(CollectionsMarshal.AsSpan(_held));
            _bytesToPass = _packetLength - _headerLength;
            EndPacket();
        }
        else if (_packetLength > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            _bytesToDrop = _packetLength - _headerLength;
            EndPacket();
        }
        else
        {
            WriteIfWhole(output);
        }
    }

    private void WriteIfWhole(ArrayBufferWriter<byte> output)
    {
        if (_held.Count == _packetLength)
        {
            WriteHeldLoginPacket(output);
        }
    }

    // A CONNECT or an AUTH, whole or cut off by the end of the exchange.
    private void WriteHeldLoginPacket(ArrayBufferWriter<byte> output)
    {
        var packet = CollectionsMarshal.AsSpan(_held);
        var body = packet[_headerLength..];
        var logged = packet[0] >> PacketTypeShift == ConnectPacketType
            ? TunnelMqttLoginBodyMasker.MaskConnect(body)
            : TunnelMqttLoginBodyMasker.MaskAuth(body);
        if (body.SequenceEqual(logged))
        {
            output.Write(packet);
        }
        else
        {
            output.Write(packet[..1]);
            TunnelMqttFields.WriteVariableByteInteger(logged.Length, output);
            output.Write(logged);
        }

        EndPacket();
    }

    private void EndPacket()
    {
        _held.Clear();
        _headerLength = 0;
    }
}
