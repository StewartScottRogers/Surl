using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The write-only stream an upload's bytes are copied into.
/// </summary>
[TestClass]
public sealed class OciUploadStreamTests
{
    [TestMethod]
    public async Task Members_OtherThanAsynchronousWrites_AreNotSupported()
    {
        await using var session = await OpenSessionAsync();
        await using var stream = new OciUploadStream(session);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        stream.Flush();
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_AppendsTheBytesToTheSession()
    {
        await using var session = await OpenSessionAsync();
        await using var stream = new OciUploadStream(session);

        await stream.WriteAsync(new byte[] { 1, 2, 3 }, 1, 2, CancellationToken.None);

        Assert.AreEqual(2, session.Length);
        Assert.AreEqual(2, stream.BytesWritten);
        Assert.IsFalse(stream.IsTooLarge);
    }

    [TestMethod]
    public async Task WriteAsync_PastTheLimit_IsTooLargeAndDropsTheRest()
    {
        await using var session = await OpenSessionAsync(maxUploadBytes: 2);
        await using var stream = new OciUploadStream(session);

        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        await stream.WriteAsync(new byte[] { 4 });

        Assert.IsTrue(stream.IsTooLarge);
        Assert.AreEqual(4, stream.BytesWritten);
    }

    private static async Task<OciUploadSession> OpenSessionAsync(long maxUploadBytes = ContentExposureOptions.DefaultMaxUploadBytes)
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(OciRegistryServerHarness.Now));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = maxUploadBytes });
        var opening = await store.OpenUploadAsync(store.MapRequestPath("/staged"), new ContentUploadOpening(false, true, false), CancellationToken.None);

        return new OciUploadSession("id", "app", "/staged", opening.Session!);
    }
}
