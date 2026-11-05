namespace Surl.Quic;

/// <summary>Computes and checks the Retry Integrity Tag of RFC 9001 Appendix A.4.</summary>
[TestClass]
public sealed class QuicRetryIntegrityTests
{
    [TestMethod]
    public void ComputeTag_Rfc9001A4Retry_GivesAppendixA4Tag()
    {
        var packet = ReadRetry();

        var tag = QuicRetryIntegrity.ComputeTag(Rfc9001AppendixA.ClientDestinationConnectionIdBytes(), packet);

        Assert.AreEqual(Rfc9001AppendixA.RetryIntegrityTag, Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    public void HasValidTag_Rfc9001A4RetryAndItsOriginalConnectionId_IsTrue()
    {
        var packet = ReadRetry();

        var valid = QuicRetryIntegrity.HasValidTag(Rfc9001AppendixA.ClientDestinationConnectionIdBytes(), packet);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void HasValidTag_AnotherOriginalConnectionId_IsFalse()
    {
        var packet = ReadRetry();

        var valid = QuicRetryIntegrity.HasValidTag(Convert.FromHexString("0000000000000000"), packet);

        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void ComputeTag_OriginalConnectionIdOver20Bytes_Throws()
    {
        var packet = ReadRetry();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicRetryIntegrity.ComputeTag(new byte[21], packet));
    }

    [TestMethod]
    public void ComputeTag_NullPacket_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicRetryIntegrity.ComputeTag([], null!));
    }

    private static QuicRetryPacket ReadRetry() =>
        (QuicRetryPacket)QuicPacketReader.Read(Convert.FromHexString(Rfc9001AppendixA.RetryPacket), 0, out _);
}
