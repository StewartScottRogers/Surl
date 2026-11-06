using System.Text;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The headers exchange's match (section 3.1.3; ADR-0112 decision 5): a binding's arguments table
/// against a message's <c>headers</c> property.
/// </summary>
/// <remarks>
/// <c>x-match</c>, a long string <c>all</c> (the default when absent) or <c>any</c>, says whether
/// every entry or at least one must match. Entries whose names begin <c>x-</c> are not matched. An
/// entry matches a header of the same name with an equal value, type octet and bytes alike; a void
/// entry matches the header's presence, whatever its value. With no entry to match, <c>all</c>
/// matches every message and <c>any</c> none.
/// </remarks>
internal static class Amqp0HeadersMatcher
{
    private const string MatchArgument = "x-match";
    private const string ReservedPrefix = "x-";

    /// <summary>Whether <paramref name="arguments"/> may bind: <c>x-match</c> absent, or a long string <c>all</c> or <c>any</c>.</summary>
    public static bool IsValidBinding(Amqp0FieldTable arguments) => MatchesAny(arguments) is not null;

    /// <summary>Whether a message with <paramref name="headers"/> matches a binding with <paramref name="arguments"/>.</summary>
    /// <param name="arguments">The binding's arguments, already found valid.</param>
    /// <param name="headers">The message's headers, <see langword="null"/> when it has none.</param>
    public static bool Matches(Amqp0FieldTable arguments, Amqp0FieldTable? headers)
    {
        var entries = arguments.Fields.Where(field => !field.Name.StartsWith(ReservedPrefix, StringComparison.Ordinal)).ToList();
        return MatchesAny(arguments) == true
            ? entries.Any(entry => EntryMatches(entry, headers))
            : entries.All(entry => EntryMatches(entry, headers));
    }

    // True for any, false for all, null for a value that is neither.
    private static bool? MatchesAny(Amqp0FieldTable arguments)
    {
        if (arguments.Find(MatchArgument) is not { } match)
        {
            return false;
        }

        var text = match.Type == Amqp0FieldValueType.LongString ? Encoding.UTF8.GetString((byte[])match.Value!) : null;
        return text switch
        {
            "all" => false,
            "any" => true,
            _ => null,
        };
    }

    private static bool EntryMatches(Amqp0Field entry, Amqp0FieldTable? headers) =>
        headers?.Find(entry.Name) is { } header
        && (entry.Value.Type == Amqp0FieldValueType.Void || Encode(entry.Value).AsSpan().SequenceEqual(Encode(header)));

    private static byte[] Encode(Amqp0FieldValue value)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteFieldValue(value);
        return writer.ToArray();
    }
}
