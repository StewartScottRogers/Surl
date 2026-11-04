using System.Security.Cryptography;

namespace Surl.Protocol.Pop3;

/// <summary>
/// A <see cref="RandomNumberGenerator"/> that fills every request with <c>01 23 45 67 89 ab cd
/// ef</c> repeated, so the greeting's <c>APOP</c> timestamp starts <c>&lt;0123456789abcdef.</c>,
/// the one the fixtures were recorded with.
/// </summary>
internal sealed class PatternRandomNumberGenerator : RandomNumberGenerator
{
    private static readonly byte[] Pattern = [0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef];

    public override void GetBytes(byte[] data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = Pattern[index % Pattern.Length];
        }
    }
}
