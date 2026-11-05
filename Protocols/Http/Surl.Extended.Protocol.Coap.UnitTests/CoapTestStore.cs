using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// A content store over an <see cref="InMemoryContentFileSystem"/> with a fixed clock, so file
/// times and ETags are the same on every run and every platform.
/// </summary>
internal sealed class CoapTestStore
{
    public static readonly DateTimeOffset FileTime = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public CoapTestStore(ContentExposureOptions? exposure = null, long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes)
    {
        FileSystem = new InMemoryContentFileSystem(new ManualTimeProvider(), maxTotalBytes);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, exposure ?? new ContentExposureOptions());
    }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public static ContentExposureOptions Uploads { get; } = new() { AllowUploads = true };

    public static ContentExposureOptions Listings { get; } = new() { ListDirectories = true };

    public string PathOf(string relative) => Path.Join(InMemoryContentFileSystem.RootPath, relative);

    public CoapTestStore WithFile(string relative, string contents)
    {
        using (var stream = FileSystem.CreateFileForAsyncWrite(PathOf(relative)))
        {
            stream.Write(Encoding.UTF8.GetBytes(contents));
        }

        FileSystem.SetLastWriteTimeUtc(PathOf(relative), FileTime);
        return this;
    }

    public CoapTestStore WithDirectory(string relative)
    {
        FileSystem.CreateDirectory(PathOf(relative));
        return this;
    }

    public bool HasFile(string relative) => FileSystem.GetEntryKind(PathOf(relative)) == ContentEntryKind.File;

    public string Contents(string relative)
    {
        using var stream = FileSystem.OpenFileForAsyncRead(PathOf(relative));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>The ETag ADR-0097 decision 1 defines for a file of <paramref name="length"/> bytes written at <see cref="FileTime"/>.</summary>
    public static byte[] EntityTagOf(long length)
    {
        var input = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(input, length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(input.AsSpan(8), FileTime.UtcTicks);
        return System.Security.Cryptography.SHA256.HashData(input)[..8];
    }
}
