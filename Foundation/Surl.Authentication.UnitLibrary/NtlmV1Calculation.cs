using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// NTLMv1's arithmetic without extended session security ([MS-NLMP] section 3.3.1), as upstream
/// curl's SMB client computes it in its session setup (<c>lib/smb.c</c> and
/// <c>lib/curl_ntlm_core.c</c> at <c>curl-8_21_0</c>): <c>LMOWFv1</c>, the NT hash and
/// <c>DESL</c>, which turns either hash and the server's challenge into a 24-byte response. The
/// tests reproduce the specification's section 4.2.2 example with it.
/// </summary>
/// <remarks>
/// Upstream curl hashes the password's bytes as it was given them, which are UTF-8, and this does
/// the same: <c>LMOWFv1</c> upper-cases only the ASCII letters and keeps the first 14 bytes, and
/// the NT hash widens each byte to 16 bits rather than encoding the password as UTF-16LE. For an
/// ASCII password both are exactly the specification's.
/// </remarks>
internal static class NtlmV1Calculation
{
    /// <summary>The length of an LM or NT response.</summary>
    public const int ResponseLength = 24;

    /// <summary>The length of the server's challenge.</summary>
    public const int ChallengeLength = 8;

    private const int LmPasswordLength = 14;

    private const int DesKeySeedLength = 7;

    private static readonly byte[] LmMagic = "KGS!@#$%"u8.ToArray();

    /// <summary>
    /// <c>LMOWFv1</c>: the password's first 14 UTF-8 bytes, ASCII letters upper-cased, zero-padded
    /// to 14, each 7-byte half a DES key that encrypts <c>KGS!@#$%</c>. A password over 14 bytes is
    /// cut, as upstream curl cuts it, so every password sharing those 14 bytes has the same hash.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The 16-byte hash.</returns>
    public static byte[] ComputeLmHash(string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] upperCased = new byte[LmPasswordLength];
        int length = Math.Min(passwordBytes.Length, LmPasswordLength);
        for (int index = 0; index < length; index++)
        {
            upperCased[index] = UpperCaseAsciiLetter(passwordBytes[index]);
        }

        return
        [
            .. Des.EncryptBlock(ExpandDesKey(upperCased.AsSpan(0, DesKeySeedLength)), LmMagic),
            .. Des.EncryptBlock(ExpandDesKey(upperCased.AsSpan(DesKeySeedLength)), LmMagic),
        ];
    }

    /// <summary>
    /// The NT hash as upstream curl computes it: MD4 of the password's UTF-8 bytes, each widened to
    /// a 16-bit little-endian unit. For an ASCII password this is the specification's
    /// <c>NTOWFv1</c>, <c>MD4(UNICODE(Passwd))</c>.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The 16-byte hash.</returns>
    public static byte[] ComputeNtHashOfWidenedUtf8(string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] widened = new byte[passwordBytes.Length * 2];
        for (int index = 0; index < passwordBytes.Length; index++)
        {
            widened[2 * index] = passwordBytes[index];
        }

        return Md4.HashData(widened);
    }

    /// <summary>
    /// <c>DESL(hash, challenge)</c>: the 16-byte hash zero-padded to 21 bytes, each 7-byte third a
    /// DES key that encrypts the challenge. Over the LM hash it is the LM response, over the NT
    /// hash the NT response.
    /// </summary>
    /// <param name="hash">The 16-byte LM or NT hash.</param>
    /// <param name="serverChallenge">The server's eight-byte challenge.</param>
    /// <returns>The 24-byte response.</returns>
    public static byte[] ComputeResponse(ReadOnlySpan<byte> hash, ReadOnlySpan<byte> serverChallenge)
    {
        Span<byte> keys = stackalloc byte[3 * DesKeySeedLength];
        hash.CopyTo(keys);
        return
        [
            .. Des.EncryptBlock(ExpandDesKey(keys[..DesKeySeedLength]), serverChallenge),
            .. Des.EncryptBlock(ExpandDesKey(keys.Slice(DesKeySeedLength, DesKeySeedLength)), serverChallenge),
            .. Des.EncryptBlock(ExpandDesKey(keys[(2 * DesKeySeedLength)..]), serverChallenge),
        ];
    }

    private static byte UpperCaseAsciiLetter(byte value) =>
        value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - ('a' - 'A')) : value;

    // Spreads 56 key bits over the high seven bits of eight bytes, as upstream curl's
    // extend_key_56_to_64 does. DES ignores each byte's low (parity) bit, so the odd parity
    // upstream curl then sets there changes nothing and is not set here.
    private static byte[] ExpandDesKey(ReadOnlySpan<byte> seed)
    {
        byte[] key = new byte[Des.KeyLength];
        key[0] = seed[0];
        for (int index = 1; index < DesKeySeedLength; index++)
        {
            key[index] = (byte)((seed[index - 1] << (8 - index)) | (seed[index] >> index));
        }

        key[7] = (byte)(seed[6] << 1);
        return key;
    }
}
