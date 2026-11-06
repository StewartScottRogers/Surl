using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using Surl.Cryptography.Curve25519;

namespace Surl.Dtls;

/// <summary>
/// The server's ephemeral ECDHE key for one handshake (RFC 8422 sections 5.4 and 5.10): its public
/// point for <c>ServerKeyExchange</c>, and the shared secret with the client's point. A client
/// point that is malformed, off its curve, or (for x25519) gives an all-zero secret is
/// <c>illegal_parameter</c> (RFC 8422 section 5.11, ADR-0087 decision 10).
/// </summary>
internal abstract class DtlsEphemeralKey : IDisposable
{
    /// <summary>The public point sent in <c>ServerKeyExchange</c>.</summary>
    public abstract byte[] PublicPoint { get; }

    /// <summary>Makes a fresh key on <paramref name="group"/>.</summary>
    /// <param name="group">The group negotiated.</param>
    /// <returns>The key.</returns>
    public static DtlsEphemeralKey Create(DtlsNamedGroup group) =>
        group == DtlsNamedGroup.X25519 ? new X25519Key() : new NistKey(group);

    /// <summary>The ECDH shared secret, the premaster secret (RFC 8422 section 5.10).</summary>
    /// <param name="clientPoint">The client's <c>ClientKeyExchange</c> point.</param>
    /// <returns>The shared secret.</returns>
    /// <exception cref="DtlsAlertException"><c>illegal_parameter</c>: the point is unusable.</exception>
    public abstract byte[] DeriveSharedSecret(ReadOnlySpan<byte> clientPoint);

    /// <inheritdoc/>
    public abstract void Dispose();

    private static DtlsAlertException IllegalPoint(string message) => new(DtlsAlertDescription.IllegalParameter, message);

    private sealed class X25519Key : DtlsEphemeralKey
    {
        private readonly byte[] privateKey = RandomNumberGenerator.GetBytes(X25519.KeySize);
        private readonly byte[] publicPoint = new byte[X25519.KeySize];

        public X25519Key() => X25519.ComputePublicKey(privateKey, publicPoint);

        public override byte[] PublicPoint => publicPoint;

        public override byte[] DeriveSharedSecret(ReadOnlySpan<byte> clientPoint)
        {
            if (clientPoint.Length != X25519.KeySize)
            {
                throw IllegalPoint($"An x25519 point is {X25519.KeySize} bytes, not {clientPoint.Length}.");
            }

            var sharedSecret = new byte[X25519.KeySize];
            X25519.ScalarMultiply(privateKey, clientPoint, sharedSecret);
            if (sharedSecret.AsSpan().IndexOfAnyExcept((byte)0) < 0)
            {
                throw IllegalPoint("The x25519 point is of low order: the shared secret is all zeros.");
            }

            return sharedSecret;
        }

        public override void Dispose() => CryptographicOperations.ZeroMemory(privateKey);
    }

    private sealed class NistKey : DtlsEphemeralKey
    {
        private static readonly BigInteger P256Prime = Hex("ffffffff00000001000000000000000000000000ffffffffffffffffffffffff");
        private static readonly BigInteger P256B = Hex("5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b");
        private static readonly BigInteger P384Prime = Hex(
            "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffff0000000000000000ffffffff");
        private static readonly BigInteger P384B = Hex(
            "b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef");

        private readonly ECCurve curve;
        private readonly int coordinateLength;
        private readonly BigInteger prime;
        private readonly BigInteger b;
        private readonly ECDiffieHellman key;

        public NistKey(DtlsNamedGroup group)
        {
            (curve, coordinateLength, prime, b) = group == DtlsNamedGroup.Secp256r1
                ? (ECCurve.NamedCurves.nistP256, 32, P256Prime, P256B)
                : (ECCurve.NamedCurves.nistP384, 48, P384Prime, P384B);
            key = ECDiffieHellman.Create(curve);
            var point = key.ExportParameters(includePrivateParameters: false).Q;
            PublicPoint = [0x04, .. point.X!, .. point.Y!];
        }

        public override byte[] PublicPoint { get; }

        public override byte[] DeriveSharedSecret(ReadOnlySpan<byte> clientPoint)
        {
            if (clientPoint.Length != 1 + (2 * coordinateLength) || clientPoint[0] != 0x04)
            {
                throw IllegalPoint($"The client's point is not an uncompressed point of {1 + (2 * coordinateLength)} bytes.");
            }

            var x = clientPoint.Slice(1, coordinateLength);
            var y = clientPoint.Slice(1 + coordinateLength, coordinateLength);
            if (!IsOnCurve(x, y))
            {
                throw IllegalPoint("The client's point is not on its curve.");
            }

            using var peer = ECDiffieHellman.Create(new ECParameters
            {
                Curve = curve,
                Q = new ECPoint { X = x.ToArray(), Y = y.ToArray() },
            });
            return key.DeriveRawSecretAgreement(peer.PublicKey);
        }

        public override void Dispose() => key.Dispose();

        private static BigInteger Hex(string value) =>
            BigInteger.Parse("0" + value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private bool IsOnCurve(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
        {
            var px = new BigInteger(x, isUnsigned: true, isBigEndian: true);
            var py = new BigInteger(y, isUnsigned: true, isBigEndian: true);
            if (px >= prime || py >= prime)
            {
                return false;
            }

            var left = BigInteger.ModPow(py, 2, prime);
            var right = (((BigInteger.ModPow(px, 3, prime) - (3 * px) + b) % prime) + prime) % prime;
            return left == right;
        }
    }
}
