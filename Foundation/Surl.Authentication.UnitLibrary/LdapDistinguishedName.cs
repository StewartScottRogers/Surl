namespace Surl.Authentication;

/// <summary>
/// A shape check of an RFC 4514 distinguished name: one or more RDNs joined by <c>,</c> (or <c>;</c>),
/// each one or more <c>type=value</c> joined by <c>+</c>, the type a descriptor or numeric OID, a
/// backslash escaping the character after it. The value's own grammar is left to the directory.
/// </summary>
internal static class LdapDistinguishedName
{
    /// <summary>
    /// Whether <paramref name="text"/> has the shape of a non-empty DN.
    /// </summary>
    /// <param name="text">The DN.</param>
    /// <returns>Whether it is well formed.</returns>
    public static bool IsWellFormed(string text)
    {
        var start = 0;
        var isEscaped = false;
        for (var index = 0; index <= text.Length; index++)
        {
            var isEnd = index == text.Length;
            var isSeparator = !isEnd && !isEscaped && text[index] is ',' or ';' or '+';
            if (!isEnd && !isSeparator)
            {
                isEscaped = !isEscaped && text[index] == '\\';
                continue;
            }

            if (isEnd && isEscaped)
            {
                return false;
            }

            if (!IsTypeAndValue(text.AsSpan(start, index - start)))
            {
                return false;
            }

            start = index + 1;
        }

        return true;
    }

    private static bool IsTypeAndValue(ReadOnlySpan<char> component)
    {
        var equals = component.IndexOf('=');

        return equals > 0 && LdapFilterEncoder.IsAttributeDescription(component[..equals].Trim());
    }
}
