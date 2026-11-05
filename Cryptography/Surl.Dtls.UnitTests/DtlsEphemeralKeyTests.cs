using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsEphemeralKey"/>: agreement with a client's key on each group, and
/// <c>illegal_parameter</c> for each unusable client point (RFC 8422 section 5.11).
/// </summary>
[TestClass]
public sealed class DtlsEphemeralKeyTests
{
    [TestMethod]
    [DataRow(DtlsNamedGroup.Secp256r1)]
    [DataRow(DtlsNamedGroup.Secp384r1)]
    public void DeriveSharedSecret_NistClientPoint_AgreesWithTheClient(DtlsNamedGroup group)
    {
        using var key = DtlsEphemeralKey.Create(group);
        var curve = group == DtlsNamedGroup.Secp256r1 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384;
        using var client = ECDiffieHellman.Create(curve);
        var q = client.ExportParameters(false).Q;
        var length = q.X!.Length;
        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = curve,
            Q = new ECPoint { X = key.PublicPoint[1..(1 + length)], Y = key.PublicPoint[(1 + length)..] },
        });

        var shared = key.DeriveSharedSecret([0x04, .. q.X, .. q.Y!]);

        CollectionAssert.AreEqual(client.DeriveRawSecretAgreement(server.PublicKey), shared);
    }

    [TestMethod]
    public void DeriveSharedSecret_WrongLengthOrPrefix_IsIllegalParameter()
    {
        using var key = DtlsEphemeralKey.Create(DtlsNamedGroup.Secp256r1);
        byte[] compressed = [0x02, .. new byte[64]];

        AssertIllegal(() => key.DeriveSharedSecret(new byte[10]));
        AssertIllegal(() => key.DeriveSharedSecret(compressed));
    }

    [TestMethod]
    public void DeriveSharedSecret_PointOffTheCurve_IsIllegalParameter()
    {
        using var key = DtlsEphemeralKey.Create(DtlsNamedGroup.Secp256r1);
        byte[] offCurve = [0x04, .. new byte[31], 1, .. new byte[31], 1];

        AssertIllegal(() => key.DeriveSharedSecret(offCurve));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DeriveSharedSecret_CoordinateNotBelowThePrime_IsIllegalParameter(bool inX)
    {
        using var key = DtlsEphemeralKey.Create(DtlsNamedGroup.Secp256r1);
        var big = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        var small = new byte[32];
        byte[] point = inX ? [0x04, .. big, .. small] : [0x04, .. small, .. big];

        AssertIllegal(() => key.DeriveSharedSecret(point));
    }

    [TestMethod]
    public void DeriveSharedSecret_X25519ClientKey_AgreesWithTheClient()
    {
        using var key = DtlsEphemeralKey.Create(DtlsNamedGroup.X25519);
        var clientPrivate = RandomNumberGenerator.GetBytes(32);
        var clientPublic = new byte[32];
        var expected = new byte[32];
        Surl.Cryptography.Curve25519.X25519.ComputePublicKey(clientPrivate, clientPublic);
        Surl.Cryptography.Curve25519.X25519.ScalarMultiply(clientPrivate, key.PublicPoint, expected);

        CollectionAssert.AreEqual(expected, key.DeriveSharedSecret(clientPublic));
    }

    [TestMethod]
    public void DeriveSharedSecret_X25519WrongLengthOrLowOrderPoint_IsIllegalParameter()
    {
        using var key = DtlsEphemeralKey.Create(DtlsNamedGroup.X25519);

        AssertIllegal(() => key.DeriveSharedSecret(new byte[31]));
        AssertIllegal(() => key.DeriveSharedSecret(new byte[32]));
    }

    private static void AssertIllegal(Action action) =>
        Assert.AreEqual(DtlsAlertDescription.IllegalParameter, Assert.ThrowsExactly<DtlsAlertException>(action).Description);
}
