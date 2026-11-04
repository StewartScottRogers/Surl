using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// H is computed over <c>V_C, V_S, I_C, I_S, K_S</c>, the method's own fields and K, K as an
/// <c>mpint</c> in the fewest bytes (RFC 4251 section 5; RFC 4253 section 8). A K whose top byte
/// is zero, which a Diffie-Hellman round gives about 1 time in 256, is hashed without that byte:
/// libssh2 1.11.1 on WinCNG hashes it with the byte, so its H differs and the exchange fails
/// (BL-251).
/// </summary>
[TestClass]
public sealed class SshExchangeHashInputTests
{
    private static readonly byte[] ClientIdentification = "SSH-2.0-libssh2_1.11.1"u8.ToArray();

    private static readonly byte[] ServerIdentification = "SSH-2.0-surl"u8.ToArray();

    private static readonly byte[] ClientKexInit = [0x14, 0x01, 0x02];

    private static readonly byte[] ServerKexInit = [0x14, 0x03, 0x04];

    private static readonly byte[] HostKeyBlob = [0x00, 0x00, 0x00, 0x07, .. "ssh-rsa"u8];

    // A 2048-bit group's K as a fixed-width 256-byte buffer whose top byte is zero.
    private static readonly byte[] FixedWidthSharedSecret = [0x00, 0x7F, .. Enumerable.Range(0, 254).Select(value => (byte)value)];

    [TestMethod]
    public void ComputeHash_SharedSecretWithATopByteOfZero_HashesItsMpintWithoutThatByte()
    {
        byte[] canonicalMpint = [0x00, 0x00, 0x00, 0xFF, .. FixedWidthSharedSecret[1..]];

        var hash = Input().ComputeHash(SharedSecret(FixedWidthSharedSecret), HashAlgorithmName.SHA256);

        CollectionAssert.AreEqual(SHA256.HashData([.. CommonFields(), .. canonicalMpint]), hash);
    }

    [TestMethod]
    public void ComputeHash_SharedSecretWithATopByteOfZero_DiffersFromTheHashOverTheFixedWidthBuffer()
    {
        byte[] fixedWidthMpint = [0x00, 0x00, 0x01, 0x00, .. FixedWidthSharedSecret];

        var hash = Input().ComputeHash(SharedSecret(FixedWidthSharedSecret), HashAlgorithmName.SHA256);

        CollectionAssert.AreNotEqual(SHA256.HashData([.. CommonFields(), .. fixedWidthMpint]), hash);
    }

    [TestMethod]
    public void ComputeHash_SharedSecretWithItsTopBitSet_HashesItsMpintWithALeadingZeroByte()
    {
        byte[] magnitude = [0x80, .. FixedWidthSharedSecret[3..]];
        byte[] mpint = [0x00, 0x00, 0x00, 0xFF, 0x00, .. magnitude];

        var hash = Input().ComputeHash(SharedSecret(magnitude), HashAlgorithmName.SHA256);

        CollectionAssert.AreEqual(SHA256.HashData([.. CommonFields(), .. mpint]), hash);
    }

    private static SshExchangeHashInput Input() =>
        new(ClientIdentification, ServerIdentification, ClientKexInit, ServerKexInit, HostKeyBlob);

    private static BigInteger SharedSecret(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private static byte[] CommonFields()
    {
        var fields = new SshWireWriter();
        fields.WriteString(ClientIdentification);
        fields.WriteString(ServerIdentification);
        fields.WriteString(ClientKexInit);
        fields.WriteString(ServerKexInit);
        fields.WriteString(HostKeyBlob);

        return fields.ToArray();
    }
}
