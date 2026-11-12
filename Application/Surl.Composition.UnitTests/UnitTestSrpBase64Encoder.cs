using System.Numerics;
using System.Text;
using Surl.Cryptography.Srp;

namespace Surl.Composition;

/// <summary>
/// Writes bytes in GnuTLS's SRP base64 for the <c>--srp-groups</c> file tests: four digits per three
/// bytes, and a leading group of two digits for one leftover byte or three for two, left-padded with
/// <c>0</c> (as <c>Surl.Cryptography.Srp.UnitTests</c>' encoder of the same name).
/// </summary>
internal static class UnitTestSrpBase64Encoder
{
    internal static string Encode(byte[] bytes)
    {
        var digitCount = (4 * (bytes.Length / 3)) + (bytes.Length % 3 == 0 ? 0 : (bytes.Length % 3) + 1);
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var text = new StringBuilder();
        for (var index = 0; index < digitCount; index++)
        {
            text.Insert(0, GnuTlsSrpBase64.Alphabet[(int)(value & 63)]);
            value >>= 6;
        }

        return text.ToString();
    }

    /// <summary>One <c>tpasswd.conf</c> line, <c>index:N:g</c>.</summary>
    internal static string GroupLine(string index, SrpGroup group) =>
        $"{index}:{Encode(Srp6a.ToUnsignedBigEndian(group.Prime))}:{Encode(Srp6a.ToUnsignedBigEndian(group.Generator))}";
}
