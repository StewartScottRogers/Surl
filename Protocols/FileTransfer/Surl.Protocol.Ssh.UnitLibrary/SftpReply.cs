using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Builds the packets the server sends in an SFTP version 3 session, each framed with its
/// <c>uint32</c> length (draft-ietf-secsh-filexfer-02, sections 3 to 7; ADR-0054, decisions 5 to 7).
/// </summary>
internal static class SftpReply
{
    /// <summary>How recent a last write must be for <see cref="LongName"/> to show its time rather than its year.</summary>
    public static readonly TimeSpan RecentWriteAge = TimeSpan.FromDays(180);

    private const uint SizePermissionsAndTimes = 0x0000000D;
    private const uint RegularFileMode = 0x81A4; // 0100644
    private const uint DirectoryMode = 0x41ED; // 040755

    private static readonly string[] StatusNames =
        ["OK", "EOF", "NO_SUCH_FILE", "PERMISSION_DENIED", "FAILURE", "BAD_MESSAGE", "NO_CONNECTION", "CONNECTION_LOST", "OP_UNSUPPORTED"];

    /// <summary>
    /// <c>SSH_FXP_VERSION</c> 3 with no extension pairs: <c>00 00 00 05 02 00 00 00 03</c>.
    /// </summary>
    /// <returns>The packet.</returns>
    public static byte[] Version() => Framed(SftpPacketType.Version, 3, _ => { });

    /// <summary>
    /// <c>SSH_FXP_STATUS</c> with <paramref name="code"/>, <paramref name="message"/> and the
    /// language tag <c>en</c>.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="code">The status code.</param>
    /// <param name="message">One of ADR-0054 decision 6's fixed messages; never a path or an exception.</param>
    /// <returns>The packet.</returns>
    public static byte[] Status(uint id, SftpStatusCode code, string message) =>
        Framed(SftpPacketType.Status, id, writer =>
        {
            writer.WriteUInt32((uint)code);
            writer.WriteString(message);
            writer.WriteString("en");
        });

    /// <summary>
    /// The name a note gives a status code: <c>NO_SUCH_FILE</c> for 2.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The name draft-02 gives it, without <c>SSH_FX_</c>.</returns>
    public static string NameOf(SftpStatusCode code) => StatusNames[(int)code];

