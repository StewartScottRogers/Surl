using System.Buffers.Binary;
using System.Text;
using Surl.Content;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SFTP version 3 packets built by hand from draft-ietf-secsh-filexfer-02 sections 3 to 7, and the
/// standard content store the SFTP tests read: <c>/a.txt</c> (<c>hello world</c> LF, 12 bytes,
/// written 2026-09-27 12:34:56 UTC), <c>/dir/b.txt</c> (<c>bee</c> LF), <c>/.hidden.txt</c> and
/// <c>/.surl/lock</c>.
/// </summary>
internal static class SftpTestPackets
{
    public static readonly DateTimeOffset WrittenAt = new(2026, 9, 27, 12, 34, 56, TimeSpan.Zero);

    public static readonly byte[] Init3 = Framed(1, UInt32(3));

    public static readonly byte[] Version3 = [0, 0, 0, 5, 2, 0, 0, 0, 3];

    public static byte[] Framed(byte type, params byte[][] fields)
    {
        var body = Concat([type], Concat(fields));

        return Concat(UInt32((uint)body.Length), body);
    }

    public static byte[] Request(byte type, uint id, params byte[][] fields) => Framed(type, Concat(UInt32(id), Concat(fields)));

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    public static byte[] Text(string text) => Bytes(Utf8(text));

    public static byte[] Bytes(byte[] bytes) => Concat(UInt32((uint)bytes.Length), bytes);

    public static byte[] UInt64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);

        return bytes;
    }

    public static byte[] Handle(uint number) => Bytes(UInt32(number));

    public static byte[] Open(uint id, string path, uint flags = 1) => Request(3, id, Text(path), UInt32(flags), UInt32(0));

    public static byte[] Read(uint id, uint handle, ulong offset, uint length) => Request(5, id, Handle(handle), UInt64(offset), UInt32(length));

    public static byte[] Status(uint id, uint code, string message) => Framed(101, UInt32(id), UInt32(code), Text(message), Text("en"));

    public static byte[] HandleReply(uint id, uint handle) => Framed(102, UInt32(id), Handle(handle));

    public static byte[] DataReply(uint id, string data) => Framed(103, UInt32(id), Text(data));

    public static byte[] Attributes(ulong size, uint permissions, DateTimeOffset written)
    {
        var seconds = UInt32((uint)written.ToUnixTimeSeconds());

        return Concat(UInt32(0x0D), UInt64(size), UInt32(permissions), seconds, seconds);
    }

    public static byte[] AttributesReply(uint id, ulong size, uint permissions, DateTimeOffset written) =>
        Framed(105, UInt32(id), Attributes(size, permissions, written));

    public static ContentStore Store(ContentExposureOptions options, TimeProvider clock, Action<InMemoryContentFileSystem>? addEntries = null) =>
        Store(options, new InMemoryContentFileSystem(clock), addEntries);

    public static ContentStore Store(ContentExposureOptions options, InMemoryContentFileSystem fileSystem, Action<InMemoryContentFileSystem>? addEntries = null)
    {
        WriteFile(fileSystem, "hello world\n", "a.txt");
        WriteFile(fileSystem, "bee\n", "dir", "b.txt");
        WriteFile(fileSystem, "hidden\n", ".hidden.txt");
        WriteFile(fileSystem, "lock\n", ".surl", "lock");
        fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "a.txt"), WrittenAt);
        fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "dir", "b.txt"), WrittenAt);
        fileSystem.SetLastWriteTimeUtc(Path.Join(InMemoryContentFileSystem.RootPath, "dir"), WrittenAt);
        addEntries?.Invoke(fileSystem);

        return new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, options);
    }

    public static void WriteFile(InMemoryContentFileSystem fileSystem, string text, params string[] segments)
    {
        var directory = Path.Join([InMemoryContentFileSystem.RootPath, .. segments[..^1]]);
        if (fileSystem.GetEntryKind(directory) == ContentEntryKind.None)
        {
            fileSystem.CreateDirectory(directory);
        }

        using var file = fileSystem.CreateFileForAsyncWrite(Path.Join(directory, segments[^1]));
        file.Write(Encoding.UTF8.GetBytes(text));
    }
}
