using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The NT hashes an account keeps for NTLM: one for each way a pinned upstream curl build turns
/// the password it was given into the <c>UNICODE(Passwd)</c> it hashes (BL-321, measured with
/// <c>pässword</c>). For an ASCII password all four are the specification's
/// <c>MD4(UTF-16LE(password))</c>.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><description><c>MD4(UTF-16LE(password))</c>, [MS-NLMP] section 3.3.1: the Windows
/// builds that hand SSPI a Unicode identity (stunnel/static-curl's, with the <c>Unicode</c>
/// feature).</description></item>
/// <item><description>MD4 of the password's UTF-8 bytes each widened to 16 bits: upstream curl's
/// own NTLM code (<c>Curl_ntlm_core_mk_nt_hash</c> in <c>lib/curl_ntlm_core.c</c> at
/// <c>curl-8_21_0</c>), which the Linux and macOS builds use.</description></item>
/// <item><description>MD4 of the UTF-16LE of the password's Windows-1252 bytes read as code
/// page 437: the Windows reference build, which has no <c>Unicode</c> feature, hands SSPI an
/// ANSI identity holding the command-line argument's ANSI (Windows-1252) bytes, and SSPI reads
/// them in the OEM code page (437 on a US-English Windows).</description></item>
/// <item><description>The same over the password's UTF-8 bytes: the reference build given the
/// password in a UTF-8 <c>-K</c> config file.</description></item>
/// </list>
/// </remarks>
internal static class NtlmPasswordHashes
{
    /// <summary>How many hashes <see cref="Compute"/> gives, and the dummy account keeps.</summary>
    public const int Count = 4;

    private const int NtHashLength = 16;

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static readonly Encoding OemUnitedStates = CodePagesEncodingProvider.Instance.GetEncoding(437)!;

    /// <summary>
    /// The four NT hashes of <paramref name="password"/>, in the order the remarks list them.
    /// </summary>
    /// <param name="password">The account's password.</param>
    /// <returns><see cref="Count"/> 16-byte hashes.</returns>
    public static IReadOnlyList<byte[]> Compute(string password) =>
    [
        NtlmV2Calculation.ComputeNtHash(password),
        NtlmV1Calculation.ComputeNtHashOfWidenedUtf8(password),
        NtlmV2Calculation.ComputeNtHash(OemUnitedStates.GetString(Windows1252.GetBytes(password))),
        NtlmV2Calculation.ComputeNtHash(OemUnitedStates.GetString(Encoding.UTF8.GetBytes(password))),
    ];

    /// <summary>
    /// <see cref="Count"/> random hashes, which match no answer: the dummy account's.
    /// </summary>
    /// <returns>The random hashes.</returns>
    public static IReadOnlyList<byte[]> ComputeRandom() =>
        [.. Enumerable.Range(0, Count).Select(_ => RandomNumberGenerator.GetBytes(NtHashLength))];
}
