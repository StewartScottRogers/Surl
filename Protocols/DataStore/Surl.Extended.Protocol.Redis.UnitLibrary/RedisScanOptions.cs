namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <c>SCAN</c>'s options after the cursor (the <c>SCAN</c> page): <c>MATCH pattern</c>,
/// <c>COUNT count</c> (default 10, at least 1) and <c>TYPE type</c>, each repeatable, the last
/// one winning. An unknown option or one missing its value is <c>-ERR syntax error</c>, as is a
/// count below 1; a count that is not an integer is <c>-ERR value is not an integer or out of range</c>.
/// </summary>
/// <param name="Pattern">The <c>MATCH</c> pattern, or <see langword="null"/> for every key.</param>
/// <param name="Count">How many keys to look at.</param>
/// <param name="TypeName">The <c>TYPE</c> to keep, or <see langword="null"/> for every type.</param>
/// <param name="Refusal">The error refusing the options, or <see langword="null"/>.</param>
internal sealed record RedisScanOptions(string? Pattern, long Count, string? TypeName, RespSimpleError? Refusal)
{
    /// <summary>How many keys a step looks at when <c>COUNT</c> is not given.</summary>
    public const long DefaultCount = 10;

    /// <summary>Reads the options from <c>SCAN cursor</c>'s third argument on.</summary>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <returns>The options, or the error refusing them in <see cref="Refusal"/>.</returns>
    public static RedisScanOptions Read(IReadOnlyList<byte[]> arguments)
    {
        var options = new RedisScanOptions(null, DefaultCount, null, null);
        for (var index = 2; index < arguments.Count && options.Refusal is null; index += 2)
        {
            options = index + 1 < arguments.Count
                ? options.With(RedisCommandTable.UpperCaseName(arguments[index]), arguments[index + 1])
                : options with { Refusal = RedisErrorReplies.SyntaxError };
        }

        return options;
    }

    /// <summary>Whether <paramref name="entry"/> passes <c>MATCH</c> and <c>TYPE</c>.</summary>
    /// <param name="entry">A key the step looked at.</param>
    /// <returns>Whether to answer it.</returns>
    public bool Matches(RedisKeyEntry entry) =>
        (Pattern is null || RedisGlobPattern.IsMatch(Pattern, entry.Key))
        && (TypeName is null || string.Equals(TypeName, entry.Value.TypeName, StringComparison.OrdinalIgnoreCase));

    private RedisScanOptions With(string option, byte[] value) => option switch
    {
        "MATCH" => this with { Pattern = RedisKeys.Name(value) },
        "COUNT" => WithCount(value),
        "TYPE" => this with { TypeName = RedisKeys.Name(value) },
        _ => this with { Refusal = RedisErrorReplies.SyntaxError },
    };

    private RedisScanOptions WithCount(byte[] value)
    {
        if (!RedisIntegers.TryParse(value, out var count))
        {
            return this with { Refusal = RedisErrorReplies.NotAnInteger };
        }

        return count < 1 ? this with { Refusal = RedisErrorReplies.SyntaxError } : this with { Count = count };
    }
}
