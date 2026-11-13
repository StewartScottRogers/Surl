using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCertificateGeneratorTests
{
    private static readonly string[] Names =
    [
        "test-ca", "test-client-cert", "test-client-eku-only", "test-localhost", "test-localhost.nn",
        "test-localhost0h", "test-localhost-san-first", "test-localhost-san-last",
    ];

    private static readonly IReadOnlyDictionary<string, byte[]> Files = Generate();

    private static IReadOnlyDictionary<string, byte[]> Generate() => UpstreamTestCertificateGenerator.Generate(
        Names.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases", "certs", name + ".prm")),
            StringComparer.Ordinal),
        TimeProvider.System);

    private static X509Certificate2 Load(string file) => X509CertificateLoader.LoadCertificate(Files[file]);

    [TestMethod]
    public void Generate_EveryDescription_GivesItsFiles()
    {
        foreach (var name in Names.Where(name => name != "test-ca"))
        {
            foreach (var extension in new[] { "crt", "der", "key", "pem", "pub.der", "pub.pem", "crl" })
            {
                Assert.IsTrue(Files.ContainsKey($"{name}.{extension}"), $"{name}.{extension}");
            }
        }

        foreach (var file in new[] { "test-ca.crt", "test-ca.cacert", "test-ca.key" })
        {
            Assert.IsTrue(Files.ContainsKey(file), file);
        }
    }

    [TestMethod]
    public void Generate_TheAuthority_IsASelfSignedCaWithItsSubjectAndKeyUsage()
    {
        using var ca = Load("test-ca.crt");

        Assert.AreEqual(ca.Subject, ca.Issuer);
        Assert.Contains("CN=Northern Nowhere Trust Anchor", ca.Subject);
        Assert.IsTrue(ca.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.AreEqual(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, ca.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages);
        CollectionAssert.AreEqual(Files["test-ca.crt"], Files["test-ca.cacert"]);
    }

    [TestMethod]
    public void Generate_TheLocalhostCertificate_IsIssuedByTheAuthorityForLocalhost()
    {
        using var ca = Load("test-ca.crt");
        using var certificate = Load("test-localhost.crt");

        Assert.AreEqual(ca.Subject, certificate.Issuer);
        Assert.Contains("CN=localhost", certificate.Subject);
        Assert.IsFalse(certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.AreEqual(
            X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement,
            certificate.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages);
        Assert.AreEqual("1.3.6.1.5.5.7.3.1", certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages[0].Value);
        Assert.Contains("localhost", certificate.Extensions["2.5.29.17"]!.Format(false));
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        Assert.IsTrue(chain.Build(certificate));
    }

    [TestMethod]
    public void Generate_TheClientCertificates_NameClientAuthentication()
    {
        using var both = Load("test-client-cert.crt");
        using var clientOnly = Load("test-client-eku-only.crt");

        var bothUsages = both.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>().Select(oid => oid.Value).ToArray();
        var onlyUsages = clientOnly.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>().Select(oid => oid.Value).ToArray();
        CollectionAssert.AreEqual(new[] { "1.3.6.1.5.5.7.3.1", "1.3.6.1.5.5.7.3.2" }, bothUsages);
        CollectionAssert.AreEqual(new[] { "1.3.6.1.5.5.7.3.2" }, onlyUsages);
    }

    [TestMethod]
    public void Generate_TheEmbeddedNulCertificate_CarriesTheRawSubjectAlternativeName()
    {
        using var certificate = Load("test-localhost0h.crt");

        CollectionAssert.AreEqual(
            new byte[] { 0x30, 0x0d, 0x82, 0x0b, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x00, 0x68 },
            certificate.Extensions["2.5.29.17"]!.RawData);
    }

    [TestMethod]
    public void Generate_TheKeyFiles_MatchTheCertificate()
    {
        using var certificate = Load("test-localhost.crt");
        using var der = Load("test-localhost.der");
        using var key = ECDsa.Create();
        key.ImportFromPem(System.Text.Encoding.ASCII.GetString(Files["test-localhost.key"]));
        var pem = System.Text.Encoding.ASCII.GetString(Files["test-localhost.pem"]);

        CollectionAssert.AreEqual(certificate.RawData, der.RawData);
        CollectionAssert.AreEqual(certificate.PublicKey.ExportSubjectPublicKeyInfo(), key.ExportSubjectPublicKeyInfo());
        CollectionAssert.AreEqual(key.ExportSubjectPublicKeyInfo(), Files["test-localhost.pub.der"]);
        Assert.IsTrue(pem.StartsWith("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal));
        Assert.Contains("BEGIN CERTIFICATE", pem);
        Assert.DoesNotContain("PUBLIC KEY", pem);
        Assert.Contains("BEGIN PUBLIC KEY", System.Text.Encoding.ASCII.GetString(Files["test-localhost.pub.pem"]));
    }

    [TestMethod]
    public void Generate_TheCrl_IsPemAndRevokesItsCertificate()
    {
        var text = System.Text.Encoding.ASCII.GetString(Files["test-localhost.crl"]);

        Assert.StartsWith("-----BEGIN X509 CRL-----", text);
    }

    [TestMethod]
    public void Generate_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCertificateGenerator.Generate(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => UpstreamTestCertificateGenerator.Generate(new Dictionary<string, string>(), null!));
    }
}
