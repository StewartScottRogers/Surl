using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The write-only stream object bytes are hashed and uploaded through.
/// </summary>
[TestClass]
public sealed class GcsHashingStreamTests
{
    [TestMethod]
    public void Members_OfAWriteOnlyStream_SayWhatItSupports()
    {
        using var hashes = new GcsObjectHashes();
        using var stream = new GcsHashingStream(hashes, null);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task WriteAsync_PastTheUploadLimit_HashesTheRestAndDropsIt()
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var store = new ContentStore(GcsServerHarness.Root, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 4 });
        var mapping = store.MapRequestPath("/a.txt");
        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(false, true, false), CancellationToken.None);
        await using var session = opening.Session!;
        using var hashes = new GcsObjectHashes();
        await using var stream = new GcsHashingStream(hashes, session);

        await stream.WriteAsync("hello"u8.ToArray(), 0, 5, CancellationToken.None);
        await stream.WriteAsync(" world"u8.ToArray(), CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        Assert.AreEqual("crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==", hashes.Finish().HashField);
    }
}
