using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// NTLMv2's arithmetic ([MS-NLMP] section 3.3.2), from the NT hash a server keeps rather than the
/// password: <c>NTOWFv2</c>, <c>NTProofStr</c> and the session base key. The tests reproduce the
/// specification's section 4.2.4 example with it.
/// </summary>
internal static class NtlmV2Calculation
{
    /// <summary>
    /// The length of <c>NTProofStr</c>, which opens an NTLMv2 <c>NtChallengeResponse</c>.
    /// </summary>
    public const int NtProofLength = 16;

    /// <summary>
    /// The NT hash of a password, <c>MD4(UNICODE(Passwd))</c> ([MS-NLMP] section 3.3.1): what an
    /// account keeps for NTLM.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The 16-byte hash.</returns>
    public static byte[] ComputeNtHash(string password) => Md4.HashData(Encoding.Unicode.GetBytes(password));

    /// <summary>
    /// <c>NTOWFv2</c>, the <c>ResponseKeyNT</c>:
    /// <c>HMAC_MD5(ntHash, UNICODE(Uppercase(User) || UserDom))</c>.
    /// </summary>
    /// <param name="ntHash">The account's NT hash.</param>
    /// <param name="userName">The user name as sent.</param>
    /// <param name="domainName">The domain name as sent.</param>
    /// <returns>The 16-byte key.</returns>
    public static byte[] ComputeResponseKeyNt(ReadOnlySpan<byte> ntHash, string userName, string domainName) =>
        HMACMD5.HashData(ntHash, Encoding.Unicode.GetBytes(userName.ToUpperInvariant() + domainName));

    /// <summary>
    /// <c>NTProofStr = HMAC_MD5(ResponseKeyNT, ServerChallenge || temp)</c>, where <c>temp</c> is
    /// the client's blob: everything in the <c>NtChallengeResponse</c> after <c>NTProofStr</c>.
    /// </summary>
    /// <param name="responseKeyNt">From <see cref="ComputeResponseKeyNt"/>.</param>
    /// <param name="serverChallenge">The eight bytes the challenge carried.</param>
    /// <param name="clientBlob">The client's <c>temp</c>.</param>
    /// <returns>The 16-byte proof.</returns>
    public static byte[] ComputeNtProof(
        ReadOnlySpan<byte> responseKeyNt, ReadOnlySpan<byte> serverChallenge, ReadOnlySpan<byte> clientBlob) =>
        HMACMD5.HashData(responseKeyNt, [.. serverChallenge, .. clientBlob]);

    /// <summary>
    /// <c>SessionBaseKey = HMAC_MD5(ResponseKeyNT, NTProofStr)</c>.
    /// </summary>
    /// <param name="responseKeyNt">From <see cref="ComputeResponseKeyNt"/>.</param>
    /// <param name="ntProof">From <see cref="ComputeNtProof"/>.</param>
    /// <returns>The 16-byte key.</returns>
    public static byte[] ComputeSessionBaseKey(ReadOnlySpan<byte> responseKeyNt, ReadOnlySpan<byte> ntProof) =>
        HMACMD5.HashData(responseKeyNt, ntProof);
}
