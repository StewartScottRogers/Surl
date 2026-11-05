namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Glob patterns as the <c>KEYS</c> page describes them, matched on bytes (one character per byte):
/// <c>*</c> any run, <c>?</c> any one byte, <c>[abc]</c>, <c>[^a]</c> and <c>[a-z]</c> a byte in or
/// out of a class, and <c>\</c> taking the next byte literally (ADR-0106 decision 1). It follows
/// Redis's matcher in the corners: a range written backwards is swapped, a class left unclosed
/// runs to the pattern's end, and a trailing <c>\</c> matches itself. A <c>*</c> is matched by
/// backtracking to the last one, so a pattern costs at most pattern length times key length.
/// </summary>
internal static class RedisGlobPattern
{
    /// <summary>Whether <paramref name="text"/> matches <paramref name="pattern"/> in full.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The key.</param>
    /// <returns>Whether it matches.</returns>
    public static bool IsMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        int patternIndex = 0, textIndex = 0, afterStar = -1, starTextIndex = 0;
        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                afterStar = ++patternIndex;
                starTextIndex = textIndex;
            }
            else if (patternIndex < pattern.Length && MatchesOne(pattern, ref patternIndex, text[textIndex]))
            {
                textIndex++;
            }
            else if (afterStar < 0)
            {
                return false;
            }
            else
            {
                patternIndex = afterStar;
                textIndex = ++starTextIndex;
            }
        }

        return pattern[patternIndex..].TrimStart('*').IsEmpty;
    }

    // One pattern element against one byte; moves past the element when it matches.
    private static bool MatchesOne(ReadOnlySpan<char> pattern, ref int patternIndex, char character)
    {
        var index = patternIndex;
        var matches = pattern[index] switch
        {
            '?' => SkipAny(ref index),
            '[' => MatchesClass(pattern, ref index, character),
            '\\' when index + 1 < pattern.Length => MatchesLiteral(pattern, ref index, 2, character),
            _ => MatchesLiteral(pattern, ref index, 1, character),
        };
        if (matches)
        {
            patternIndex = index;
        }

        return matches;
    }

    private static bool SkipAny(ref int index)
    {
        index++;
        return true;
    }

    // The character width - 1 places on taken literally: the next one, or the one after a backslash.
    private static bool MatchesLiteral(ReadOnlySpan<char> pattern, ref int index, int width, char character)
    {
        index += width;
        return pattern[index - 1] == character;
    }

    // [abc], [^abc], [a-z] and \ inside them; an unclosed class runs to the pattern's end.
    private static bool MatchesClass(ReadOnlySpan<char> pattern, ref int index, char character)
    {
        index++;
        var isNegated = index < pattern.Length && pattern[index] == '^';
        index += isNegated ? 1 : 0;
        var matches = false;
        while (index < pattern.Length && pattern[index] != ']')
        {
            matches |= MatchesClassMember(pattern, ref index, character);
        }

        index = Math.Min(index + 1, pattern.Length);
        return matches != isNegated;
    }

    private static bool MatchesClassMember(ReadOnlySpan<char> pattern, ref int index, char character)
    {
        var remaining = pattern.Length - index;
        if (pattern[index] == '\\' && remaining >= 2)
        {
            index += 2;
            return pattern[index - 1] == character;
        }

        if (remaining >= 3 && pattern[index + 1] == '-')
        {
            var (first, last) = (pattern[index], pattern[index + 2]);
            index += 3;
            return character >= Math.Min(first, last) && character <= Math.Max(first, last);
        }

        index++;
        return pattern[index - 1] == character;
    }
}
