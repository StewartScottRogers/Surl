using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// The bus's canonical topic names and filters (ADR-0100, decision 3.2), which are MQTT's
/// (MQTT 3.1.1 and 5.0 section 4.7): a non-empty UTF-8 string of at most
/// <see cref="MaxBytes"/> bytes without U+0000, split into levels at <c>/</c>, where a level
/// may be empty. A topic holds no <c>+</c> or <c>#</c>; a filter's level may be exactly
/// <c>+</c> (one level) or <c>#</c> (zero or more levels), and unlike MQTT a <c>#</c> may stand
/// at any level, because AMQP 0-9-1 binding keys allow it. Names compare ordinally.
/// </summary>
public static class CanonicalTopicName
{
    /// <summary>
    /// The most UTF-8 bytes a topic name or filter holds: 65535.
    /// </summary>
    public const int MaxBytes = 65535;

    private const char LevelSeparator = '/';
    private const string SingleLevelWildcard = "+";
    private const string MultiLevelWildcard = "#";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Whether <paramref name="name"/> is a canonical topic name a message can be published on.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> when it is non-empty, valid UTF-16, at most
    /// <see cref="MaxBytes"/> UTF-8 bytes, and holds no U+0000, <c>+</c> or <c>#</c>.</returns>
    public static bool IsValidTopic([NotNullWhen(true)] string? name) =>
        IsEncodable(name) && name.AsSpan().IndexOfAny('+', '#') < 0;

    /// <summary>
    /// Whether <paramref name="filter"/> is a canonical topic filter a subscription can be made with.
    /// </summary>
    /// <param name="filter">The filter to check.</param>
    /// <returns><see langword="true"/> when it is non-empty, valid UTF-16, at most
    /// <see cref="MaxBytes"/> UTF-8 bytes, holds no U+0000, and each level is <c>+</c>,
    /// <c>#</c>, or holds neither.</returns>
    public static bool IsValidFilter([NotNullWhen(true)] string? filter) =>
        IsEncodable(filter) && SplitLevels(filter).All(IsValidFilterLevel);

    /// <summary>
    /// Splits a valid name or filter into its levels.
    /// </summary>
    internal static string[] SplitLevels(string name) => name.Split(LevelSeparator);

    /// <summary>
    /// Whether a filter, split into levels, matches a topic, split into levels. A filter whose
    /// first level is a wildcard never matches a topic beginning with <c>$</c> (MQTT 3.1.1 and
    /// 5.0 section 4.7.2). Runs in time proportional to the product of the level counts, so no
    /// filter can make it backtrack without bound.
    /// </summary>
    internal static bool FilterMatches(string[] filterLevels, string[] topicLevels) =>
        Matches(filterLevels, topicLevels, LevelEquals);

    /// <summary>
    /// Whether a grant's filter, split into levels, covers a subscription's filter, split into
    /// levels (ADR-0102 decision 3.4), so every topic the second matches the first matches too:
    /// level by level, a <c>#</c> covers the rest, a <c>+</c> covers any one level but
    /// <c>#</c>, and a literal level covers only itself; a wildcard first level never covers a
    /// level beginning with <c>$</c>.
    /// </summary>
    internal static bool FilterCovers(string[] grantLevels, string[] filterLevels) =>
        Matches(grantLevels, filterLevels, LevelCovers);

    private static bool Matches(string[] filterLevels, string[] topicLevels, Func<string, string, bool> levelMatches)
    {
        if (topicLevels[0].StartsWith('$') && IsWildcard(filterLevels[0]))
        {
            return false;
        }

        // matchesFrom[t]: the filter's levels after the current one match the topic's levels from t on.
        var matchesFrom = new bool[topicLevels.Length + 1];
        matchesFrom[topicLevels.Length] = true;
        for (var f = filterLevels.Length - 1; f >= 0; f--)
        {
            var current = new bool[topicLevels.Length + 1];
            for (var t = topicLevels.Length; t >= 0; t--)
            {
                current[t] = LevelMatchesFrom(filterLevels[f], topicLevels, t, matchesFrom, current, levelMatches);
            }

            matchesFrom = current;
        }

        return matchesFrom[0];
    }

    private static bool LevelMatchesFrom(string filterLevel, string[] topicLevels, int t, bool[] matchesFrom, bool[] current, Func<string, string, bool> levelMatches) =>
        filterLevel == MultiLevelWildcard
            ? matchesFrom[t] || (t < topicLevels.Length && current[t + 1])
            : t < topicLevels.Length && levelMatches(filterLevel, topicLevels[t]) && matchesFrom[t + 1];

    private static bool LevelEquals(string filterLevel, string topicLevel) =>
        filterLevel == SingleLevelWildcard || string.Equals(filterLevel, topicLevel, StringComparison.Ordinal);

    private static bool LevelCovers(string grantLevel, string filterLevel) =>
        grantLevel == SingleLevelWildcard ? filterLevel != MultiLevelWildcard : string.Equals(grantLevel, filterLevel, StringComparison.Ordinal);

    private static bool IsWildcard(string level) => level is SingleLevelWildcard or MultiLevelWildcard;

    private static bool IsValidFilterLevel(string level) =>
        IsWildcard(level) || level.AsSpan().IndexOfAny('+', '#') < 0;

    private static bool IsEncodable([NotNullWhen(true)] string? name) =>
        name is { Length: > 0 and <= MaxBytes } && !name.Contains('\0') && Utf8ByteCount(name) <= MaxBytes;

    private static int Utf8ByteCount(string name)
    {
        try
        {
            return StrictUtf8.GetByteCount(name);
        }
        catch (EncoderFallbackException)
        {
            return int.MaxValue;
        }
    }
}
