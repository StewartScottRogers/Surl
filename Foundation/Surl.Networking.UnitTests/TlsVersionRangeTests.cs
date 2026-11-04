using System.Security.Authentication;

namespace Surl.Networking;

[TestClass]
public sealed class TlsVersionRangeTests
{
    [TestMethod]
    public void Default_AcceptsTls12AndTls13()
    {
        var range = TlsVersionRange.Default;

        Assert.AreEqual(SslProtocols.Tls12, range.Lowest);
        Assert.AreEqual(SslProtocols.Tls13, range.Highest);
        Assert.AreEqual(SslProtocols.Tls12 | SslProtocols.Tls13, range.AcceptedProtocols);
    }

    [TestMethod]
    public void Constructor_LowestTls13LikeTlsv13_AcceptsTls13Only()
    {
        var range = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13);

        Assert.AreEqual(SslProtocols.Tls13, range.AcceptedProtocols);
    }

    [TestMethod]
    public void Constructor_HighestTls12LikeTlsMax12_AcceptsTls12Only()
    {
        var range = new TlsVersionRange(SslProtocols.Tls12, SslProtocols.Tls12);

        Assert.AreEqual(SslProtocols.Tls12, range.AcceptedProtocols);
    }

    [TestMethod]
    public void Constructor_LowestTls10_AcceptsEveryVersionUpToTheHighest()
    {
#pragma warning disable SYSLIB0039 // --tlsv1.0 names the old versions on purpose (ADR-0006 section 4).
        var range = new TlsVersionRange(SslProtocols.Tls, SslProtocols.Tls13);

        Assert.AreEqual(SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13, range.AcceptedProtocols);
#pragma warning restore SYSLIB0039
    }

    [TestMethod]
    public void Constructor_LowestAboveHighest_ThrowsArgumentException()
    {
        var failure = Assert.ThrowsExactly<ArgumentException>(() => new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls12));

        Assert.AreEqual("lowest", failure.ParamName);
    }

    [TestMethod]
    [DataRow(SslProtocols.None, SslProtocols.Tls13, "lowest")]
    [DataRow(SslProtocols.Tls12, SslProtocols.Tls12 | SslProtocols.Tls13, "highest")]
    public void Constructor_BoundThatIsNotOneTlsVersion_ThrowsArgumentOutOfRangeException(SslProtocols lowest, SslProtocols highest, string parameterName)
    {
        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TlsVersionRange(lowest, highest));

        Assert.AreEqual(parameterName, failure.ParamName);
    }
}
