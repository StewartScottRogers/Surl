using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One of the MACs ADR-0051 decision 2 offers by default: <c>hmac-sha2-256</c> and
/// <c>hmac-sha2-512</c> (RFC 6668), and their <c>-etm@openssh.com</c> forms (OpenSSH
/// <c>PROTOCOL</c> section 1.7), each keyed with as many bytes as its hash gives and sending
/// the whole HMAC.
/// </summary>
/// <param name="HashAlgorithm">The HMAC's hash.</param>
/// <param name="KeyLength">The integrity key's length, which is also the MAC's.</param>
/// <param name="EncryptThenMac">
/// Whether the MAC is computed over the ciphertext with <c>packet_length</c> sent in the clear
/// (the <c>-etm</c> forms), rather than over the packet in the clear (RFC 4253, section 6.4).
/// </param>
internal sealed record SshHmac(HashAlgorithmName HashAlgorithm, int KeyLength, bool EncryptThenMac)
{
    private static readonly Dictionary<string, SshHmac> Macs = new(StringComparer.Ordinal)
    {
        ["hmac-sha2-256"] = new(HashAlgorithmName.SHA256, 32, false),
        ["hmac-sha2-512"] = new(HashAlgorithmName.SHA512, 64, false),
        ["hmac-sha2-256-etm@openssh.com"] = new(HashAlgorithmName.SHA256, 32, true),
        ["hmac-sha2-512-etm@openssh.com"] = new(HashAlgorithmName.SHA512, 64, true),
    };

    /// <summary>
    /// The MAC named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The MAC agreed, or <see langword="null"/> when none was.</param>
    /// <returns>The MAC, or <see langword="null"/> for none or one not built.</returns>
    public static SshHmac? ForName(string? name) => name is not null && Macs.TryGetValue(name, out var mac) ? mac : null;

    /// <summary>
    /// Computes <c>MAC(key, sequence_number || bytes)</c> (RFC 4253, section 6.4).
    /// </summary>
    /// <param name="key">The integrity key.</param>
    /// <param name="sequenceNumber">The packet's sequence number, hashed as a <c>uint32</c>.</param>
    /// <param name="bytes">The bytes the MAC covers.</param>
    /// <returns>The MAC.</returns>
    public byte[] Compute(byte[] key, uint sequenceNumber, ReadOnlySpan<byte> bytes)
    {
        var input = new byte[sizeof(uint) + bytes.Length];
        BinaryPrimitives.WriteUInt32BigEndian(input, sequenceNumber);
        bytes.CopyTo(input.AsSpan(sizeof(uint)));

        return CryptographicOperations.HmacData(HashAlgorithm, key, input);
    }
}
