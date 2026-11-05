namespace Surl.Core;

[TestClass]
public sealed class CryptographicIdentifierRandomTests
{
    [TestMethod]
    public void NextBytes_TwoDraws_GiveDifferentBytes()
    {
        var first = new byte[32];
        var second = new byte[32];

        CryptographicIdentifierRandom.Instance.NextBytes(first);
        CryptographicIdentifierRandom.Instance.NextBytes(second);

        CollectionAssert.AreNotEqual(first, second);
    }
}
