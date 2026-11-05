using System.Globalization;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// Splits an operation's arguments into fields and reads its sizes and counts (ADR-0113 decisions
/// 0 and 4): fields are separated by one or more spaces or tabs, and a size or count is a
/// non-negative decimal integer, anything else <c>-ERR 'Parser Error'</c>.
/// </summary>
internal static class NatsOperationFields
{
    private static readonly char[] FieldSeparators = [' ', '\t'];

    /// <summary>The fields of <paramref name="arguments"/>, empty ones dropped.</summary>
    public static string[] Split(string arguments) =>
        arguments.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Reads a size or count.</summary>
    /// <exception cref="NatsProtocolViolationException">It is not a non-negative decimal integer.</exception>
    public static long ReadCount(string operation, string field) =>
        long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : throw new NatsProtocolViolationException(NatsErrors.ParserError, $"{operation} carried the count '{field}'");

    /// <summary>Throws <c>-ERR 'Parser Error'</c> for an operation with the wrong number of fields.</summary>
    public static NatsProtocolViolationException WrongFieldCount(string operation, int count) =>
        new(NatsErrors.ParserError, $"{operation} carried {count} fields");
}
