namespace Surl.Tls13;

[TestClass]
public sealed class FinishedTests
{
    [TestMethod]
    public void Write_Rfc8448Section3ServerFinished_WritesItByteForByte()
    {
        var finished = new Finished(Rfc8448.ServerVerifyData);

        var message = finished.Write();

        CollectionAssert.AreEqual(Rfc8448.ServerFinished, message);
    }
}
