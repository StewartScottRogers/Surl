using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Conformance;

/// <summary>
/// What one of upstream curl's <c>tests/certs/*.prm</c> files describes about a certificate: its
/// subject, subject alternative names, key usage, extended key usage and whether it is a
/// certificate authority. Read by <see cref="Parse"/>; the keys, validity and issuer come from
/// <see cref="UpstreamTestCertificateGenerator"/>.
/// </summary>
/// <param name="Subject">The distinguished name, <c>C=..., O=..., CN=...</c>.</param>
/// <param name="DnsNames">The <c>DNS:</c> alternative names, in the file's order.</param>
/// <param name="RawSubjectAlternativeName">
/// The DER bytes of a <c>subjectAltName = DER:..</c> line, or <see langword="null"/> when the file lists names.
/// </param>
/// <param name="KeyUsage">The key usage flags.</param>
/// <param name="KeyUsageIsCritical">Whether the key usage extension is marked critical.</param>
/// <param name="ExtendedKeyUsageOids">The extended key usage object identifiers.</param>
/// <param name="IsCertificateAuthority">Whether basic constraints say <c>CA:true</c>.</param>
public sealed record UpstreamTestCertificateDescription(
    string Subject,
    IReadOnlyList<string> DnsNames,
    byte[]? RawSubjectAlternativeName,
    X509KeyUsageFlags KeyUsage,
    bool KeyUsageIsCritical,
    IReadOnlyList<string> ExtendedKeyUsageOids,
    bool IsCertificateAuthority)
{
    private static readonly Dictionary<string, X509KeyUsageFlags> KeyUsageNames = new(StringComparer.Ordinal)
    {
        ["digitalSignature"] = X509KeyUsageFlags.DigitalSignature,
        ["keyEncipherment"] = X509KeyUsageFlags.KeyEncipherment,
        ["keyAgreement"] = X509KeyUsageFlags.KeyAgreement,
        ["keyCertSign"] = X509KeyUsageFlags.KeyCertSign,
        ["cRLSign"] = X509KeyUsageFlags.CrlSign,
    };

    private static readonly Dictionary<string, string> ExtendedKeyUsageNames = new(StringComparer.Ordinal)
    {
        ["serverAuth"] = "1.3.6.1.5.5.7.3.1",
        ["clientAuth"] = "1.3.6.1.5.5.7.3.2",
    };

    private static readonly Dictionary<string, string> DistinguishedNameParts = new(StringComparer.Ordinal)
    {
        ["countryName_value"] = "C",
        ["organizationName_value"] = "O",
        ["commonName_value"] = "CN",
    };

    /// <summary>Reads the text of a <c>.prm</c> file.</summary>
    /// <exception cref="FormatException">The file names a key usage or extended key usage this reader does not know.</exception>
    public static UpstreamTestCertificateDescription Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);
        var nameParts = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = string.Empty;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.StartsWith('['))
            {
                section = line.Trim('[', ']', ' ');
            }
            else if (equals > 0)
            {
                var target = section switch { "x509v3" => extensions, "req_DN" => nameParts, _ => null };
                target?[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }
        }

        var (usage, critical) = ReadKeyUsage(extensions.GetValueOrDefault("keyUsage", string.Empty));
        var (dnsNames, rawNames) = ReadSubjectAlternativeName(extensions.GetValueOrDefault("subjectAltName", string.Empty));
        var subject = string.Join(", ", DistinguishedNameParts
            .Where(part => nameParts.ContainsKey(part.Key))
            .Select(part => $"{part.Value}={nameParts[part.Key]}"));
        return new UpstreamTestCertificateDescription(
            subject,
            dnsNames,
            rawNames,
            usage,
            critical,
            Split(extensions.GetValueOrDefault("extendedKeyUsage", string.Empty)).Select(LookUpExtendedKeyUsage).ToList(),
            extensions.GetValueOrDefault("basicConstraints", string.Empty).Contains("CA:true", StringComparison.Ordinal));
    }

    private static string[] Split(string list) => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string LookUpExtendedKeyUsage(string name) =>
        ExtendedKeyUsageNames.TryGetValue(name, out var oid) ? oid : throw new FormatException($"Unknown extendedKeyUsage '{name}'.");

    private static (X509KeyUsageFlags Usage, bool Critical) ReadKeyUsage(string list)
    {
        var usage = X509KeyUsageFlags.None;
        var critical = false;
        foreach (var name in Split(list))
        {
            if (name == "critical")
            {
                critical = true;
            }
            else
            {
                usage |= KeyUsageNames.TryGetValue(name, out var flag) ? flag : throw new FormatException($"Unknown keyUsage '{name}'.");
            }
        }

        return (usage, critical);
    }

    private static (List<string> DnsNames, byte[]? Raw) ReadSubjectAlternativeName(string value) =>
        value.StartsWith("DER:", StringComparison.Ordinal)
            ? ([], value["DER:".Length..].Split(':').Select(hex => byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray())
            : (Split(value).Select(name => name["DNS:".Length..]).ToList(), null);
}
