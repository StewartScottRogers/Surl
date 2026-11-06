namespace Surl.Authentication;

[TestClass]
public sealed class RandomSaslNonceSourceTests
{
    [TestMethod]
    [DataRow(8)]
    [DataRow(16)]
    public void CreateNonce_GivesTheLengthAskedFor_NewEachTime(int length)
    {
        var first = RandomSaslNonceSource.Instance.CreateNonce(length);
        var second = RandomSaslNonceSource.Instance.CreateNonce(length);

        Assert.HasCount(length, first);
        Assert.HasCount(length, second);
        CollectionAssert.AreNotEqual(first, second);
    }
}
