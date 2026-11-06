using System.Security.Cryptography;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class SocketIoRandomIdTests
{
    [TestMethod]
    public void Create_IsTwentyBase64UrlCharactersAndFreshEachTime()
    {
        using var random = RandomNumberGenerator.Create();

        var first = SocketIoRandomId.Create(random);
        var second = SocketIoRandomId.Create(random);

        Assert.AreEqual(20, first.Length);
        Assert.IsTrue(first.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.AreNotEqual(first, second);
        Assert.ThrowsExactly<ArgumentNullException>(() => SocketIoRandomId.Create(null!));
    }
}
