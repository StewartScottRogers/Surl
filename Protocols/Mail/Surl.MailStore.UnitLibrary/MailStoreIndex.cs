using System.Buffers.Binary;
using System.Text;

namespace Surl.MailStore;

/// <summary>
/// The mail store index's byte format (ADR-0050, decision 7). Every integer is big-endian and
/// unsigned unless marked signed.
/// </summary>
/// <remarks>
/// <see cref="Header"/>; the next message file number (8 bytes); the last <c>UIDVALIDITY</c>
/// given (4 bytes); the owner count (4 bytes) and that many owners in ordinal order of name.
/// An owner is its name's UTF-8 length (2 bytes), the name, its mailbox count (4 bytes) and
/// that many mailboxes in ordinal order of name; an owner with no mailboxes is not written. A
/// mailbox is its name's UTF-8 length (2 bytes), the name (<c>INBOX</c> in capitals), its
/// <c>UIDVALIDITY</c> (4 bytes), its next UID (4 bytes), its message count (4 bytes) and that
/// many messages in ascending UID order. A message is its UID (4 bytes), its flags (1 byte, the
/// <see cref="MailFlags"/> bits), its internal date as Unix seconds (8 bytes, signed) and offset
/// in minutes (2 bytes, signed), its size (8 bytes) and its message file number (8 bytes). Then
/// the end of the file.
/// </remarks>
internal static class MailStoreIndex
{
    private const MailFlags AllFlags =
        MailFlags.Seen | MailFlags.Answered | MailFlags.Flagged | MailFlags.Deleted | MailFlags.Draft;

    private const int MaxOffsetMinutes = 1439;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The 18 ASCII bytes every index starts with: <c>SURL-MAIL-INDEX-1</c> and a line feed.
    /// </summary>
    public static ReadOnlySpan<byte> Header => "SURL-MAIL-INDEX-1\n"u8;

