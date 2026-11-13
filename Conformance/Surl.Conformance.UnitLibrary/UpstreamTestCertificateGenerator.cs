using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Makes the keys and certificates upstream curl's <c>tests/certs</c> holds, which upstream's
/// <c>genserv.pl</c> makes with <c>openssl</c>, from the imported <c>.prm</c> files with
/// <see cref="CertificateRequest"/> (ADR-0080): <c>test-ca</c> is the authority and every other
/// description is a certificate it issues. Nothing here touches the file system: the files come
/// back as bytes for the caller to write where a case's <c>%CERTDIR/certs/</c> points.
/// </summary>
public static class UpstreamTestCertificateGenerator
{
    /// <summary>The description that is the certificate authority.</summary>
    public const string AuthorityName = "test-ca";

    private static readonly TimeSpan Validity = TimeSpan.FromDays(365);

    /// <summary>
    /// Generates every file. The authority gives <c>test-ca.crt</c>, <c>test-ca.cacert</c> (its
    /// certificate as PEM) and <c>test-ca.key</c>; each other description <c>NAME</c> gives
    /// <c>NAME.crt</c> (certificate, PEM), <c>NAME.der</c> (certificate, DER), <c>NAME.key</c>
    /// (PKCS #8 private key, PEM), <c>NAME.pem</c> (key then certificate), <c>NAME.pub.der</c> and
    /// <c>NAME.pub.pem</c> (the SubjectPublicKeyInfo) and <c>NAME.crl</c> (a list from the authority
    /// that revokes it, PEM).
    /// </summary>
    /// <param name="descriptionTexts">Each <c>.prm</c> file's text by name without extension; must include <c>test-ca</c>.</param>
    /// <param name="time">The clock the validity starts from.</param>
    public static IReadOnlyDictionary<string, byte[]> Generate(IReadOnlyDictionary<string, string> descriptionTexts, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(descriptionTexts);
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        using var authorityKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authorityRequest = Request(descriptionTexts[AuthorityName], authorityKey);
        using var authority = authorityRequest.CreateSelfSigned(now.AddHours(-1), now + Validity);
        AddPem(files, AuthorityName + ".crt", authority.ExportCertificatePem());
        AddPem(files, AuthorityName + ".cacert", authority.ExportCertificatePem());
        AddPem(files, AuthorityName + ".key", authorityKey.ExportPkcs8PrivateKeyPem());

        foreach (var (name, text) in descriptionTexts.Where(entry => entry.Key != AuthorityName))
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Request(text, key).Create(authority, now.AddHours(-1), now + Validity, RandomNumberGenerator.GetBytes(16));
            AddIssued(files, name, certificate, key, authority, now);
        }

        return files;
    }

    private static CertificateRequest Request(string text, ECDsa key)
    {
        var description = UpstreamTestCertificateDescription.Parse(text);
        var request = new CertificateRequest(description.Subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(description.IsCertificateAuthority, false, 0, description.IsCertificateAuthority));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(description.KeyUsage, description.KeyUsageIsCritical));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (description.ExtendedKeyUsageOids.Count > 0)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                ToOids(description.ExtendedKeyUsageOids), false));
        }

        if (description.RawSubjectAlternativeName is { } raw)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.17"), raw, false));
        }
        else if (description.DnsNames.Count > 0)
        {
            var names = new SubjectAlternativeNameBuilder();
            foreach (var dnsName in description.DnsNames)
            {
                names.AddDnsName(dnsName);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        return request;
    }

    private static OidCollection ToOids(IEnumerable<string> oids)
    {
        var collection = new OidCollection();
        foreach (var oid in oids)
        {
            collection.Add(new Oid(oid));
        }

        return collection;
    }

    private static void AddIssued(
        Dictionary<string, byte[]> files, string name, X509Certificate2 certificate, ECDsa key, X509Certificate2 authority, DateTimeOffset now)
    {
        var certificatePem = certificate.ExportCertificatePem();
        var keyPem = key.ExportPkcs8PrivateKeyPem();
        AddPem(files, name + ".crt", certificatePem);
        files[name + ".der"] = certificate.RawData;
        AddPem(files, name + ".key", keyPem);
        AddPem(files, name + ".pem", keyPem + certificatePem);
        files[name + ".pub.der"] = key.ExportSubjectPublicKeyInfo();
        AddPem(files, name + ".pub.pem", key.ExportSubjectPublicKeyInfoPem());

        var revocations = new CertificateRevocationListBuilder();
        revocations.AddEntry(certificate, now);
        var list = revocations.Build(authority, BigInteger.One, now + Validity, HashAlgorithmName.SHA256, null, now);
        AddPem(files, name + ".crl", PemEncoding.WriteString("X509 CRL", list));
    }

    private static void AddPem(Dictionary<string, byte[]> files, string fileName, string pem) =>
        files[fileName] = Encoding.ASCII.GetBytes(pem);
}
