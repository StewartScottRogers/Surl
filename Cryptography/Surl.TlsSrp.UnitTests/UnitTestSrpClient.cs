using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// Plays the client of a TLS-SRP handshake from RFC 5054 and RFC 5246, written out here
/// independently of the server's message writers: it builds a ClientHello, computes the server's
/// flight the server should send (with the server drawing <see cref="ServerRandomPattern"/>
/// repeated, as the relay that recorded the fixtures did), and then the client's
/// ClientKeyExchange, <c>ChangeCipherSpec</c> and <c>Finished</c> from the client's side of the SRP
/// math: <c>S = (B - k*g^x)^(a + u*x) % N</c>.
/// </summary>
internal static class UnitTestSrpClient
{
    public const string UserName = "jsmith";
    public const string Password = "abc";

    public static readonly byte[] ServerRandomPattern = Sequence(0x20, 32);
    public static readonly byte[] Salt = Sequence(0x10, 16);
    public static readonly byte[] ClientRandom = Sequence(0x40, 32);
    public static readonly byte[] ClientIv = Sequence(0x90, 16);
    public static readonly BigInteger ClientPrivateValue = Srp6a.FromUnsignedBigEndian(Sequence(0x60, 32));

    public static SrpGroup Group => SrpGroup.Rfc5054Group2048;

    /// <summary>The server's accounts: <c>jsmith</c> with <c>abc</c>, and a zero simulation secret.</summary>
    public static TlsSrpAccounts Accounts() =>
        new([new SrpVerifierEntry(UserName, Salt, Verifier(UserName, Password), Group)], new byte[TlsSrpAccounts.SimulationSecretLength]);

    public static TlsSrpServer Server() => new(Accounts(), new UnitTestRepeatingRandomNumberGenerator(ServerRandomPattern));

    public static byte[] Verifier(string userName, string password) =>
        Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(Group, Salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(password)));

    public static byte[] Sequence(int first, int count) => Enumerable.Range(first, count).Select(value => (byte)value).ToArray();

    public static byte[] Vector8(byte[] data) => [(byte)data.Length, .. data];

    public static byte[] Vector16(byte[] data) => [(byte)(data.Length >> 8), (byte)data.Length, .. data];

    public static byte[] Extension(ushort type, byte[] data) => [(byte)(type >> 8), (byte)type, .. Vector16(data)];

    public static byte[] SrpExtension(string userName) => Extension(TlsExtensionType.Srp, Vector8(Encoding.UTF8.GetBytes(userName)));

    public static byte[] Handshake(TlsHandshakeType type, byte[] body) =>
        [(byte)type, (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length, .. body];

    public static byte[] Record(TlsContentType type, byte[] fragment, ushort version = 0x0303) =>
        [(byte)type, (byte)(version >> 8), (byte)version, (byte)(fragment.Length >> 8), (byte)fragment.Length, .. fragment];

    /// <summary>A ClientHello body: <c>client_version</c>, random, empty session ID, suites, compression and extensions.</summary>
    public static byte[] ClientHelloBody(ushort version, ushort[] suites, byte[] compression, params byte[][] extensions) =>
    [
        (byte)(version >> 8), (byte)version,
        .. ClientRandom,
        0,
        .. Vector16([.. suites.SelectMany(suite => new[] { (byte)(suite >> 8), (byte)suite })]),
        .. Vector8(compression),
        .. Vector16([.. extensions.SelectMany(extension => extension)]),
    ];

    /// <summary>The usual hello: TLS 1.2, the unsigned AES-256 suite, null compression and <c>srp</c> for <paramref name="userName"/>.</summary>
    public static byte[] ClientHello(string userName = UserName, params byte[][] moreExtensions) =>
        Handshake(TlsHandshakeType.ClientHello, ClientHelloBody(0x0303, [0xC020], [0], [SrpExtension(userName), .. moreExtensions]));

    /// <summary>The server's <c>B</c> for <c>jsmith</c>, with <c>b</c> the random pattern.</summary>
    public static BigInteger ServerPublicValue() =>
        Srp6a.ComputeServerPublicValue(Group, Srp6a.FromUnsignedBigEndian(Verifier(UserName, Password)), Srp6a.FromUnsignedBigEndian(ServerRandomPattern));

    /// <summary>
    /// The ServerKeyExchange the server should send for <c>jsmith</c>: <c>N</c>, <c>g</c>, <c>s</c>
    /// and <c>B</c>, each as its unsigned big-endian bytes in a vector (RFC 5054 section 2.8.2).
    /// </summary>
    public static byte[] ExpectedServerKeyExchange() =>
        Handshake(
            TlsHandshakeType.ServerKeyExchange,
            [
                .. Vector16(Group.Prime.ToByteArray(isUnsigned: true, isBigEndian: true)),
                .. Vector16([2]),
                .. Vector8(Salt),
                .. Vector16(ServerPublicValue().ToByteArray(isUnsigned: true, isBigEndian: true)),
            ]);

    /// <summary>The ServerHello the server should send with the extensions given, in order.</summary>
    public static byte[] ExpectedServerHello(ushort suite, params byte[][] extensions)
    {
        byte[] extensionBlock = [.. extensions.SelectMany(extension => extension)];
        return Handshake(
            TlsHandshakeType.ServerHello,
            [
                0x03, 0x03,
                .. ServerRandomPattern,
                0,
                (byte)(suite >> 8), (byte)suite,
                0,
                .. (extensionBlock.Length == 0 ? [] : Vector16(extensionBlock)),
            ]);
    }

    /// <summary>The client's premaster secret for <paramref name="password"/> against <c>jsmith</c>'s <c>B</c>.</summary>
    public static byte[] ClientPremasterSecret(string userName, string password)
    {
        var a = ClientPrivateValue;
        var clientPublic = BigInteger.ModPow(Group.Generator, a, Group.Prime);
        var serverPublic = ServerPublicValue();
        var x = Srp6a.ComputePrivateKey(Salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(password));
        var k = Srp6a.ComputeMultiplier(Group);
        var u = Srp6a.ComputeScrambler(Group, clientPublic, serverPublic);
        var baseValue = (serverPublic - (k * BigInteger.ModPow(Group.Generator, x, Group.Prime))) % Group.Prime;
        baseValue = baseValue.Sign < 0 ? baseValue + Group.Prime : baseValue;
        return Srp6a.ToUnsignedBigEndian(BigInteger.ModPow(baseValue, a + (u * x), Group.Prime));
    }

    /// <summary>The ClientKeyExchange carrying <c>A = g^a % N</c>.</summary>
    public static byte[] ClientKeyExchange() =>
        Handshake(
            TlsHandshakeType.ClientKeyExchange,
            Vector16(Srp6a.ToUnsignedBigEndian(BigInteger.ModPow(Group.Generator, ClientPrivateValue, Group.Prime))));

    public static TlsCbcRecordProtection Protection(TlsBulkCipher cipher, byte[] key, byte[] macKey, bool encryptThenMac) =>
        new(cipher, key, macKey, encryptThenMac, new UnitTestFixedRandomNumberGenerator(ClientIv));

    /// <summary>One record protected as the client writes it.</summary>
    public static byte[] ProtectedRecord(TlsCbcRecordProtection protection, TlsContentType type, byte[] content) =>
        Record(type, protection.Protect(type, 0x0303, content));
}
