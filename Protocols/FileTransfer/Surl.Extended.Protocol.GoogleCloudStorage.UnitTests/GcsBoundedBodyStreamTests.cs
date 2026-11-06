using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The bounded memory a multipart completion's body is copied into.
/// </summary>
[TestClass]
public sealed class GcsBoundedBodyStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WithinTheBound_KeepsTheBytes()
    {
        using var stream = new GcsBoundedBodyStream(5);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("hello"), 0, 5, CancellationToken.None);

        Assert.IsFalse(stream.IsTooLarge);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(stream.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_PastTheBound_KeepsNothingMoreAndSaysSo()
    {
        using var stream = new GcsBoundedBodyStream(4);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("hello"), 0, 5, CancellationToken.None);

        Assert.IsTrue(stream.IsTooLarge);
        Assert.AreEqual(0, stream.Length);
    }
}
