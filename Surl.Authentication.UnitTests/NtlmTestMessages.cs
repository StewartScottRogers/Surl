using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Builds NTLM messages for the tests that need one upstream curl was not recorded sending: a
/// <c>NEGOTIATE_MESSAGE</c> with chosen flags and an <c>AUTHENTICATE_MESSAGE</c> with a chosen
/// user, domain and <c>NtChallengeResponse</c>, laid out as [MS-NLMP] section 2.2.1 says.
/// </summary>
internal static class NtlmTestMessages
{
    public const uint Unicode = 0x00000001;

    public const uint Oem = 0x00000002;

    // The blob of [MS-NLMP] section 4.2.4 with a zero timestamp and client challenge aa...aa,
    // over its target information (NetBIOS domain "Domain", computer "Server").
    public static readonly byte[] ClientBlob = Convert.FromHexString(
        "0101000000000000" + "0000000000000000" + "AAAAAAAAAAAAAAAA" + "00000000"
        + "02000C0044006F006D00610069006E00" + "01000C00530065007200760065007200" + "00000000"
        + "00000000");

    public static byte[] Negotiate(uint flags)
    {
        var message = new byte[32];
        "NTLMSSP\0"u8.CopyTo(message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(12), flags);

        return message;
    }

    /// <summary>
    /// An NTLMv2 answer for <paramref name="password"/> to <paramref name="serverChallenge"/>,
    /// computed over <paramref name="proofUser"/> and <paramref name="proofDomain"/> but naming
    /// <paramref name="user"/> and <paramref name="domain"/>, so a test can send a message whose
    /// names differ from those the proof covers.
    /// </summary>
    public static byte[] Authenticate(
        string user,
        string domain,
        string password,
        byte[] serverChallenge,
        bool unicode = true,
        string? proofUser = null,
        string? proofDomain = null)
    {
        var key = NtlmV2Calculation.ComputeResponseKeyNt(
            NtlmV2Calculation.ComputeNtHash(password), proofUser ?? user, proofDomain ?? domain);
        var proof = NtlmV2Calculation.ComputeNtProof(key, serverChallenge, ClientBlob);

        return Authenticate(user, domain, [.. proof, .. ClientBlob], unicode ? Unicode : Oem);
    }

    public static byte[] Authenticate(string user, string domain, byte[] ntResponse, uint flags) =>
        Authenticate(user, domain, ntResponse, flags, []);

    /// <summary>
    /// As <see cref="Authenticate(string, string, byte[], uint)"/>, carrying <paramref name="encryptedSessionKey"/>
    /// as the <c>EncryptedRandomSessionKey</c>.
    /// </summary>
    public static byte[] Authenticate(string user, string domain, byte[] ntResponse, uint flags, byte[] encryptedSessionKey)
    {
        var encoding = (flags & Unicode) != 0 ? Encoding.Unicode : Encoding.Latin1;
        var domainBytes = encoding.GetBytes(domain);
        var userBytes = encoding.GetBytes(user);
        var message = new byte[64 + ntResponse.Length + domainBytes.Length + userBytes.Length + encryptedSessionKey.Length];
        var span = message.AsSpan();
        "NTLMSSP\0"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], 3);
        var offset = 64;
        foreach (var (field, payload) in new[] { (20, ntResponse), (28, domainBytes), (36, userBytes), (52, encryptedSessionKey) })
        {
            WriteField(span[field..], payload.Length, offset);
            payload.CopyTo(span[offset..]);
            offset += payload.Length;
        }

        foreach (var emptyField in new[] { 12, 44 })
        {
            WriteField(span[emptyField..], 0, 64);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(span[60..], flags);

        return message;
    }

    public static string Authorization(byte[] message) => $"NTLM {Convert.ToBase64String(message)}";

    private static void WriteField(Span<byte> destination, int length, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], (uint)offset);
    }
}
