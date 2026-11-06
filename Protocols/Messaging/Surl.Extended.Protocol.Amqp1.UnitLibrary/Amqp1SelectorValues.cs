namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The value rules of a JMS selector (Jakarta Messaging 3.1 section 3.8.1.2): NULL in, NULL out for
/// arithmetic and comparison; numbers compare and calculate as <c>long</c> when both are exact and as
/// <c>double</c> otherwise; strings and booleans compare only with <c>=</c> and <c>&lt;&gt;</c>; and
/// comparing values of unlike types is FALSE. Arithmetic on anything but numbers, and an exact division by
/// zero, is NULL. Nothing here throws.
/// </summary>
internal static class Amqp1SelectorValues
{
    /// <summary>A value as three-valued logic sees it: a <c>bool</c> is itself, anything else NULL.</summary>
    /// <param name="value">The value.</param>
    /// <returns>TRUE, FALSE or NULL.</returns>
    public static bool? AsLogic(object? value) => value as bool?;

    /// <summary>Normalises an application property's decoded value to a selector value.</summary>
    /// <param name="value">The decoded value.</param>
    /// <returns>
    /// A string or symbol as a <c>string</c>; a <c>bool</c>; any integer as a <c>long</c> (a <c>ulong</c>
    /// past <see cref="long.MaxValue"/> as a <c>double</c>); a <c>float</c> or <c>double</c> as a
    /// <c>double</c>; a <c>timestamp</c> as its milliseconds; anything else NULL.
    /// </returns>
    public static object? Normalise(object? value) => value switch
    {
        string or bool => value,
        Amqp1Symbol symbol => symbol.Value,
        Amqp1Timestamp timestamp => timestamp.MillisecondsSinceUnixEpoch,
        IConvertible number => NormaliseNumber(number),
        _ => null,
    };

    /// <summary>Compares two values.</summary>
    /// <param name="comparison">The comparison's token kind.</param>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>TRUE, FALSE, or NULL when either value is NULL.</returns>
    public static object? Compare(Amqp1SelectorTokenKind comparison, object? left, object? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            return CompareNumbers(comparison, left, right);
        }

        return CompareForEquality(comparison, left, right);
    }

    /// <summary>Calculates <c>+</c>, <c>-</c>, <c>*</c> or <c>/</c>.</summary>
    /// <param name="operation">The operator's token kind.</param>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result; NULL when either value is not a number, or on an exact division by zero.</returns>
    public static object? Calculate(Amqp1SelectorTokenKind operation, object? left, object? right)
    {
        if (left is long exactLeft && right is long exactRight)
        {
            return CalculateExact(operation, exactLeft, exactRight);
        }

        return IsNumber(left) && IsNumber(right)
            ? CalculateApproximate(operation, Convert.ToDouble(left, null), Convert.ToDouble(right, null))
            : null;
    }

    /// <summary>Applies a unary sign.</summary>
    /// <param name="negates">Whether the sign is <c>-</c>.</param>
    /// <param name="value">The value.</param>
    /// <returns>The signed number; NULL when the value is not a number.</returns>
    public static object? Sign(bool negates, object? value) => value switch
    {
        long exact => negates ? unchecked(-exact) : exact,
        double approximate => negates ? -approximate : approximate,
        _ => null,
    };

    private static object? NormaliseNumber(IConvertible number) => number.GetTypeCode() switch
    {
        >= TypeCode.SByte and <= TypeCode.Int64 => number.ToInt64(null),
        TypeCode.UInt64 => NormaliseUnsignedLong(number.ToUInt64(null)),
        TypeCode.Single or TypeCode.Double => number.ToDouble(null),
        _ => null,
    };

    private static object NormaliseUnsignedLong(ulong number) => number <= long.MaxValue ? (long)number : (double)number;

    private static bool IsNumber(object? value) => value is long or double;

    private static bool CompareForEquality(Amqp1SelectorTokenKind comparison, object left, object right)
    {
        var isEquality = comparison is Amqp1SelectorTokenKind.Equal or Amqp1SelectorTokenKind.NotEqual;
        return isEquality && left.GetType() == right.GetType()
            && (comparison == Amqp1SelectorTokenKind.Equal) == left.Equals(right);
    }

    private static bool CompareNumbers(Amqp1SelectorTokenKind comparison, object left, object right)
    {
        if (left is long exactLeft && right is long exactRight)
        {
            return Holds(comparison, exactLeft.CompareTo(exactRight));
        }

        double approximateLeft = Convert.ToDouble(left, null), approximateRight = Convert.ToDouble(right, null);
        return double.IsNaN(approximateLeft) || double.IsNaN(approximateRight)
            ? comparison == Amqp1SelectorTokenKind.NotEqual
            : Holds(comparison, approximateLeft.CompareTo(approximateRight));
    }

    private static bool Holds(Amqp1SelectorTokenKind comparison, int order) => comparison switch
    {
        Amqp1SelectorTokenKind.Equal => order == 0,
        Amqp1SelectorTokenKind.NotEqual => order != 0,
        Amqp1SelectorTokenKind.Less => order < 0,
        Amqp1SelectorTokenKind.LessOrEqual => order <= 0,
        Amqp1SelectorTokenKind.Greater => order > 0,
        _ => order >= 0,
    };

    private static object? CalculateExact(Amqp1SelectorTokenKind operation, long left, long right) => operation switch
    {
        Amqp1SelectorTokenKind.Plus => unchecked(left + right),
        Amqp1SelectorTokenKind.Minus => unchecked(left - right),
        Amqp1SelectorTokenKind.Multiply => unchecked(left * right),
        _ => DivideExact(left, right),
    };

    private static object? DivideExact(long left, long right) => right switch
    {
        0 => null,
        -1 => unchecked(-left),
        _ => left / right,
    };

    private static double CalculateApproximate(Amqp1SelectorTokenKind operation, double left, double right) => operation switch
    {
        Amqp1SelectorTokenKind.Plus => left + right,
        Amqp1SelectorTokenKind.Minus => left - right,
        Amqp1SelectorTokenKind.Multiply => left * right,
        _ => left / right,
    };
}
