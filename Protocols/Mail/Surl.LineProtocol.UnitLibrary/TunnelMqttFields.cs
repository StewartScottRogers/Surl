using System.Buffers;
using System.Buffers.Binary;

namespace Surl.LineProtocol;

/// <summary>
/// Reads, skips and writes the fields of an MQTT packet a tunnel's redactor walks (3.1.1 sections
/// 1.5 and 2.2.3; 5.0 section 1.5). This library keeps its own: it may not reference an MQTT server
/// (ADR-0002 decision 3).
/// </summary>
internal static class TunnelMqttFields
{
    private const int MaxVariableByteIntegerBytes = 4;
    private const int ContinuationBit = 0x80;
    private const int ValueBits = 0x7F;

    /// <summary>The field a secret one is logged as: a length of 4 and <c>****</c>.</summary>
    public static ReadOnlySpan<byte> MaskedField => "\0\u0004****"u8;

    /// <summary>Reads a variable byte integer at <paramref name="cursor"/>.</summary>
    /// <param name="bytes">The bytes holding it.</param>
    /// <param name="cursor">Where it starts; moved past it as far as it was read.</param>
    /// <param name="value">Its value.</param>
    /// <returns>False when it is cut short or runs past four bytes.</returns>
    public static bool TryReadVariableByteInteger(ReadOnlySpan<byte> bytes, ref int cursor, out int value)
    {
        value = 0;
        for (var index = 0; index < MaxVariableByteIntegerBytes && cursor < bytes.Length; index++)
        {
            var next = bytes[cursor++];
            value |= (next & ValueBits) << (7 * index);
            if ((next & ContinuationBit) == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes <paramref name="value"/> as a variable byte integer of as few bytes as it takes.</summary>
    /// <param name="value">The value, zero or more.</param>
    /// <param name="output">Where it is appended.</param>
    public static void WriteVariableByteInteger(int value, ArrayBufferWriter<byte> output)
    {
        do
        {
            var next = value & ValueBits;
            value >>= 7;
            output.Write([(byte)(value > 0 ? next | ContinuationBit : next)]);
        }
        while (value > 0);
    }

    /// <summary>Moves <paramref name="cursor"/> past a field: two length bytes and that many bytes.</summary>
    /// <param name="bytes">The bytes holding it.</param>
    /// <param name="cursor">Where it starts; moved only when it is whole.</param>
    /// <returns>False when it is cut short.</returns>
    public static bool TrySkipField(ReadOnlySpan<byte> bytes, ref int cursor) =>
        bytes.Length >= cursor + sizeof(ushort)
        && TrySkip(bytes, ref cursor, sizeof(ushort) + BinaryPrimitives.ReadUInt16BigEndian(bytes[cursor..]));

    /// <summary>Moves <paramref name="cursor"/> past <paramref name="count"/> bytes.</summary>
    /// <param name="bytes">The bytes holding them.</param>
    /// <param name="cursor">Where they start; moved only when they are all there.</param>
    /// <param name="count">How many there are.</param>
    /// <returns>False when fewer are left.</returns>
    public static bool TrySkip(ReadOnlySpan<byte> bytes, ref int cursor, int count)
    {
        if (bytes.Length - cursor < count)
        {
            return false;
        }

        cursor += count;
        return true;
    }
}
