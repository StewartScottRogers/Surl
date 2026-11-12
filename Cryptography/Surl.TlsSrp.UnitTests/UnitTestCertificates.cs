using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// Keys and a stand-in chain for the signed suites. The chain is bytes the server only forwards
/// (it never parses a certificate), so each test checks them as bytes.
/// </summary>
internal static class UnitTestCertificates
{
    public static readonly byte[][] Chain = [[0x30, 0x03, 0x02, 0x01, 0x01], [0x30, 0x03, 0x02, 0x01, 0x02]];

    // Generated once, then each use gets its own instance: one key object is not safe to share across the parallel tests.
    private static readonly Lazy<RSAParameters> RsaKey = new(() =>
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(true);
    });

    private static readonly Lazy<DSAParameters> DsaKey = new(() =>
    {
        using var dsa = DSA.Create(2048);
        return dsa.ExportParameters(true);
    });

    public static RSA Rsa => RSA.Create(RsaKey.Value);

    /// <summary>A 2048-bit DSA key (so q is 256 bits); the BCL has no DSA on macOS.</summary>
    public static DSA Dsa => DSA.Create(DsaKey.Value);

    public static TlsSrpServerCertificate RsaCertificate() => new(Chain, Rsa);

    public static TlsSrpServerCertificate DsaCertificate() => new(Chain, Dsa);

    /// <summary>The handshake messages inside one record's fragment: type and body of each.</summary>
    public static List<(TlsHandshakeType Type, byte[] Body)> Messages(byte[] fragment)
    {
        var messages = new List<(TlsHandshakeType, byte[])>();
        for (var offset = 0; offset < fragment.Length;)
        {
            var length = (fragment[offset + 1] << 16) | (fragment[offset + 2] << 8) | fragment[offset + 3];
            messages.Add(((TlsHandshakeType)fragment[offset], fragment[(offset + 4)..(offset + 4 + length)]));
            offset += 4 + length;
        }

        return messages;
    }
}
