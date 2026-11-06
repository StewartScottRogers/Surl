using System.Text;

namespace Surl.Tls13;

/// <summary>
/// Builds a TLS 1.3 ClientHello for QUIC from typed values, written from RFC 8446 section 4.1.2
/// and RFC 9001 section 8, so a handshake test can offer exactly the parameters it means to. Each
/// default is what a QUIC client offers; null leaves an extension out.
/// </summary>
internal sealed class UnitTestClientHelloBuilder
{
    public ushort[] CipherSuites { get; init; } = [0x1301];

    public ushort[]? SupportedVersions { get; init; } = [0x0304];

    public ushort[]? SupportedGroups { get; init; } = [0x001D];

    public (ushort Group, byte[] KeyExchange)[]? KeyShares { get; init; }

    public ushort[]? SignatureAlgorithms { get; init; } = [0x0804, 0x0403, 0x0503, 0x0603];

    public string[]? ApplicationProtocols { get; init; } = ["h3"];

    public byte[]? QuicTransportParameters { get; init; } = [0x0F, 0x00];

    public byte[] Write()
    {
        var extensions = new List<(ushort Type, byte[] Data)>();
        AddIfSet(extensions, 0x002B, SupportedVersions, versions => Vector(1, UInt16s(versions)));
        AddIfSet(extensions, 0x000A, SupportedGroups, groups => Vector(2, UInt16s(groups)));
        AddIfSet(extensions, 0x000D, SignatureAlgorithms, schemes => Vector(2, UInt16s(schemes)));
        AddIfSet(extensions, 0x0033, KeyShares, shares => Vector(2, [.. shares.SelectMany(KeyShareEntry)]));
        AddIfSet(extensions, 0x0010, ApplicationProtocols, names => Vector(2, [.. names.SelectMany(name => Vector(1, Encoding.ASCII.GetBytes(name)))]));
        AddIfSet(extensions, 0x0039, QuicTransportParameters, parameters => parameters);
        return new UnitTestClientHelloBytes
        {
            CipherSuitesVector = Vector(2, UInt16s(CipherSuites)),
            Extensions = [.. extensions],
        }.Write();
    }

    private static void AddIfSet<T>(List<(ushort Type, byte[] Data)> extensions, ushort type, T? value, Func<T, byte[]> encode)
        where T : class
    {
        if (value is not null)
        {
            extensions.Add((type, encode(value)));
        }
    }

    private static byte[] KeyShareEntry((ushort Group, byte[] KeyExchange) share) =>
        [(byte)(share.Group >> 8), (byte)share.Group, .. Vector(2, share.KeyExchange)];

    private static byte[] UInt16s(IEnumerable<ushort> values) =>
        [.. values.SelectMany(value => new[] { (byte)(value >> 8), (byte)value })];

    private static byte[] Vector(int lengthBytes, byte[] body) =>
        lengthBytes == 1
            ? [(byte)body.Length, .. body]
            : [(byte)(body.Length >> 8), (byte)body.Length, .. body];
}
