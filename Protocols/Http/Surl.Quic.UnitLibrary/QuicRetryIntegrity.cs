using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// The Retry Integrity Tag of QUIC version 1 (RFC 9001 section 5.8): AEAD_AES_128_GCM with a
/// fixed key and nonce over the Retry pseudo-packet - the original Destination Connection ID,
/// length-prefixed, then the Retry packet without its tag - and an empty plaintext. Surl sends no
/// Retry (ADR-0082 decision 4); the tag is here so a Retry the codec reads can be checked.
/// </summary>
public static class QuicRetryIntegrity
{
    private static ReadOnlySpan<byte> Key =>
        [0xbe, 0x0c, 0x69, 0x0b, 0x9f, 0x66, 0x57, 0x5a, 0x1d, 0x76, 0x6b, 0x54, 0xe3, 0x68, 0xc8, 0x4e];

    private static ReadOnlySpan<byte> Nonce =>
        [0x46, 0x15, 0x99, 0xd3, 0x5d, 0x63, 0x2b, 0xf2, 0x23, 0x98, 0x25, 0xbb];

    /// <summary>Computes the Retry Integrity Tag a Retry packet should carry.</summary>
    /// <param name="originalDestinationConnectionId">The Destination Connection ID of the client's Initial that the Retry answers, at most 20 bytes.</param>
    /// <param name="packet">The Retry packet; its own <see cref="QuicRetryPacket.RetryIntegrityTag"/> is not part of the computation.</param>
    /// <returns>The 16-byte tag.</returns>
    /// <exception cref="ArgumentException">A connection ID is longer than 20 bytes, or a field of <paramref name="packet"/> is not one RFC 9000 allows.</exception>
    public static byte[] ComputeTag(ReadOnlySpan<byte> originalDestinationConnectionId, QuicRetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(originalDestinationConnectionId.Length, QuicConnectionIds.MaxLength, nameof(originalDestinationConnectionId));
        var packetBytes = packet.ToBytes();
        var withoutTag = packetBytes.AsSpan(0, packetBytes.Length - QuicRetryPacket.RetryIntegrityTagLength);
        var pseudoPacket = new byte[1 + originalDestinationConnectionId.Length + withoutTag.Length];
        pseudoPacket[0] = (byte)originalDestinationConnectionId.Length;
        originalDestinationConnectionId.CopyTo(pseudoPacket.AsSpan(1));
        withoutTag.CopyTo(pseudoPacket.AsSpan(1 + originalDestinationConnectionId.Length));
        var tag = new byte[QuicRetryPacket.RetryIntegrityTagLength];
        using var aead = new AesGcm(Key, QuicRetryPacket.RetryIntegrityTagLength);
        aead.Encrypt(Nonce, [], [], tag, pseudoPacket);
        return tag;
    }

    /// <summary>Returns whether a Retry packet carries the tag its original Destination Connection ID gives it.</summary>
    /// <param name="originalDestinationConnectionId">The Destination Connection ID of the client's Initial that the Retry answers, at most 20 bytes.</param>
    /// <param name="packet">The Retry packet.</param>
    /// <returns><see langword="true"/> when the tag is the one <see cref="ComputeTag"/> gives.</returns>
    /// <exception cref="ArgumentException">A connection ID is longer than 20 bytes, or a field of <paramref name="packet"/> is not one RFC 9000 allows.</exception>
    public static bool HasValidTag(ReadOnlySpan<byte> originalDestinationConnectionId, QuicRetryPacket packet) =>
        CryptographicOperations.FixedTimeEquals(ComputeTag(originalDestinationConnectionId, packet), packet.RetryIntegrityTag.Span);
}
