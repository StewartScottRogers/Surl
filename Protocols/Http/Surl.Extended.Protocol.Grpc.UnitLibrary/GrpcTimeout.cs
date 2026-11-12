namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Reads the <c>grpc-timeout</c> request header of PROTOCOL-HTTP2.md "Requests" (ADR-0116 decision 5):
/// TimeoutValue, 1 to 8 ASCII digits, then exactly one TimeoutUnit - <c>H</c> hours, <c>M</c> minutes,
/// <c>S</c> seconds, <c>m</c> milliseconds, <c>u</c> microseconds, <c>n</c> nanoseconds - and nothing else.
/// </summary>
public static class GrpcTimeout
{
    private const int MaximumDigits = 8;
    private const long TicksPerMicrosecond = 10;
    private const long NanosecondsPerTick = 100;

    /// <summary>
    /// Reads the call's <c>grpc-timeout</c> fields. A value of 0 is a deadline already passed. A nanosecond
    /// value is rounded up to the 100-nanosecond tick, so a deadline of 1n is not read as 0.
    /// </summary>
    /// <param name="values">Every <c>grpc-timeout</c> field the request carried, in order.</param>
    /// <returns>The time the call may take, or <see langword="null" /> when it carried none: an infinite deadline.</returns>
    /// <exception cref="GrpcCallException"><see cref="GrpcStatus.MalformedGrpcTimeout" /> for a value outside the grammar, or more than one field.</exception>
    public static TimeSpan? Read(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count switch
        {
            0 => null,
            1 => ReadValue(values[0]),
            _ => throw Malformed(),
        };
    }

    private static TimeSpan ReadValue(string value)
    {
        var amount = ReadAmount(value);
        return value[^1] switch
        {
            'H' => TimeSpan.FromHours(amount),
            'M' => TimeSpan.FromMinutes(amount),
            'S' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMilliseconds(amount),
            'u' => TimeSpan.FromTicks(amount * TicksPerMicrosecond),
            'n' => TimeSpan.FromTicks((amount + NanosecondsPerTick - 1) / NanosecondsPerTick),
            _ => throw Malformed(),
        };
    }

    private static long ReadAmount(string value)
    {
        var digits = value.Length - 1;
        if (digits is < 1 or > MaximumDigits || value.AsSpan(0, digits).ContainsAnyExceptInRange('0', '9'))
        {
            throw Malformed();
        }

        return long.Parse(value.AsSpan(0, digits), provider: System.Globalization.CultureInfo.InvariantCulture);
    }

    private static GrpcCallException Malformed() => new(GrpcStatus.MalformedGrpcTimeout);
}
