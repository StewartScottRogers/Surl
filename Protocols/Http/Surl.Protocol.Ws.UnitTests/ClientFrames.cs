using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Ws;

/// <summary>
/// Client frames built by RFC 6455 section 5.2's layout, masked as a client must mask them
/// (section 5.1), for the frames pinned upstream curl's tool does not send (ADR-0071: it sends
/// only a <c>PONG</c>).
/// </summary>
internal static class ClientFrames
{
    private static readonly byte[] MaskingKey = [0x37, 0xfa, 0x21, 0x3d];

    /// <summary>A masked frame whose first byte is <paramref name="firstByte"/> (FIN, RSV bits and opcode).</summary>
    public static byte[] Masked(byte firstByte, byte[] payload)
    {
        var header = new List<byte> { firstByte };
        if (payload.Length <= 125)
        {
            header.Add((byte)(0x80 | payload.Length));
        }
        else if (payload.Length <= ushort.MaxValue)
        {
            header.Add(0x80 | 126);
            header.AddRange(BigEndian((ushort)payload.Length));
        }
        else
        {
            header.Add(0x80 | 127);
            header.AddRange(BigEndian((ulong)payload.Length));
        }

        header.AddRange(MaskingKey);
        return [.. header, .. payload.Select((value, index) => (byte)(value ^ MaskingKey[index % 4]))];
    }

    /// <summary>A masked frame carrying ASCII <paramref name="payload"/>.</summary>
    public static byte[] Masked(byte firstByte, string payload) => Masked(firstByte, Encoding.Latin1.GetBytes(payload));

    /// <summary>A masked CLOSE carrying <paramref name="code"/> and <paramref name="reason"/>.</summary>
    public static byte[] Close(ushort code, string reason = "") =>
        Masked(0x88, [.. BigEndian(code), .. Encoding.UTF8.GetBytes(reason)]);

    private static byte[] BigEndian(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
}
