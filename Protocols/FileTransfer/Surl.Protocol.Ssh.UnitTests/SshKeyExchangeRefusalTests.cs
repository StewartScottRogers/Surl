using System.Numerics;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The client key exchange messages the server refuses (ADR-0051, decision 9): a public value
/// that is not a point on the curve, a curve25519 key that is not 32 bytes or of low order, or a
/// value not in 1 &lt; e &lt; p - 1 is <c>DISCONNECT</c> 2, and a
/// group exchange request no RFC 3526 group fits is <c>DISCONNECT</c> 3.
/// </summary>
[TestClass]
public sealed class SshKeyExchangeRefusalTests
{
    private static readonly BigInteger P256Prime = BigInteger.Parse(
        "0FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF",
        System.Globalization.NumberStyles.HexNumber,
        System.Globalization.CultureInfo.InvariantCulture);

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> RefusedPoints()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        var offCurveY = (byte[])q.Y!.Clone();
        offCurveY[^1] ^= 1;
        var prime = Coordinate(P256Prime);

        yield return ["Off the curve", Concat([4], q.X!, offCurveY), "The client's nistp256 point is not on the curve."];
        yield return ["x not below p", Concat([4], prime, q.Y!), "The client's nistp256 point is not on the curve."];
        yield return ["y not below p", Concat([4], q.X!, prime), "The client's nistp256 point is not on the curve."];
        yield return ["Compressed", Concat([2], q.X!, q.Y!), "The client's nistp256 point is not an uncompressed point of 65 bytes."];
        yield return ["Too short", Concat([4], q.X!), "The client's nistp256 point is not an uncompressed point of 65 bytes."];
    }

    [TestMethod]
    [DynamicData(nameof(RefusedPoints))]
    public async Task EcdhInitWithAPointNotOnTheCurve_IsAnsweredDisconnect2(string caseName, byte[] point, string note)
    {
        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "ecdh-sha2-nistp256"),
            Packet(Concat([30], Str(point))));

        AssertDisconnected(written, 2, "Protocol error");
        Assert.AreEqual(note, notes[2], caseName);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Empty")]
    [DataRow(31, DisplayName = "One byte short")]
    [DataRow(33, DisplayName = "One byte long")]
    [DataRow(65, DisplayName = "A NIST P-256 point's length")]
    public async Task Curve25519InitWithAKeyNot32BytesLong_IsAnsweredDisconnect2(int length)
    {
        var key = new byte[length];
        key.AsSpan().Fill(9);

        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "curve25519-sha256"),
            Packet(Concat([30], Str(key))));

        AssertDisconnected(written, 2, "Protocol error");
        Assert.AreEqual($"The client's curve25519 public key is {length} bytes, not 32.", notes[2]);
    }

    // Low-order u-coordinates from the list "Which Curve25519 public keys are unsafe?" at
    // https://cr.yp.to/ecdh.html (Bernstein): 0 and 1, the two points of order 8, and p, which
    // reduces to 0. Every clamped scalar is a multiple of 8, so each gives the all-zero shared
    // secret RFC 8731 section 3 says aborts the exchange.
    public static IEnumerable<object[]> LowOrderCurve25519Keys()
    {
        yield return ["u = 0", Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")];
        yield return ["u = 1", Convert.FromHexString("0100000000000000000000000000000000000000000000000000000000000000")];
        yield return ["Order 8, first", Convert.FromHexString("e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b800")];
        yield return ["Order 8, second", Convert.FromHexString("5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f1157")];
        yield return ["u = p", Convert.FromHexString("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")];
    }

    [TestMethod]
    [DynamicData(nameof(LowOrderCurve25519Keys))]
    public async Task Curve25519InitWithALowOrderKey_IsAnsweredDisconnect2(string caseName, byte[] key)
    {
        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "curve25519-sha256"),
            Packet(Concat([30], Str(key))));

        AssertDisconnected(written, 2, "Protocol error");
        Assert.HasCount(2, ServerPackets(written[ServerLine.Length..]), caseName);
        Assert.AreEqual("The client's curve25519 public key is of low order: the shared secret is all zeros.", notes[2], caseName);
    }

    public static IEnumerable<object[]> RefusedPublicValues()
    {
        var prime = SshModpGroup.Group14.Prime;

        yield return ["Zero", BigInteger.Zero];
        yield return ["One", BigInteger.One];
        yield return ["Negative", BigInteger.MinusOne];
        yield return ["p - 1", prime - 1];
        yield return ["p", prime];
    }

    [TestMethod]
    [DynamicData(nameof(RefusedPublicValues))]
    public async Task KexDhInitOutsideOneToPMinusOne_IsAnsweredDisconnect2(string caseName, BigInteger e)
    {
        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "diffie-hellman-group14-sha256"),
            Packet(Concat([30], Mpint(e))));

        AssertDisconnected(written, 2, "Protocol error");
        Assert.AreEqual("The client's Diffie-Hellman public value e is not in 1 < e < p - 1 of the 2048-bit group.", notes[2], caseName);
    }

    [TestMethod]
    public async Task GroupExchangeInitOutsideOneToPMinusOne_IsAnsweredDisconnect2AfterTheGroup()
    {
        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "diffie-hellman-group-exchange-sha256"),
            Packet(Concat([34], UInt32(2048), UInt32(2048), UInt32(2048))),
            Packet(Concat([32], Mpint(BigInteger.One))));

        var group = Concat([31], Mpint(SshModpGroup.Group14.Prime), Mpint(2));
        CollectionAssert.AreEqual(group, ServerPackets(written[ServerLine.Length..])[1]);
        AssertDisconnected(written, 2, "Protocol error");
        Assert.AreEqual("The client's Diffie-Hellman public value e is not in 1 < e < p - 1 of the 2048-bit group.", notes[2]);
    }

    [TestMethod]
    [DataRow(8193u, 8193u, 9000u, DisplayName = "Above 8192 bits")]
    [DataRow(1024u, 1024u, 2047u, DisplayName = "Below 2048 bits")]
    [DataRow(4097u, 5000u, 6000u, DisplayName = "Between two groups")]
    [DataRow(4096u, 4096u, 3072u, DisplayName = "Minimum above maximum")]
    public async Task GroupExchangeRequestNoGroupFits_IsAnsweredDisconnect3(uint min, uint preferred, uint max)
    {
        var (written, notes) = await ServeAsync(
            ClientKexInitPayload(keyExchange: "diffie-hellman-group-exchange-sha256"),
            Packet(Concat([34], UInt32(min), UInt32(preferred), UInt32(max))));

        AssertDisconnected(written, 3, "No group fits the requested range");
        Assert.AreEqual(
            $"No group of 2048 to 8192 bits fits the client's group exchange request of {min} to {max} bits, preferring {preferred}.",
            notes[2]);
    }

    [TestMethod]
    public async Task MessageOtherThanNewKeysAfterTheReply_IsAnsweredDisconnect2()
    {
        using var client = new SshTestKeyExchangeClient("ecdh-sha2-nistp256", "rsa-sha2-512");

        var (written, notes) = await ServeAsync(client.KexInitPayload, client.MethodPackets(), Packet(5, 0, 0, 0, 0));

        AssertDisconnected(written, 2, "Protocol error");
        Assert.AreEqual("The client sent SSH message 5 during the key exchange.", notes[2]);
    }

    private static byte[] Coordinate(BigInteger value) => value.ToByteArray(isUnsigned: true, isBigEndian: true);

    private static void AssertDisconnected(byte[] written, uint reason, string description)
    {
        var disconnect = ServerDisconnectPacket(reason, description);
        CollectionAssert.AreEqual(disconnect, written[^disconnect.Length..]);
    }

    private async Task<(byte[] Written, IReadOnlyList<string> Notes)> ServeAsync(byte[] clientKexInit, params byte[][] packets)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Concat([Ascii(ClientLine), Packet(clientKexInit), .. packets])]);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        return (connection.WrittenBytes, log.Notes);
    }
}
