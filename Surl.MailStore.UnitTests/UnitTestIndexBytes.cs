using System.Buffers.Binary;
using System.Text;

namespace Surl.MailStore;

/// <summary>
/// Builds mail store index bytes part by part, as ADR-0050 decision 7 lays them out, so a test
/// can write a well-formed index or one wrong in exactly one part.
/// </summary>
internal static class UnitTestIndexBytes
{
    public static byte[] Header => "SURL-MAIL-INDEX-1\n"u8.ToArray();

    public static byte[] Index(ulong nextFileNumber, uint lastUidValidity, params byte[][] owners) =>
        Join(Header, U64(nextFileNumber), U32(lastUidValidity), U32((uint)owners.Length), Join(owners));

    public static byte[] Owner(string name, params byte[][] mailboxes) => Owner(Encoding.UTF8.GetBytes(name), mailboxes);

    public static byte[] Owner(byte[] name, params byte[][] mailboxes) =>
        Join(Name(name), U32((uint)mailboxes.Length), Join(mailboxes));

    public static byte[] Mailbox(string name, uint uidValidity, uint nextUid, params byte[][] messages) =>
        Mailbox(Encoding.UTF8.GetBytes(name), uidValidity, nextUid, messages);

    public static byte[] Mailbox(byte[] name, uint uidValidity, uint nextUid, params byte[][] messages) =>
        Join(Name(name), U32(uidValidity), U32(nextUid), U32((uint)messages.Length), Join(messages));

    public static byte[] Message(uint uid, byte flags, long unixSeconds, short offsetMinutes, ulong size, ulong fileNumber) =>
        Join(U32(uid), [flags], I64(unixSeconds), I16(offsetMinutes), U64(size), U64(fileNumber));

    public static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    public static byte[] Name(byte[] utf8) => Join(U16((ushort)utf8.Length), utf8);

    public static byte[] U16(ushort value)
    {
        var bytes = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] I16(short value)
    {
        var bytes = new byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] U32(uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] I64(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] U64(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
}
