using System.Buffers.Binary;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a <see cref="DnsMessage"/> to its wire form (RFC 1035 section 4.1), compressing names
/// (section 4.1.4) in owners, questions and the RDATA of NS, CNAME, SOA, PTR and MX, and nowhere
/// else (RFC 3597 section 4, RFC 2782). A pointer always names the first place a suffix was
/// written, byte for byte with its case, within the first 16384 bytes a pointer can reach.
/// </summary>
public sealed class DnsMessageWriter
{
    private const int MaxPointerOffset = 0x3FFF;

    private readonly List<byte> bytes = [];
    private readonly Dictionary<string, int> suffixOffsets = new(StringComparer.Ordinal);

    private DnsMessageWriter()
    {
    }

    /// <summary>Returns the wire form of <paramref name="message"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A section holds more than 65535 entries, a record's RDATA is longer than 65535 bytes, or a
    /// header field is out of its bits' range.
    /// </exception>
    public static byte[] Write(DnsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var writer = new DnsMessageWriter();
        writer.WriteHeader(message);

        foreach (var question in message.Questions)
        {
            writer.WriteName(question.Name, compress: true);
            writer.WriteUInt16((ushort)question.Type);
            writer.WriteUInt16(question.Class);
        }

        foreach (var record in message.Answers.Concat(message.Authorities).Concat(message.Additionals))
        {
            writer.WriteRecord(record);
        }

        return [.. writer.bytes];
    }

    /// <summary>Writes one byte.</summary>
    internal void WriteUInt8(byte value) => bytes.Add(value);

    /// <summary>Writes a 16-bit number in network order.</summary>
    internal void WriteUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    /// <summary>Writes a 32-bit number in network order.</summary>
    internal void WriteUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    /// <summary>Writes <paramref name="value"/> as it is.</summary>
    internal void WriteBytes(byte[] value) => bytes.AddRange(value);

    /// <summary>
    /// Writes <paramref name="name"/>, ending in a pointer to the longest suffix already written
    /// when <paramref name="compress"/> is set. Every suffix written in full is remembered either
    /// way, so a later compressed name may point into an uncompressed one.
    /// </summary>
    internal void WriteName(DnsName name, bool compress)
    {
        for (var index = 0; index < name.LabelCount; index++)
        {
            var key = name.SuffixKey(index);

            if (compress && suffixOffsets.TryGetValue(key, out var offset))
            {
                WriteUInt16((ushort)(0xC000 | offset));
                return;
            }

            RememberSuffix(key);
            var label = name.LabelAt(index);
            WriteUInt8((byte)label.Length);
            WriteBytes(label);
        }

        WriteUInt8(0);
    }

    private void RememberSuffix(string key)
    {
        if (bytes.Count <= MaxPointerOffset)
        {
            suffixOffsets.TryAdd(key, bytes.Count);
        }
    }

    private void WriteHeader(DnsMessage message)
    {
        WriteUInt16(message.Header.Id);
        WriteUInt16(message.Header.ToFlags());
        WriteCount(message.Questions.Count);
        WriteCount(message.Answers.Count);
        WriteCount(message.Authorities.Count);
        WriteCount(message.Additionals.Count);
    }

    private void WriteCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, ushort.MaxValue, nameof(count));
        WriteUInt16((ushort)count);
    }

    private void WriteRecord(DnsResourceRecord record)
    {
        WriteName(record.Name, compress: true);
        WriteUInt16((ushort)record.Type);
        WriteUInt16(record.Class);
        WriteUInt32(record.TimeToLive);

        var lengthOffset = bytes.Count;
        WriteUInt16(0);
        record.Data.WriteTo(this);

        var length = bytes.Count - lengthOffset - 2;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, ushort.MaxValue, "RDLENGTH");
        bytes[lengthOffset] = (byte)(length >> 8);
        bytes[lengthOffset + 1] = (byte)length;
    }
}
