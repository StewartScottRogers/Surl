using System.Globalization;
using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads <c>APPEND</c>'s <c>date-time</c> (RFC 3501, section 9): <c>dd-MMM-yyyy HH:mm:ss +hhmm</c>,
/// the day's first digit a space or a digit, the month's name in English, a zone of at most
/// 14 hours either way, and a moment that in UTC falls between the years 1 and 9999.
/// </summary>
internal static class ImapDateTime
{
    // "dd-MMM-yyyy HH:mm:ss" is 20 characters, then a space and "+hhmm".
    private const int DateLength = 20;
    private const int Length = DateLength + 6;

    private static readonly TimeSpan MaxZone = TimeSpan.FromHours(14);

    /// <summary>
    /// Parses a <c>date-time</c>'s content, without its quotes.
    /// </summary>
    /// <param name="value">The quoted string's bytes.</param>
    /// <returns>The date with its zone, or <see langword="null"/> when it is not a <c>date-time</c>.</returns>
    public static DateTimeOffset? Parse(byte[] value)
    {
        var text = Encoding.Latin1.GetString(value);
        return text.Length == Length && ReadDate(text[..DateLength]) is { } date && ReadZone(text[DateLength..]) is { } zone && IsInRange(date, zone)
            ? new DateTimeOffset(date, zone)
            : null;
    }

    // Whether the date in UTC is a date .NET can hold: 01-Jan-0001 00:00:00 +0100 is not.
    private static bool IsInRange(DateTime date, TimeSpan zone)
    {
        var utcTicks = date.Ticks - zone.Ticks;
        return utcTicks >= DateTime.MinValue.Ticks && utcTicks <= DateTime.MaxValue.Ticks;
    }

    private static DateTime? ReadDate(string text) =>
        text[0] is ' ' or (>= '0' and <= '9')
        && DateTime.TryParseExact(text.TrimStart(), "d-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    // " +hhmm" or " -hhmm".
    private static TimeSpan? ReadZone(string text)
    {
        var sign = text[1] switch { '+' => 1, '-' => -1, _ => 0 };
        return text[0] == ' ' && sign != 0 && int.TryParse(text.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var hoursAndMinutes)
            ? ToZone(sign, hoursAndMinutes / 100, hoursAndMinutes % 100)
            : null;
    }

    private static TimeSpan? ToZone(int sign, int hours, int minutes)
    {
        var zone = new TimeSpan(hours, minutes, 0);
        return minutes < 60 && zone <= MaxZone ? zone * sign : null;
    }
}