    /// <summary>
    /// The bytes of the index of a store holding <paramref name="owners"/>.
    /// </summary>
    public static byte[] Encode(ulong nextFileNumber, uint lastUidValidity, IEnumerable<OwnerMailboxes> owners)
    {
        var writer = new IndexWriter();
        writer.Write(Header);
        writer.WriteUInt64(nextFileNumber);
        writer.WriteUInt32(lastUidValidity);
        var written = owners.Where(owner => owner.Mailboxes.Count > 0).OrderBy(owner => owner.Name, StringComparer.Ordinal).ToList();
        writer.WriteUInt32((uint)written.Count);
        foreach (var owner in written)
        {
            writer.WriteName(owner.Name);
            writer.WriteUInt32((uint)owner.Mailboxes.Count);
            foreach (var mailbox in owner.Mailboxes.Values.OrderBy(mailbox => mailbox.Name, StringComparer.Ordinal))
            {
                EncodeMailbox(writer, mailbox);
            }
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Decodes an index, refusing one that is malformed or holds more than the bounds allow.
    /// </summary>
    /// <exception cref="InvalidDataException">The index does not parse, or passes a bound; the
    /// message is <see cref="MailStoreFiles.MalformedIndexReason"/>.</exception>
    public static MailStoreIndexContents Decode(byte[] bytes, int maxMessages, long maxTotalMessageBytes, int maxMailboxes)
    {
        var reader = new IndexReader(bytes);
        Require(reader.Take(Header.Length).SequenceEqual(Header));
        var contents = new MailStoreIndexContents(reader.ReadUInt64(), reader.ReadUInt32());
        var ownerNames = new HashSet<string>(StringComparer.Ordinal);
        for (var remaining = reader.ReadUInt32(); remaining > 0; remaining--)
        {
            var owner = DecodeOwner(reader, contents);
            Require(ownerNames.Add(owner.Name));
            contents.Owners.Add(owner);
        }

        Require(reader.IsAtEnd);
        Require(contents.MessageCount <= maxMessages
            && contents.TotalMessageBytes <= maxTotalMessageBytes
            && contents.MailboxCount <= maxMailboxes);
        return contents;
    }

    private static void EncodeMailbox(IndexWriter writer, StoredMailbox mailbox)
    {
        writer.WriteName(mailbox.Name);
        writer.WriteUInt32(mailbox.UidValidity);
        writer.WriteUInt32(mailbox.NextUid);
        writer.WriteUInt32((uint)mailbox.Messages.Count);
        foreach (var message in mailbox.Messages.Values)
        {
            writer.WriteUInt32(message.Uid);
            writer.Write([(byte)message.Flags]);
            writer.WriteInt64(message.InternalDate.ToUnixTimeSeconds());
            writer.WriteInt16((short)message.InternalDate.Offset.TotalMinutes);
            writer.WriteUInt64((ulong)message.Body.Length);
            writer.WriteUInt64(message.Body.FileNumber);
        }
    }

    private static OwnerMailboxes DecodeOwner(IndexReader reader, MailStoreIndexContents contents)
    {
        var owner = new OwnerMailboxes(DecodeName(reader));
        var uidValidities = new HashSet<uint>();
        for (var remaining = reader.ReadUInt32(); remaining > 0; remaining--)
        {
            var mailbox = DecodeMailbox(reader, contents);
            Require(owner.Mailboxes.TryAdd(mailbox.Name, mailbox) && uidValidities.Add(mailbox.UidValidity));
            contents.MailboxCount += mailbox.Name == MailboxName.Inbox ? 0 : 1;
        }

        return owner;
    }

    private static StoredMailbox DecodeMailbox(IndexReader reader, MailStoreIndexContents contents)
    {
        var name = MailboxName.Canonical(DecodeName(reader));
        var uidValidity = reader.ReadUInt32();
        var nextUid = reader.ReadUInt32();
        Require(MailboxName.IsValid(name) && uidValidity != 0 && uidValidity <= contents.LastUidValidity && nextUid != 0);
        var mailbox = new StoredMailbox(name, uidValidity, nextUid);
        uint lastUid = 0;
        for (var remaining = reader.ReadUInt32(); remaining > 0; remaining--)
        {
            var message = DecodeMessage(reader, contents);
            Require(message.Uid > lastUid && message.Uid < nextUid);
            mailbox.Messages.Add(message.Uid, message);
            lastUid = message.Uid;
        }

        return mailbox;
    }

    private static StoredMessage DecodeMessage(IndexReader reader, MailStoreIndexContents contents)
    {
        var uid = reader.ReadUInt32();
        var flags = (MailFlags)reader.Take(1)[0];
        Require((flags & ~AllFlags) == 0);
        var internalDate = DecodeInternalDate(reader.ReadInt64(), reader.ReadInt16());
        var body = DecodeBody(reader.ReadUInt64(), reader.ReadUInt64(), contents);
        body.ReferenceCount++;
        contents.MessageCount++;
        return new StoredMessage(uid, body, internalDate, flags);
    }

    private static DateTimeOffset DecodeInternalDate(long unixSeconds, short offsetMinutes)
    {
        Require(Math.Abs((int)offsetMinutes) <= MaxOffsetMinutes);
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Malformed();
        }
    }

    private static MessageBody DecodeBody(ulong size, ulong fileNumber, MailStoreIndexContents contents)
    {
        Require(fileNumber < contents.NextFileNumber && size <= int.MaxValue);
        if (contents.Bodies.TryGetValue(fileNumber, out var known))
        {
            Require(known.Length == (long)size);
            return known;
        }

        var body = new MessageBody(fileNumber, (long)size, bytes: null);
        contents.Bodies.Add(fileNumber, body);
        contents.TotalMessageBytes += (long)size;
        return body;
    }

    private static string DecodeName(IndexReader reader)
    {
        string name;
        try
        {
            name = StrictUtf8.GetString(reader.Take(reader.ReadUInt16()));
        }
        catch (DecoderFallbackException)
        {
            throw Malformed();
        }

        Require(!name.Any(MailboxName.IsControl));
        return name;
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw Malformed();
        }
    }

    private static InvalidDataException Malformed() => new(MailStoreFiles.MalformedIndexReason);

    /// <summary>
    /// Writes an index's big-endian integers and names.
    /// </summary>
    private sealed class IndexWriter
    {
        private readonly MemoryStream bytes = new();
        private readonly byte[] scratch = new byte[sizeof(ulong)];

        public void Write(ReadOnlySpan<byte> value) => bytes.Write(value);

        public void WriteInt16(short value)
        {
            BinaryPrimitives.WriteInt16BigEndian(scratch, value);
            Write(scratch.AsSpan(0, sizeof(short)));
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

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64BigEndian(scratch, value);
            Write(scratch);
        }

        public void WriteName(string name)
        {
            var utf8 = Encoding.UTF8.GetBytes(name);
            BinaryPrimitives.WriteUInt16BigEndian(scratch, (ushort)utf8.Length);
            Write(scratch.AsSpan(0, sizeof(ushort)));
            Write(utf8);
        }

        public byte[] ToArray() => bytes.ToArray();
    }

    /// <summary>
    /// Reads an index's big-endian integers, refusing a read past its end.
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

        public short ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(Take(sizeof(short)));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(sizeof(uint)));

        public long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(Take(sizeof(long)));

        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(Take(sizeof(ulong)));
    }
}
