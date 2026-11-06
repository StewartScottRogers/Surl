namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The bounded memory a CompleteMultipartUpload body is read into.
/// </summary>
[TestClass]
public sealed class S3BoundedBodyStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WithinTheBound_KeepsTheBytes()
    {
        using var stream = new S3BoundedBodyStream(4);

        await stream.WriteAsync([1, 2], 0, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 3, 4 }.AsMemory(), CancellationToken.None);

        Assert.IsFalse(stream.IsTooLarge);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, stream.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_PastTheBound_KeepsNothingMoreAndSaysSo()
    {
        using var stream = new S3BoundedBodyStream(4);

        await stream.WriteAsync([1, 2, 3], 0, 3, CancellationToken.None);
        await stream.WriteAsync([4, 5], 0, 2, CancellationToken.None);
        await stream.WriteAsync([6], 0, 1, CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.ToArray());
    }
}
