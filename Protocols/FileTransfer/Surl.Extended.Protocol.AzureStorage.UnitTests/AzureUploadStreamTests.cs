using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The stream a Put Blob body is copied into.
/// </summary>
[TestClass]
public sealed class AzureUploadStreamTests
{
    [TestMethod]
    public void Members_EverythingButAnAsynchronousWrite_IsNotSupported()
    {
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        using var stream = new AzureUploadStream(null!, md5);

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
    public async Task WriteAsync_ArrayWrite_IsAppendedToTheSession()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        var mapping = harness.Store.MapRequestPath("/a.txt");
        var opening = await harness.Store.OpenUploadAsync(mapping, new ContentUploadOpening(false, true, false), CancellationToken.None);
        await using var session = opening.Session!;
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        await using var stream = new AzureUploadStream(session, md5);

        await stream.WriteAsync(Encoding.UTF8.GetBytes("hello world"), 0, 11, CancellationToken.None);

        Assert.AreEqual(11, session.Length);
        Assert.IsFalse(stream.IsTooLarge);
    }

    [TestMethod]
    public async Task WriteAsync_WritesPastTheUploadLimit_AreTooLargeAndTheRestDropped()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable with { MaxUploadBytes = 4 });
        var mapping = harness.Store.MapRequestPath("/a.txt");
        var opening = await harness.Store.OpenUploadAsync(mapping, new ContentUploadOpening(false, true, false), CancellationToken.None);
        await using var session = opening.Session!;
        using var md5 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        await using var stream = new AzureUploadStream(session, md5);

        await stream.WriteAsync(Encoding.UTF8.GetBytes("hello"), CancellationToken.None);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(" world"), CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        CollectionAssert.AreEqual(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("hello world")), md5.GetHashAndReset());
    }
}
