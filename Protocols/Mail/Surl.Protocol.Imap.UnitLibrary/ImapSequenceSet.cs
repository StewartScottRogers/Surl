using System.Globalization;

namespace Surl.Protocol.Imap;

/// <summary>
/// A <c>sequence-set</c> (RFC 3501, section 9; ADR-0055, decision 4): numbers, <c>n:m</c> ranges
/// in either order and <c>*</c>, the highest number or UID, joined by commas.
/// </summary>
internal sealed class ImapSequenceSet
{
    // Each range's two ends; 0 stands for "*".
    private readonly IReadOnlyList<(uint First, uint Last)> ranges;

    private ImapSequenceSet(IReadOnlyList<(uint First, uint Last)> ranges)
    {
        this.ranges = ranges;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a byte a sequence set holds: a digit, <c>:</c>,
    /// <c>,</c> or <c>*</c>.
    /// </summary>
    /// <param name="value">A byte of the command.</param>
    /// <returns>Whether a sequence set may hold it.</returns>
    public static bool IsSequenceByte(byte value) => char.IsAsciiDigit((char)value) || value is (byte)':' or (byte)',' or (byte)'*';

    /// <summary>
    /// Reads a space, then a sequence set.
    /// </summary>
    /// <param name="arguments">The command, just before the space.</param>
    /// <returns>The set, or <see langword="null"/> when either is not next.</returns>
    public static ImapSequenceSet? ReadSpaced(ImapArguments arguments) =>
        arguments.TryReadSpace() && arguments.ReadRun(IsSequenceByte) is { } text ? Parse(text) : null;

    /// <summary>
    /// Parses <paramref name="text"/> as a sequence set.
    /// </summary>
    /// <param name="text">The bytes <see cref="IsSequenceByte"/> accepts, as text.</param>
    /// <returns>The set, or <see langword="null"/> when the text is not one: an empty member, a
    /// 0, a leading zero, a number past 32 bits, or a range of more than two ends.</returns>
    public static ImapSequenceSet? Parse(string text)
    {
        List<(uint First, uint Last)> ranges = [];
        foreach (var member in text.Split(','))
        {
            if (ParseRange(member) is not { } range)
            {
                return null;
            }

            ranges.Add(range);
        }

        return new ImapSequenceSet(ranges);
    }

    /// <summary>
    /// Whether <paramref name="value"/> is in the set.
    /// </summary>
    /// <param name="value">A message number or UID.</param>
    /// <param name="highest">What <c>*</c> stands for: the highest number or UID.</param>
    /// <returns>Whether a member names it.</returns>
    public bool Contains(uint value, uint highest) =>
        ranges.Any(range => Math.Min(Resolve(range.First, highest), Resolve(range.Last, highest)) <= value
            && value <= Math.Max(Resolve(range.First, highest), Resolve(range.Last, highest)));

    /// <summary>
    /// Whether every number the set names is a message of a mailbox of <paramref name="count"/>
    /// messages: none past <paramref name="count"/>, and <c>*</c> only when there is a message.
    /// </summary>
    /// <param name="count">How many messages the session sees.</param>
    /// <returns>Whether the set names only messages that exist.</returns>
    public bool IsWithin(uint count) =>
        ranges.All(range => IsNumberOf(Resolve(range.First, count), count) && IsNumberOf(Resolve(range.Last, count), count));

    /// <summary>
    /// Writes numbers as a set, each run of consecutive numbers as <c>n:m</c>, for the UID sets
    /// of <c>COPYUID</c> (RFC 4315, section 3): the n-th number of one set pairs with the n-th of
    /// the other, so the order given is kept.
    /// </summary>
    /// <param name="numbers">The numbers, at least one, in the order they pair.</param>
    /// <returns>The set, e.g. <c>1:3,7</c>.</returns>
    public static string Format(IReadOnlyList<uint> numbers)
    {
        List<string> members = [];
        var first = 0;
        for (var index = 1; index <= numbers.Count; index++)
        {
            if (index == numbers.Count || numbers[index] != numbers[index - 1] + 1)
            {
                members.Add(FormatRun(numbers[first], numbers[index - 1]));
                first = index;
            }
        }

        return string.Join(',', members);
    }

    private static string FormatRun(uint first, uint last) =>
        first == last
            ? first.ToString(CultureInfo.InvariantCulture)
            : first.ToString(CultureInfo.InvariantCulture) + ":" + last.ToString(CultureInfo.InvariantCulture);

    private static bool IsNumberOf(uint number, uint count) => number > 0 && number <= count;

    private static uint Resolve(uint end, uint highest) => end == 0 ? highest : end;

    // "n" or "n:m"; a lone number is a range of one.
    private static (uint First, uint Last)? ParseRange(string member)
    {
        var ends = member.Split(':');
        return ends.Length <= 2 && ParseEnd(ends[0]) is { } first && ParseEnd(ends[^1]) is { } last ? (first, last) : null;
    }

    // An nz-number, or "*" as 0.
    private static uint? ParseEnd(string end) =>
        end == "*" ? 0
        : end.Length > 0 && end[0] != '0' && uint.TryParse(end, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number
        : null;
}