    /// <summary>
    /// <c>SSH_FXP_HANDLE</c> carrying <paramref name="handle"/> as 4 big-endian bytes.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="handle">The handle's number.</param>
    /// <returns>The packet.</returns>
    public static byte[] Handle(uint id, uint handle) =>
        Framed(SftpPacketType.Handle, id, writer =>
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, handle);
            writer.WriteString(bytes);
        });

    /// <summary>
    /// <c>SSH_FXP_DATA</c> carrying <paramref name="data"/>.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="data">The bytes read.</param>
    /// <returns>The packet.</returns>
    public static byte[] Data(uint id, ReadOnlyMemory<byte> data) =>
        Framed(SftpPacketType.Data, id, writer => writer.WriteString(data.Span));

    /// <summary>
    /// <c>SSH_FXP_ATTRS</c> carrying an entry's attributes (decision 7).
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="status">The entry's status.</param>
    /// <returns>The packet.</returns>
    public static byte[] Attributes(uint id, ContentEntryStatus status) =>
        Framed(SftpPacketType.Attributes, id, writer => WriteAttributes(writer, status.Kind, status.Length, status.LastModifiedUtc));

    /// <summary>
    /// <c>SSH_FXP_NAME</c> carrying one name whose file name and long name are both
    /// <paramref name="path"/> and whose attributes are empty: <c>REALPATH</c>'s answer (decision 8).
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="path">The canonical path.</param>
    /// <returns>The packet.</returns>
    public static byte[] RealPath(uint id, string path) =>
        Framed(SftpPacketType.Name, id, writer =>
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            writer.WriteUInt32(1);
            writer.WriteString(bytes);
            writer.WriteString(bytes);
            writer.WriteUInt32(0);
        });

    /// <summary>
    /// One name of a <c>READDIR</c> answer: the file name, the long name and the attributes.
    /// </summary>
    /// <param name="entry">The listed entry.</param>
    /// <param name="now">The server's clock, which says whether the entry's write is recent.</param>
    /// <returns>The name's bytes, for <see cref="Names"/>.</returns>
    public static byte[] NameEntry(ContentDirectoryEntry entry, DateTimeOffset now)
    {
        var writer = new SshWireWriter();
        writer.WriteString(Encoding.UTF8.GetBytes(entry.Name));
        writer.WriteString(Encoding.UTF8.GetBytes(LongName(entry, now)));
        WriteAttributes(writer, entry.Kind, entry.Length, entry.LastModifiedUtc);

        return writer.ToArray();
    }

    /// <summary>
    /// The bytes <see cref="Names"/> adds to a packet for no names at all: the length, the type,
    /// the id and the count.
    /// </summary>
    public const int NamesOverhead = 13;

    /// <summary>
    /// <c>SSH_FXP_NAME</c> carrying <paramref name="entries"/>, each from <see cref="NameEntry"/>.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="entries">The names.</param>
    /// <returns>The packet.</returns>
    public static byte[] Names(uint id, IReadOnlyList<byte[]> entries) =>
        Framed(SftpPacketType.Name, id, writer =>
        {
            writer.WriteUInt32((uint)entries.Count);
            foreach (var entry in entries)
            {
                writer.WriteBytes(entry);
            }
        });

    /// <summary>
    /// A <c>READDIR</c> long name: ADR-0052 decision 7's <c>LIST</c> line without its line ending,
    /// <c>-rw-r--r-- 1 surl surl &lt;size&gt; &lt;date&gt; &lt;name&gt;</c> for a file and
    /// <c>drwxr-xr-x 1 surl surl 0 &lt;date&gt; &lt;name&gt;</c> for a directory, the size
    /// right-aligned in 12 columns, the date <c>MMM dd HH:mm</c> (day space-padded) for a write
    /// within <see cref="RecentWriteAge"/> before <paramref name="now"/>, else <c>MMM dd  yyyy</c>, in UTC.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="now">The server's clock.</param>
    /// <returns>The long name.</returns>
    public static string LongName(ContentDirectoryEntry entry, DateTimeOffset now)
    {
        var written = entry.LastModifiedUtc.UtcDateTime;
        var age = now - entry.LastModifiedUtc;
        var timeOrYear = age >= TimeSpan.Zero && age <= RecentWriteAge
            ? written.ToString("HH:mm", CultureInfo.InvariantCulture)
            : written.ToString(" yyyy", CultureInfo.InvariantCulture);
        var mode = entry.Kind == ContentEntryKind.Directory ? "drwxr-xr-x" : "-rw-r--r--";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{mode} 1 surl surl {entry.Length ?? 0,12} {written.ToString("MMM", CultureInfo.InvariantCulture)} {written.Day,2} {timeOrYear} {entry.Name}");
    }

    // Decision 7: SIZE, PERMISSIONS and ACMODTIME; no UIDGID, since the store has no owners; the
    // access time repeats the last write, since the store keeps no other.
    private static void WriteAttributes(SshWireWriter writer, ContentEntryKind kind, long? length, DateTimeOffset lastWrite)
    {
        var seconds = (uint)Math.Clamp(lastWrite.ToUnixTimeSeconds(), 0, uint.MaxValue);
        writer.WriteUInt32(SizePermissionsAndTimes);
        writer.WriteUInt64((ulong)(length ?? 0));
        writer.WriteUInt32(kind == ContentEntryKind.Directory ? DirectoryMode : RegularFileMode);
        writer.WriteUInt32(seconds);
        writer.WriteUInt32(seconds);
    }

    private static byte[] Framed(byte type, uint idOrVersion, Action<SshWireWriter> writeFields)
    {
        var body = new SshWireWriter();
        body.WriteByte(type);
        body.WriteUInt32(idOrVersion);
        writeFields(body);
        var packet = new SshWireWriter();
        packet.WriteString(body.ToArray());

        return packet.ToArray();
    }
}
