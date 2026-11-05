using System.Globalization;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Reads a Streamable HTTP <c>POST</c>'s <c>Accept</c> field (ADR-0111 decision 4.3): it must list both
/// <c>application/json</c> and <c>text/event-stream</c>. A media range counts for a type when it names it, its
/// top-level type with <c>/*</c>, or <c>*/*</c>; the most specific range naming a type decides, and its
/// <c>q=0</c> excludes it (RFC 9110 section 12.5.1).
/// </summary>
internal static class McpAcceptField
{
    /// <summary>Whether the <c>Accept</c> value accepts both of the types Streamable HTTP answers with.</summary>
    /// <param name="accept">Every <c>Accept</c> field's value, joined with commas, or <see langword="null" /> when none was sent.</param>
    /// <returns><see langword="true" /> when both <c>application/json</c> and <c>text/event-stream</c> are acceptable.</returns>
    public static bool AcceptsJsonAndEventStream(string? accept)
    {
        if (accept is null)
        {
            return false;
        }

        var ranges = accept.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(MediaRange.Parse)
            .ToArray();
        return Accepts(ranges, "application", "json") && Accepts(ranges, "text", "event-stream");
    }

    private static bool Accepts(MediaRange[] ranges, string type, string subtype)
    {
        var best = ranges
            .Select(range => (Range: range, Specificity: range.SpecificityFor(type, subtype)))
            .Where(match => match.Specificity > 0)
            .OrderByDescending(match => match.Specificity)
            .FirstOrDefault();
        return best.Specificity > 0 && best.Range.Quality > 0;
    }

    private sealed record MediaRange(string Type, string Subtype, decimal Quality)
    {
        public static MediaRange Parse(string text)
        {
            var parts = text.Split(';', StringSplitOptions.TrimEntries);
            var slash = parts[0].IndexOf('/', StringComparison.Ordinal);
            var type = slash < 0 ? parts[0] : parts[0][..slash];
            var subtype = slash < 0 ? string.Empty : parts[0][(slash + 1)..];
            return new MediaRange(type, subtype, QualityOf(parts.Skip(1)));
        }

        // 3 names the type exactly, 2 its top-level type with /*, 1 is */*, 0 does not name it.
        public int SpecificityFor(string type, string subtype)
        {
            if (Type == "*" && Subtype == "*")
            {
                return 1;
            }

            if (!Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return Subtype == "*" ? 2 : Subtype.Equals(subtype, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        }

        // A weight that does not parse is taken as 1, the default, rather than refusing the whole field.
        private static decimal QualityOf(IEnumerable<string> parameters)
        {
            var weight = parameters.FirstOrDefault(parameter => parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase));
            return weight is not null && decimal.TryParse(weight[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quality)
                ? quality
                : 1m;
        }
    }
}
