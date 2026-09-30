using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.Ripemd160;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One of the MACs ADR-0051 decision 2 offers: by default <c>hmac-sha2-256</c> and
/// <c>hmac-sha2-512</c> (RFC 6668) and their <c>-etm@openssh.com</c> forms (OpenSSH
/// <c>PROTOCOL</c> section 1.7); with <c>--allow-weak-ssh-algorithms</c> also <c>hmac-sha1</c>,
/// <c>hmac-sha1-etm@openssh.com</c>, <c>hmac-sha1-96</c>, <c>hmac-md5</c> and
/// <c>hmac-md5-96</c> (RFC 4253, section 6.4), and OpenSSH's <c>hmac-ripemd160</c> and its other name
/// <c>hmac-ripemd160@openssh.com</c> (ADR-0061), HMAC-RIPEMD-160 (RFC 2286) over the packet in the
/// clear, computed by <see cref="HmacRipemd160"/> because the BCL has no RIPEMD-160. Each is keyed with as many bytes as its hash
/// gives, and sends the whole HMAC, or its first 12 bytes for the <c>-96</c> forms.
/// </summary>
/// <param name="HashAlgorithm">The HMAC's hash: a BCL hash's name, or <see cref="Ripemd160HashName"/>.</param>
/// <param name="KeyLength">The integrity key's length: the hash's.</param>
/// <param name="MacLength">How many bytes of the HMAC are sent: the hash's, or 12 for a <c>-96</c> form.</param>
/// <param name="EncryptThenMac">
/// Whether the MAC is computed over the ciphertext with <c>packet_length</c> sent in the clear
/// (the <c>-etm</c> forms), rather than over the packet in the clear (RFC 4253, section 6.4).
/// </param>
internal sealed record SshHmac(HashAlgorithmName HashAlgorithm, int KeyLength, int MacLength, bool EncryptThenMac)
{
    /// <summary>
    /// The name <see cref="HashAlgorithm"/> holds for RIPEMD-160, which the BCL does not compute.
    /// </summary>
    public static readonly HashAlgorithmName Ripemd160HashName = new("RIPEMD160");

    private const int TruncatedLength = 12;

    private static readonly Dictionary<string, SshHmac> Macs = new(StringComparer.Ordinal)
    {
        ["hmac-sha2-256"] = new(HashAlgorithmName.SHA256, 32, 32, false),
        ["hmac-sha2-512"] = new(HashAlgorithmName.SHA512, 64, 64, false),
        ["hmac-sha2-256-etm@openssh.com"] = new(HashAlgorithmName.SHA256, 32, 32, true),
        ["hmac-sha2-512-etm@openssh.com"] = new(HashAlgorithmName.SHA512, 64, 64, true),
        ["hmac-sha1"] = new(HashAlgorithmName.SHA1, 20, 20, false),
        ["hmac-sha1-etm@openssh.com"] = new(HashAlgorithmName.SHA1, 20, 20, true),
        ["hmac-sha1-96"] = new(HashAlgorithmName.SHA1, 20, TruncatedLength, false),
        ["hmac-md5"] = new(HashAlgorithmName.MD5, 16, 16, false),
        ["hmac-md5-96"] = new(HashAlgorithmName.MD5, 16, TruncatedLength, false),
        ["hmac-ripemd160"] = new(Ripemd160HashName, HmacRipemd160.HashSize, HmacRipemd160.HashSize, false),
        ["hmac-ripemd160@openssh.com"] = new(Ripemd160HashName, HmacRipemd160.HashSize, HmacRipemd160.HashSize, false),
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
    /// <returns>The MAC, <see cref="MacLength"/> bytes.</returns>
    public byte[] Compute(byte[] key, uint sequenceNumber, ReadOnlySpan<byte> bytes)
    {
        var input = new byte[sizeof(uint) + bytes.Length];
        BinaryPrimitives.WriteUInt32BigEndian(input, sequenceNumber);
        bytes.CopyTo(input.AsSpan(sizeof(uint)));

        return Tag(key, input);
    }

    /// <summary>
    /// The HMAC of <paramref name="message"/>, cut to <see cref="MacLength"/> bytes.
    /// </summary>
    /// <param name="key">The integrity key.</param>
    /// <param name="message">What is authenticated.</param>
    /// <returns>The first <see cref="MacLength"/> bytes of the HMAC.</returns>
    public byte[] Tag(byte[] key, ReadOnlySpan<byte> message) =>
        (HashAlgorithm == Ripemd160HashName ? HmacRipemd160.HashData(key, message) : CryptographicOperations.HmacData(HashAlgorithm, key, message))[..MacLength];
}
