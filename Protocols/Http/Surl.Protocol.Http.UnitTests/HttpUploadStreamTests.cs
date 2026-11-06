using System.Security.Cryptography;
using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpUploadStream"/> hashes every byte of a <c>PUT</c> body it is given a hash for,
/// and appends it to the upload session until the session is past its limit (ADR-0132,
/// decision 2).
/// </summary>
[TestClass]
public sealed class HttpUploadStreamTests
{
    private static readonly string Root = InMemoryContentFileSystem.RootPath;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Members_OtherThanAsynchronousWrites_AreThoseOfAWriteOnlyStream()
    {
        using var stream = new HttpUploadStream(null, null);

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
    public async Task WriteAsync_WithNoSession_OnlyHashesTheBytes()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = new HttpUploadStream(null, hash);

        await stream.WriteAsync("abc"u8.ToArray(), 0, 3, TestContext.CancellationToken);

        CollectionAssert.AreEqual(SHA256.HashData("abc"u8), hash.GetHashAndReset());
        Assert.IsFalse(stream.IsTooLarge);
    }

    [TestMethod]
    public async Task WriteAsync_PastTheUploadLimit_IsTooLargeAndWritesNoMore()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 4 });
        var opening = await store.OpenUploadAsync(store.MapRequestPath("/up.bin"), new ContentUploadOpening(false, true, false), TestContext.CancellationToken);
        await using var session = opening.Session!;
        await using var stream = new HttpUploadStream(session, null);

        await stream.WriteAsync("abc"u8.ToArray(), TestContext.CancellationToken);
        await stream.WriteAsync("de"u8.ToArray(), TestContext.CancellationToken);
        await stream.WriteAsync("f"u8.ToArray(), TestContext.CancellationToken);

        Assert.IsTrue(stream.IsTooLarge);
        Assert.AreEqual(ContentUploadResult.TooLarge, await session.CommitAsync(TestContext.CancellationToken));
    }
}
