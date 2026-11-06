namespace Surl.Extended.Protocol.AzureStorage;

[TestClass]
public sealed class AzureBoundedBodyStreamTests
{
    [TestMethod]
    public async Task WriteAsync_UpToTheBound_KeepsEveryByte()
    {
        using var stream = new AzureBoundedBodyStream(4);

        await stream.WriteAsync(new byte[] { 1, 2, 9 }, 0, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 3, 4 }.AsMemory(), CancellationToken.None);

        Assert.IsFalse(stream.IsTooLarge);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, stream.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_PastTheBound_KeepsNothingMoreAndSaysSo()
    {
        using var stream = new AzureBoundedBodyStream(4);

        await stream.WriteAsync(new byte[] { 1, 2, 3 }.AsMemory(), CancellationToken.None);
        await stream.WriteAsync(new byte[] { 4, 5 }.AsMemory(), CancellationToken.None);
        await stream.WriteAsync(new byte[] { 6 }.AsMemory(), CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.ToArray());
    }
}
