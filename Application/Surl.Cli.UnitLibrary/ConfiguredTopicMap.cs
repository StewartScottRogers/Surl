namespace Surl.Cli;

/// <summary>
/// One <c>--topic-map &lt;scheme&gt;:&lt;name&gt;=&lt;scheme&gt;:&lt;name&gt;</c> (ADR-0101 decision 4, ADR-0102 decision
/// 3.3): the left side's name on its scheme is the canonical topic the right side's name has on its scheme.
/// The record holds what was written, percent-decoded, each scheme lower-cased. Whether a registered server
/// speaks each scheme, and whether each name is valid there, is <c>Surl.Composition</c>'s to check (BL-991).
/// </summary>
/// <param name="FromScheme">The left side's scheme, lower-cased, one some surl server speaks.</param>
/// <param name="FromName">The left side's name on <paramref name="FromScheme"/>, never empty.</param>
/// <param name="ToScheme">The right side's scheme, lower-cased, one some surl server speaks.</param>
/// <param name="ToName">The right side's name on <paramref name="ToScheme"/>, never empty.</param>
public sealed record ConfiguredTopicMap(string FromScheme, string FromName, string ToScheme, string ToName)
{
    /// <summary>The left side, <c>&lt;scheme&gt;:&lt;name&gt;</c>: what two maps may not share (ADR-0101 decision 5).</summary>
    public string Source => FromScheme + ":" + FromName;
}
