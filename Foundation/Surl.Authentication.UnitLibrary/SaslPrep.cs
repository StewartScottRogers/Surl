using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The <c>Normalize</c> SCRAM applies to a password before hashing it (RFC 5802 section 2.2):
/// SASLprep, RFC 4013, built by hand on the base class library. Its mapping step is RFC 4013's:
/// every non-ASCII space (RFC 3454 table C.1.2) becomes U+0020 and every character commonly mapped
/// to nothing (table B.1) is removed; then the result is put in Unicode normalization form KC
/// with <see cref="string.Normalize(NormalizationForm)"/>. The prohibited-output and
/// bidirectional checks are not made: they only refuse a string, and a client that makes them
/// never sends a password they would refuse, so leaving them out changes no accepted login. A
/// password of printable ASCII is its own normalization.
/// </summary>
internal static class SaslPrep
{
    // RFC 3454 table C.1.2, non-ASCII space characters.
    private const string NonAsciiSpaces =
        "             ​  　";

    // RFC 3454 table B.1, commonly mapped to nothing.
    private const string MappedToNothing =
        "­͏᠆᠋᠌᠍‌‍⁠︀︁︂︃︄︅︆︇"
        + "︈︉︊︋︌︍︎️﻿";

    /// <summary>
    /// SASLprep's mapping and normalization of <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The string, such as a password.</param>
    /// <returns>The prepared string.</returns>
    public static string Normalize(string text)
    {
        var mapped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            mapped.Append(NonAsciiSpaces.Contains(character) ? " " : MappedToNothing.Contains(character) ? string.Empty : character);
        }

        return mapped.ToString().Normalize(NormalizationForm.FormKC);
    }
}
