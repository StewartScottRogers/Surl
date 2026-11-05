namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CryptographicCoapRandomSource"/>: draws within their ranges.
/// </summary>
[TestClass]
public sealed class CryptographicCoapRandomSourceTests
{
    [TestMethod]
    public void Draws_AreWithinTheirRanges()
    {
        var random = CryptographicCoapRandomSource.Instance;
        var bytes = new byte[64];

        random.Fill(bytes);
        var fraction = random.DrawFraction();
        _ = random.DrawMessageId();

        Assert.IsTrue(fraction is >= 0 and < 1);
        Assert.IsTrue(bytes.Any(value => value != 0));
    }

    [TestMethod]
    [DataRow(0.0, 2000.0)]
    [DataRow(0.5, 2500.0)]
    [DataRow(0.999, 2999.0)]
    public void InitialTimeout_LiesBetweenAckTimeoutAndItsRandomFactor(double fraction, double milliseconds) =>
        Assert.AreEqual(milliseconds, CoapTransmissionParameters.InitialTimeout(fraction).TotalMilliseconds, 0.5);
}
