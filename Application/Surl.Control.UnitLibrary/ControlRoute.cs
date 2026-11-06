using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// One request the control endpoint answers: its method, its path under <c>/v1/</c> as segments - a literal,
/// <c>{}</c> for one segment or <c>{*}</c> for the rest (at least one) - the query keys it takes, and its answer.
/// </summary>
/// <param name="Method">The method, such as <c>GET</c>.</param>
/// <param name="Template">The path's segments after <c>v1</c>.</param>
/// <param name="QueryKeys">The query keys the request takes.</param>
/// <param name="Answer">Answers a matched request.</param>
internal sealed record ControlRoute(
    string Method,
    string[] Template,
    string[] QueryKeys,
    Func<ControlRequest, Task<HttpAnswer>> Answer)
{
    private const string OneSegment = "{}";
    private const string Rest = "{*}";

    /// <summary>
    /// The decoded segments the template captures from <paramref name="segments"/>, or <see langword="null"/> when
    /// the path is not this route's.
    /// </summary>
    /// <param name="segments">The path's raw segments after <c>v1</c>.</param>
    /// <returns>The captured segments, a <c>{*}</c> joined with <c>/</c>.</returns>
    public IReadOnlyList<string>? Match(string[] segments)
    {
        var hasRest = Template[^1] == Rest;
        var fixedLength = hasRest ? Template.Length - 1 : Template.Length;
        var hasLength = hasRest ? segments.Length > fixedLength : segments.Length == fixedLength;
        var parameters = hasLength ? FixedParameters(segments, fixedLength) : null;

        return parameters is not null && hasRest ? WithRest(parameters, segments[fixedLength..]) : parameters;
    }

    // The parameters of the template's segments before any {*}, or null when a segment does not match.
    private List<string>? FixedParameters(string[] segments, int fixedLength)
    {
        var parameters = new List<string>();
        for (var index = 0; index < fixedLength; index++)
        {
            if (!MatchesSegment(Template[index], segments[index], parameters))
            {
                return null;
            }
        }

        return parameters;
    }

    private static bool MatchesSegment(string templateSegment, string segment, List<string> parameters)
    {
        if (templateSegment != OneSegment)
        {
            return templateSegment == segment;
        }

        parameters.Add(Uri.UnescapeDataString(segment));

        return segment.Length > 0;
    }

    // The rest of the path, decoded segment by segment and joined with '/'; an empty rest is no match.
    private static List<string>? WithRest(List<string> parameters, string[] restSegments)
    {
        var rest = string.Join('/', restSegments.Select(Uri.UnescapeDataString));
        if (rest.Length == 0)
        {
            return null;
        }

        parameters.Add(rest);

        return parameters;
    }
}
