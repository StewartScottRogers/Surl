using System.Buffers.Binary;
using System.Text;
using Surl.Hpack;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// An HTTP/2 client's bytes, written by hand from RFC 9113 sections 3.4, 4.1 and 6, and the
/// server's frames read back, so the DNS over HTTPS tests need none of <c>Surl.Http2</c>'s
/// internal codec.
/// </summary>
internal sealed class DnsHttp2TestFrames
{
    public const byte DataType = 0x0;
    public const byte HeadersType = 0x1;
    public const byte RstStreamType = 0x3;
    public const byte SettingsType = 0x4;
    public const byte ContinuationType = 0x9;
    public const byte EndStream = 0x1;
    public const byte EndHeaders = 0x4;

    private readonly HpackEncoder encoder = new();

    /// <summary>
    /// The client's empty <c>SETTINGS</c> (RFC 9113 section 3.4): what follows the preface, which
    /// <see cref="Surl.Http2.Http2ConnectionServer"/>'s caller has already read.
    /// </summary>
    public static byte[] Start => Frame(SettingsType, 0, 0, []);

    public static byte[] Frame(byte type, byte flags, int streamId, byte[] payload)
    {
        var frame = new byte[9 + payload.Length];
        frame[0] = (byte)(payload.Length >> 16);
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)payload.Length;
        frame[3] = type;
        frame[4] = flags;
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(5), streamId);
        payload.CopyTo(frame, 9);
        return frame;
    }

    public static byte[] Data(int streamId, byte[] data, bool endStream) => Frame(DataType, endStream ? EndStream : (byte)0, streamId, data);

    /// <summary>A request's <c>HEADERS</c>, <c>:scheme https</c>, with <paramref name="fields"/> after the pseudo-header fields.</summary>
    public byte[] Request(int streamId, string method, string path, bool endStream, params (string Name, string Value)[] fields)
    {
        HpackHeaderField[] block =
        [
            new(":method", method),
            new(":scheme", "https"),
            new(":authority", "dnsserver.example.net"),
            new(":path", path),
            .. fields.Select(field => new HpackHeaderField(field.Name, field.Value)),
        ];

        return Frame(HeadersType, (byte)(EndHeaders | (endStream ? EndStream : 0)), streamId, encoder.Encode(block));
    }

    /// <summary>What the server wrote on <paramref name="streamId"/>: its header fields, its body, and its reset code.</summary>
    public static (IReadOnlyList<HpackHeaderField> Fields, byte[] Body, bool Ended, uint? ResetCode) Response(byte[] written, int streamId)
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
        var fields = new List<HpackHeaderField>();
        var body = new List<byte>();
        var block = new List<byte>();
        var ended = false;
        uint? resetCode = null;

        for (var offset = 0; offset < written.Length;)
        {
            var length = (written[offset] << 16) | (written[offset + 1] << 8) | written[offset + 2];
            var type = written[offset + 3];
            var flags = written[offset + 4];
            var id = BinaryPrimitives.ReadInt32BigEndian(written.AsSpan(offset + 5)) & 0x7FFFFFFF;
            var payload = written[(offset + 9)..(offset + 9 + length)];
            offset += 9 + length;

            if (type is HeadersType or ContinuationType)
            {
                // Every block is decoded, whatever its stream, to keep the decoder's table in step.
                block.AddRange(payload);
                if ((flags & EndHeaders) != 0)
                {
                    var decoded = decoder.Decode(block.ToArray());
                    Assert.IsNull(decoded.Error);
                    if (id == streamId)
                    {
                        fields.AddRange(decoded.Fields);
                    }

                    block.Clear();
                }
            }

            if (id != streamId)
            {
                continue;
            }

            if (type == DataType)
            {
                body.AddRange(payload);
            }

            if (type == RstStreamType)
            {
                resetCode = BinaryPrimitives.ReadUInt32BigEndian(payload);
            }

            ended |= type is DataType or HeadersType && (flags & EndStream) != 0;
        }

        return (fields, [.. body], ended, resetCode);
    }

    public static string Text(IReadOnlyList<HpackHeaderField> fields) =>
        string.Join("\n", fields.Select(field => $"{field.Name}: {field.Value}"));

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
