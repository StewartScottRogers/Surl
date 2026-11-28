using System.Text.Json;

namespace Surl.Mcp;

/// <summary>
/// Reads a number a tool's input schema has accepted as an <c>integer</c>. The schema counts any number with no
/// fractional part, <c>1.0</c> and <c>6e4</c> included, which <see cref="JsonElement"/>'s integer readers refuse
/// with a <see cref="FormatException"/>, so it is read as a <see cref="double"/> and converted. The schema's
/// <c>minimum</c> and <c>maximum</c> keep the number in the type's range and exact (ADR-0103 amendment 7).
/// </summary>
internal static class JsonElementWholeNumbers
{
    /// <summary>
    /// 2^53 - 1, the largest whole number below which a <see cref="double"/> holds every whole number exactly: the
    /// schema's <c>maximum</c> of every <see cref="long"/> argument with no smaller one.
    /// </summary>
    public const long LargestExactWholeNumber = 9007199254740991;

    /// <summary>Reads <paramref name="number"/> as an <see cref="int"/>.</summary>
    /// <param name="number">A whole number in <see cref="int"/>'s range.</param>
    /// <returns>The number.</returns>
    public static int GetWholeInt32(this JsonElement number) => (int)number.GetDouble();

    /// <summary>Reads <paramref name="number"/> as a <see cref="long"/>.</summary>
    /// <param name="number">A whole number no further from 0 than <see cref="LargestExactWholeNumber"/>.</param>
    /// <returns>The number.</returns>
    public static long GetWholeInt64(this JsonElement number) => (long)number.GetDouble();
}
