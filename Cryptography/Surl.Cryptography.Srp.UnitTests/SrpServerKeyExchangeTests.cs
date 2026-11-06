using static Surl.Cryptography.Srp.Rfc5054AppendixB;

namespace Surl.Cryptography.Srp;

/// <summary>
/// Checks <see cref="SrpServerKeyExchange" /> against RFC 5054 Appendix B with the server's secret
/// <c>b</c> fixed, and its refusal of an <c>A</c> with <c>A % N = 0</c> (section 2.5.4).
/// </summary>
[TestClass]
public sealed class SrpServerKeyExchangeTests
{
    [TestMethod]
    public void ServerPublicValue_AppendixBVerifierAndB_IsAppendixBCapitalB()
    {
        var exchange = CreateAppendixBExchange();

        Assert.AreEqual(ServerPublicValueHex, Convert.ToHexString(exchange.ServerPublicValue));
        Assert.AreSame(SrpGroup.Rfc5054Group1024, exchange.Group);
    }

    [TestMethod]
    public void TryComputePremasterSecret_AppendixBCapitalA_IsAppendixBPremasterSecret()
    {
        var exchange = CreateAppendixBExchange();

        var accepted = exchange.TryComputePremasterSecret(Convert.FromHexString(ClientPublicValueHex), out var premasterSecret);

        Assert.IsTrue(accepted);
        Assert.AreEqual(PremasterSecretHex, Convert.ToHexString(premasterSecret));
    }

    [TestMethod]
    public void TryComputePremasterSecret_AEqualToN_IsRefused()
    {
        var exchange = CreateAppendixBExchange();
        var prime = Srp6a.ToUnsignedBigEndian(SrpGroup.Rfc5054Group1024.Prime);

        var accepted = exchange.TryComputePremasterSecret(prime, out var premasterSecret);

        Assert.IsFalse(accepted);
        Assert.IsEmpty(premasterSecret);
    }

    [TestMethod]
    public void TryComputePremasterSecret_AZero_IsRefused()
    {
        var exchange = CreateAppendixBExchange();

        var accepted = exchange.TryComputePremasterSecret(new byte[128], out _);

        Assert.IsFalse(accepted);
    }

    [TestMethod]
    public void TryComputePremasterSecret_ALongerThanN_Throws()
    {
        var exchange = CreateAppendixBExchange();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => exchange.TryComputePremasterSecret(new byte[129], out _));
    }

    [TestMethod]
    public void Constructor_NullGroupOrRandom_Throws()
    {
        using var random = new UnitTestFixedRandomNumberGenerator(new byte[32]);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SrpServerKeyExchange(null!, [1], random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SrpServerKeyExchange(SrpGroup.Rfc5054Group1024, [1], null!));
    }

    private static SrpServerKeyExchange CreateAppendixBExchange()
    {
        using var random = new UnitTestFixedRandomNumberGenerator(Convert.FromHexString(ServerPrivateValueHex));
        return new SrpServerKeyExchange(SrpGroup.Rfc5054Group1024, Convert.FromHexString(VerifierHex), random);
    }
}
