using System.Buffers.Binary;
using System.Text;

namespace Surl.Content;

/// <summary>
/// The object metadata index's byte format (ADR-0100, decision 2.3, its format recorded in the
/// task BL-636 and this library's <c>CLAUDE.md</c>). Every integer is big-endian; a string is its
/// UTF-8 length (2 bytes, unsigned) then its UTF-8 bytes, an empty string meaning absent.
/// </summary>
/// <remarks>
/// <see cref="Header"/>; the entry count (4 bytes, unsigned); that many entries in strictly
/// ascending ordinal order of key. An entry is its key (a string: the path relative to the served
/// root, segments joined by <c>/</c>, or a folder object's: the folder's path and a final <c>/</c>),
/// the stamped file length (0 for a folder object) (8 bytes, signed, not negative),
/// the stamped last write time in UTC ticks (8 bytes, signed, a valid <see cref="DateTime"/>),
/// the generation (8 bytes, signed), the metageneration (8 bytes, signed, at least 1; version 3 on), the ETag (a string, not empty), <c>Content-Type</c>,
/// <c>Content-Encoding</c>, <c>Content-Disposition</c>, <c>Cache-Control</c>,
/// <c>Content-Language</c> and <c>Content-MD5</c> (six strings), the user metadata count (2 bytes,
/// unsigned) and that many name and value pairs (two strings each) in strictly ascending ordinal
/// order of name. Then the end of the file. An index starting with
/// <see cref="HeaderWithoutContentMd5"/> instead, written before BL-736, is read too: its entries
/// hold the first five header fields only, and read back with no <c>Content-MD5</c>. One starting
/// <see cref="HeaderWithoutMetageneration"/> (version 2, written before BL-1045) is read too: its entries
/// hold no metageneration and read back as 1.
/// </remarks>
internal static class ObjectMetadataIndexFormat
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The 20 ASCII bytes an index written before the metageneration was kept starts with:
    /// <c>SURL-OBJECT-INDEX-2</c> and a line feed. Its entries hold six header fields and read back
    /// with metageneration 1; it is never written.
    /// </summary>
    public static ReadOnlySpan<byte> HeaderWithoutMetageneration => "SURL-OBJECT-INDEX-2\n"u8;

    /// <summary>
    /// The 20 ASCII bytes every index written now starts with: <c>SURL-OBJECT-INDEX-3</c> and a
    /// line feed. Each entry holds the metageneration (8 bytes, signed, at least 1) after the generation (ADR-0148, BL-1045).
    /// </summary>
    public static ReadOnlySpan<byte> Header => "SURL-OBJECT-INDEX-3\n"u8;

    /// <summary>
    /// The 20 ASCII bytes an index written before <c>Content-MD5</c> was kept starts with:
    /// <c>SURL-OBJECT-INDEX-1</c> and a line feed. Its entries hold five header fields and read
    /// back with no <c>Content-MD5</c>; it is never written.
    /// </summary>
    public static ReadOnlySpan<byte> HeaderWithoutContentMd5 => "SURL-OBJECT-INDEX-1\n"u8;

    /// <summary>
    /// The bytes of an index holding <paramref name="entries"/>.
    /// </summary>
    public static byte[] Encode(IReadOnlyDictionary<string, ObjectMetadataEntry> entries)
    {
        var writer = new IndexWriter();
        writer.Write(Header);
        writer.WriteUInt32((uint)entries.Count);
        foreach (var (key, entry) in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(key);
            writer.WriteInt64(entry.Length);
            writer.WriteInt64(entry.LastWriteTicks);
            writer.WriteInt64(entry.Generation);
            writer.WriteInt64(entry.Metageneration);
            writer.WriteString(entry.ETag);
            foreach (var field in entry.HeaderFields)
            {
                writer.WriteString(field ?? string.Empty);
            }

            writer.WriteUInt16((ushort)entry.UserMetadata.Count);
            foreach (var (name, value) in entry.UserMetadata)
            {
                writer.WriteString(name);
                writer.WriteString(value);
            }
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Decodes an index, refusing one that is malformed or holds more than the bounds allow.
    /// </summary>
    /// <exception cref="InvalidDataException">The index does not parse, or passes a bound; the
    /// message is <see cref="ObjectMetadataIndex.MalformedIndexReason"/>.</exception>
    public static Dictionary<string, ObjectMetadataEntry> Decode(byte[] bytes, int maxEntries, int maxMetadataBytes)
    {
        var reader = new IndexReader(bytes);
        var version = VersionOf(reader.Take(Header.Length));
        var count = reader.ReadUInt32();
        Require(count <= (uint)maxEntries);
        var entries = new Dictionary<string, ObjectMetadataEntry>(StringComparer.Ordinal);
        string? previousKey = null;
        for (var remaining = count; remaining > 0; remaining--)
        {
            var key = reader.ReadString();
            Require((ObjectMetadataIndex.IsValidKey(key) || ObjectMetadataIndex.IsValidFolderObjectKey(key)) && IsAfter(key, previousKey));
            entries.Add(key, DecodeEntry(reader, version, maxMetadataBytes));
            previousKey = key;
        }

        Require(reader.IsAtEnd);
        return entries;
    }

    // The format version a header names: 1 (five header fields), 2 (six) or 3 (six, and a metageneration).
    private static int VersionOf(ReadOnlySpan<byte> header)
    {
        var version = header.SequenceEqual(Header) ? 3 : header.SequenceEqual(HeaderWithoutMetageneration) ? 2 : 1;
        Require(version > 1 || header.SequenceEqual(HeaderWithoutContentMd5));

        return version;
    }

    private static bool AreValid(long length, long lastWriteTicks, long metageneration, string eTag) =>
        length >= 0 && lastWriteTicks >= 0 && lastWriteTicks <= DateTime.MaxValue.Ticks && metageneration >= 1 && ObjectMetadataIndex.IsValidETag(eTag);

    // An entry's header fields: five in an index written before Content-MD5 was kept, which
    // reads back with none, else six; a metageneration only from version 3, else 1.
    private static ObjectMetadataEntry DecodeEntry(IndexReader reader, int version, int maxMetadataBytes)
    {
        var length = reader.ReadInt64();
        var lastWriteTicks = reader.ReadInt64();
        var generation = reader.ReadInt64();
        var metageneration = version >= 3 ? reader.ReadInt64() : 1;
        var eTag = reader.ReadString();
        Require(AreValid(length, lastWriteTicks, metageneration, eTag));
        var fields = new string?[6];
        for (var field = 0; field < (version >= 2 ? 6 : 5); field++)
        {
            fields[field] = reader.ReadOptionalString();
        }

        var userMetadata = DecodeUserMetadata(reader);
        Require(ObjectMetadataEntry.CountMetadataBytes(fields, userMetadata) <= maxMetadataBytes);
        return new ObjectMetadataEntry(length, lastWriteTicks, generation, metageneration, eTag, fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], userMetadata);
    }

    private static List<KeyValuePair<string, string>> DecodeUserMetadata(IndexReader reader)
    {
        var userMetadata = new List<KeyValuePair<string, string>>();
        string? previousName = null;
        for (var remaining = reader.ReadUInt16(); remaining > 0; remaining--)
        {
            var name = reader.ReadString();
            Require(IsAfter(name, previousName));
            userMetadata.Add(new(name, reader.ReadString()));
            previousName = name;
        }

        return userMetadata;
    }

    private static bool IsAfter(string value, string? previous) =>
        previous is null || string.CompareOrdinal(previous, value) < 0;

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidDataException(ObjectMetadataIndex.MalformedIndexReason);
        }
    }

    /// <summary>
    /// Writes an index's big-endian integers and strings.
    /// </summary>
    private sealed class IndexWriter
    {
        private readonly MemoryStream bytes = new();
        private readonly byte[] scratch = new byte[sizeof(long)];

        public void Write(ReadOnlySpan<byte> value) => bytes.Write(value);

        public void WriteUInt16(ushort value)
        {
            BinaryPrimitives.WriteUInt16BigEndian(scratch, value);
            Write(scratch.AsSpan(0, sizeof(ushort)));
        }

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(scratch, value);
            Write(scratch.AsSpan(0, sizeof(uint)));
        }

        public void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(scratch, value);
            Write(scratch);
        }

        public void WriteString(string value)
        {
            var utf8 = Encoding.UTF8.GetBytes(value);
            WriteUInt16((ushort)utf8.Length);
            Write(utf8);
        }

        public byte[] ToArray() => bytes.ToArray();
    }

    /// <summary>
    /// Reads an index's big-endian integers and strings, refusing a read past its end and a
    /// string that is not UTF-8.
    /// </summary>
    private sealed class IndexReader(byte[] bytes)
    {
        private int position;

        public bool IsAtEnd => position == bytes.Length;

        public ReadOnlySpan<byte> Take(int count)
        {
            Require(bytes.Length - position >= count);
            var taken = bytes.AsSpan(position, count);
            position += count;
            return taken;
        }

        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(sizeof(ushort)));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(sizeof(uint)));

        public long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(Take(sizeof(long)));

        public string ReadString()
        {
            var utf8 = Take(ReadUInt16());
            try
            {
                return StrictUtf8.GetString(utf8);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException(ObjectMetadataIndex.MalformedIndexReason);
            }
        }

        public string? ReadOptionalString()
        {
            var value = ReadString();
            return value.Length == 0 ? null : value;
        }
    }
}
