namespace Surl.Dtls;

[TestClass]
public sealed class DtlsHandshakeFragmenterTests
{
    [TestMethod]
    public void Fragment_BodyLongerThanTheBudget_SplitsItWithEachFragmentsOffset()
    {
        var body = Enumerable.Range(0, 25).Select(value => (byte)value).ToArray();
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.Certificate, 2, body);

        var fragments = DtlsHandshakeFragmenter.Fragment(message, DtlsHandshakeHeader.Size + 10);

        Assert.HasCount(3, fragments);
        int[] expectedOffsets = [0, 10, 20];
        int[] expectedLengths = [10, 10, 5];
        for (var index = 0; index < fragments.Count; index++)
        {
            DtlsHandshakeHeader.TryRead(fragments[index], out var header);
            Assert.AreEqual(
                new DtlsHandshakeHeader(DtlsHandshakeType.Certificate, 25, 2, expectedOffsets[index], expectedLengths[index]),
                header);
            CollectionAssert.AreEqual(
                body.AsSpan(expectedOffsets[index], expectedLengths[index]).ToArray(),
                fragments[index].AsSpan(DtlsHandshakeHeader.Size).ToArray());
        }
    }

    [TestMethod]
    public void Fragment_BodyThatFits_IsTheUnfragmentedMessage()
    {
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.Finished, 6, new byte[12]);

        var fragments = DtlsHandshakeFragmenter.Fragment(message, DtlsHandshakeFragmenter.MaximumDatagramLength);

        Assert.HasCount(1, fragments);
        CollectionAssert.AreEqual(message.ToUnfragmentedBytes(), fragments[0]);
    }

    [TestMethod]
    public void Fragment_EmptyBody_IsOneHeaderOnlyFragment()
    {
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.ServerHelloDone, 3, []);

        var fragments = DtlsHandshakeFragmenter.Fragment(message, 100);

        Assert.HasCount(1, fragments);
        CollectionAssert.AreEqual(Convert.FromHexString("0E000000" + "0003" + "000000" + "000000"), fragments[0]);
    }

    [TestMethod]
    public void Fragment_BudgetWithNoRoomForABodyByte_Throws()
    {
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.Finished, 0, new byte[12]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DtlsHandshakeFragmenter.Fragment(message, DtlsHandshakeHeader.Size));
    }
}
