using System.Numerics;
using static Surl.Cryptography.Srp.Rfc5054AppendixB;

namespace Surl.Cryptography.Srp;

/// <summary>
/// Pins <see cref="Srp6a" /> to RFC 5054 Appendix B's test vectors (<c>I = "alice"</c>,
/// <c>P = "password123"</c>, the 1024-bit group) byte for byte, and checks <c>PAD</c>, the byte
/// conversions and the <c>A % N = 0</c> rule of section 2.5.4.
/// </summary>
[TestClass]
public sealed class Srp6aTests
{
    private static readonly SrpGroup Group = SrpGroup.Rfc5054Group1024;

    [TestMethod]
    public void ComputeMultiplier_AppendixBGroup_IsAppendixBK()
    {
        var multiplier = Srp6a.ComputeMultiplier(Group);

        Assert.AreEqual(MultiplierHex, Hex(multiplier));
    }

    [TestMethod]
    public void ComputePrivateKey_AppendixBSaltUserAndPassword_IsAppendixBX()
    {
        var privateKey = Srp6a.ComputePrivateKey(Salt, UserName, Password);

        Assert.AreEqual(PrivateKeyHex, Hex(privateKey));
    }

    [TestMethod]
    public void ComputeVerifier_AppendixBSaltUserAndPassword_IsAppendixBV()
    {
        var verifier = Srp6a.ComputeVerifier(Group, Salt, UserName, Password);

        Assert.AreEqual(VerifierHex, Hex(verifier));
    }

    [TestMethod]
    public void ComputeClientPublicValue_AppendixBA_IsAppendixBCapitalA()
    {
        var clientPublicValue = Srp6a.ComputeClientPublicValue(Group, Number(ClientPrivateValueHex));

        Assert.AreEqual(ClientPublicValueHex, Hex(clientPublicValue));
    }

    [TestMethod]
    public void ComputeServerPublicValue_AppendixBVAndB_IsAppendixBCapitalB()
    {
        var serverPublicValue = Srp6a.ComputeServerPublicValue(Group, Number(VerifierHex), Number(ServerPrivateValueHex));

        Assert.AreEqual(ServerPublicValueHex, Hex(serverPublicValue));
    }

    [TestMethod]
    public void ComputeScrambler_AppendixBCapitalAAndB_IsAppendixBU()
    {
        var scrambler = Srp6a.ComputeScrambler(Group, Number(ClientPublicValueHex), Number(ServerPublicValueHex));

        Assert.AreEqual(ScramblerHex, Hex(scrambler));
    }

    [TestMethod]
    public void ComputePremasterSecret_AppendixBValues_IsAppendixBPremasterSecret()
    {
        var premasterSecret = Srp6a.ComputePremasterSecret(
            Group, Number(ClientPublicValueHex), Number(VerifierHex), Number(ScramblerHex), Number(ServerPrivateValueHex));

        Assert.AreEqual(PremasterSecretHex, Hex(premasterSecret));
    }

    [TestMethod]
    public void Pad_ShortValue_IsLeftPaddedToTheLengthOfN()
    {
        var padded = Srp6a.Pad(Group, 2);

        Assert.HasCount(128, padded);
        Assert.AreEqual(2, padded[127]);
        Assert.IsTrue(padded[..127].All(value => value == 0));
    }

    [TestMethod]
    public void Pad_ValueWiderThanN_Throws()
    {
        var wide = BigInteger.One << 1024;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Srp6a.Pad(Group, wide));
    }

    [TestMethod]
    public void Pad_NegativeValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Srp6a.Pad(Group, BigInteger.MinusOne));
    }

    [TestMethod]
    public void ToUnsignedBigEndian_ValueWithHighBitSet_HasNoLeadingZeroByte()
    {
        var bytes = Srp6a.ToUnsignedBigEndian(0x80FF);

        CollectionAssert.AreEqual(new byte[] { 0x80, 0xFF }, bytes);
    }

    [TestMethod]
    public void ToUnsignedBigEndian_NegativeValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Srp6a.ToUnsignedBigEndian(BigInteger.MinusOne));
    }

    [TestMethod]
    public void FromUnsignedBigEndian_LeadingZeroBytes_AreIgnored()
    {
        var value = Srp6a.FromUnsignedBigEndian([0x00, 0x00, 0x80, 0x01]);

        Assert.AreEqual(new BigInteger(0x8001), value);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void IsClientPublicValueAcceptable_MultipleOfN_IsFalse(int multiple)
    {
        var acceptable = Srp6a.IsClientPublicValueAcceptable(Group, Group.Prime * multiple);

        Assert.IsFalse(acceptable);
    }

    [TestMethod]
    public void IsClientPublicValueAcceptable_AppendixBCapitalA_IsTrue()
    {
        var acceptable = Srp6a.IsClientPublicValueAcceptable(Group, Number(ClientPublicValueHex));

        Assert.IsTrue(acceptable);
    }

    [TestMethod]
    public void EveryGroupMethod_NullGroup_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.Pad(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputeVerifier(null!, Salt, UserName, Password));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputeMultiplier(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputeClientPublicValue(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputeServerPublicValue(null!, 1, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputeScrambler(null!, 1, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.IsClientPublicValueAcceptable(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Srp6a.ComputePremasterSecret(null!, 1, 1, 1, 1));
    }
}
