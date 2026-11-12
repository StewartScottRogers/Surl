using System.Security.Cryptography.X509Certificates;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCertificateDescriptionTests
{
    private static string Prm(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases", "certs", name + ".prm"));

    [TestMethod]
    public void Parse_TheAuthority_IsACriticalCertificateSigningCa()
    {
        var description = UpstreamTestCertificateDescription.Parse(Prm("test-ca"));

        Assert.AreEqual("C=NN, O=Edel curl Arctic Illudium Research Cloud, CN=Northern Nowhere Trust Anchor", description.Subject);
        Assert.IsTrue(description.IsCertificateAuthority);
        Assert.IsTrue(description.KeyUsageIsCritical);
        Assert.AreEqual(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, description.KeyUsage);
        Assert.IsEmpty(description.DnsNames);
        Assert.IsNull(description.RawSubjectAlternativeName);
        Assert.IsEmpty(description.ExtendedKeyUsageOids);
    }

    [TestMethod]
    public void Parse_TheLocalhostCertificate_IsAServerCertificateForLocalhost()
    {
        var description = UpstreamTestCertificateDescription.Parse(Prm("test-localhost"));

        Assert.AreEqual("C=NN, O=Edel curl Arctic Illudium Research Cloud, CN=localhost", description.Subject);
        Assert.IsFalse(description.IsCertificateAuthority);
        Assert.IsFalse(description.KeyUsageIsCritical);
        Assert.AreEqual(
            X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement, description.KeyUsage);
        CollectionAssert.AreEqual(new[] { "localhost" }, description.DnsNames.ToArray());
        CollectionAssert.AreEqual(new[] { "1.3.6.1.5.5.7.3.1" }, description.ExtendedKeyUsageOids.ToArray());
    }

    [TestMethod]
    public void Parse_TheClientCertificates_NameClientAuthentication()
    {
        var both = UpstreamTestCertificateDescription.Parse(Prm("test-client-cert"));
        var clientOnly = UpstreamTestCertificateDescription.Parse(Prm("test-client-eku-only"));

        CollectionAssert.AreEqual(new[] { "1.3.6.1.5.5.7.3.1", "1.3.6.1.5.5.7.3.2" }, both.ExtendedKeyUsageOids.ToArray());
        CollectionAssert.AreEqual(new[] { "1.3.6.1.5.5.7.3.2" }, clientOnly.ExtendedKeyUsageOids.ToArray());
    }

    [TestMethod]
    public void Parse_TheSanOrderVariants_KeepTheFilesOrder()
    {
        var first = UpstreamTestCertificateDescription.Parse(Prm("test-localhost-san-first"));
        var last = UpstreamTestCertificateDescription.Parse(Prm("test-localhost-san-last"));
        var nn = UpstreamTestCertificateDescription.Parse(Prm("test-localhost.nn"));

        CollectionAssert.AreEqual(new[] { "localhost", "localhost1", "localhost2" }, first.DnsNames.ToArray());
        CollectionAssert.AreEqual(new[] { "localhost1", "localhost2", "localhost" }, last.DnsNames.ToArray());
        CollectionAssert.AreEqual(new[] { "localhost.nn" }, nn.DnsNames.ToArray());
        Assert.EndsWith("CN=localhost.nn", first.Subject);
    }

    [TestMethod]
    public void Parse_TheEmbeddedNulCertificate_KeepsItsRawSubjectAlternativeName()
    {
        var description = UpstreamTestCertificateDescription.Parse(Prm("test-localhost0h"));

        CollectionAssert.AreEqual(
            new byte[] { 0x30, 0x0d, 0x82, 0x0b, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x00, 0x68 },
            description.RawSubjectAlternativeName);
        Assert.IsEmpty(description.DnsNames);
    }

    [TestMethod]
    public void Parse_AnUnknownKeyUsage_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCertificateDescription.Parse("[ x509v3 ]\nkeyUsage = bogus\n"));

    [TestMethod]
    public void Parse_AnUnknownExtendedKeyUsage_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCertificateDescription.Parse("[ x509v3 ]\nextendedKeyUsage = bogus\n"));

    [TestMethod]
    public void Parse_NullText_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCertificateDescription.Parse(null!));
}
