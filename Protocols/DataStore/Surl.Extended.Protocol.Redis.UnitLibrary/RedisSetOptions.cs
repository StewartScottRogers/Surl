namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <c>SET</c>'s options after the key and value (the <c>SET</c> page): <c>NX</c> or <c>XX</c>,
/// <c>GET</c>, and one of <c>EX</c>, <c>PX</c>, <c>EXAT</c>, <c>PXAT</c> or <c>KEEPTTL</c>. As Redis
/// reads them, an option may repeat (a repeated expiry's last time wins), and <c>NX</c> with
/// <c>XX</c>, two different expiry options, or an option missing its time is <c>-ERR syntax error</c>.
/// </summary>
/// <param name="OnlyIfAbsent"><c>NX</c>: set only when the key is absent.</param>
/// <param name="OnlyIfPresent"><c>XX</c>: set only when the key is present.</param>
/// <param name="AnswersOldValue"><c>GET</c>: answer the value before the set.</param>
/// <param name="KeepsExpiry"><c>KEEPTTL</c>: keep the key's expiry.</param>
/// <param name="Expiry">The expiry option given, or <see langword="null"/>.</param>
/// <param name="ExpiryTime">The time after the expiry option, or <see langword="null"/>.</param>
internal sealed record RedisSetOptions(
    bool OnlyIfAbsent,
    bool OnlyIfPresent,
    bool AnswersOldValue,
    bool KeepsExpiry,
    RedisSetExpiry? Expiry,
    byte[]? ExpiryTime)
{
    private static readonly RedisSetOptions None = new(false, false, false, false, null, null);

    /// <summary>Reads the options from <c>SET key value</c>'s fourth argument on.</summary>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <param name="options">The options, when they read.</param>
    /// <returns>Whether they read; otherwise the answer is <c>-ERR syntax error</c>.</returns>
    public static bool TryRead(IReadOnlyList<byte[]> arguments, out RedisSetOptions options)
    {
        options = None;
        for (var index = 3; index < arguments.Count; index++)
        {
            var name = RedisCommandTable.UpperCaseName(arguments[index]);
            var next = index + 1 < arguments.Count ? arguments[index + 1] : null;
            if (options.WithFlag(name) is { } flagged)
            {
                options = flagged;
                continue;
            }

            if (options.WithExpiry(name, next) is not { } expiring)
            {
                return false;
            }

            options = expiring;
            index++;
        }

        return true;
    }

    /// <summary>Whether <c>NX</c> and <c>XX</c> let the set go ahead.</summary>
    /// <param name="isPresent">Whether the key is present, of any type.</param>
    /// <returns>Whether to set.</returns>
    public bool AllowsSetting(bool isPresent) => OnlyIfAbsent ? !isPresent : !OnlyIfPresent || isPresent;

    private RedisSetOptions? WithFlag(string name) => name switch
    {
        "NX" when !OnlyIfPresent => this with { OnlyIfAbsent = true },
        "XX" when !OnlyIfAbsent => this with { OnlyIfPresent = true },
        "GET" => this with { AnswersOldValue = true },
        "KEEPTTL" when Expiry is null => this with { KeepsExpiry = true },
        _ => null,
    };

    private RedisSetOptions? WithExpiry(string name, byte[]? time)
    {
        var expiry = RedisSetExpiry.Named(name);
        var conflicts = KeepsExpiry || (Expiry is not null && Expiry != expiry);
        return expiry is null || conflicts || time is null ? null : this with { Expiry = expiry, ExpiryTime = time };
    }
}
