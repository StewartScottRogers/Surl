namespace Surl.Conformance;

/// <summary>
/// One main section of a test case - <c>&lt;info&gt;</c>, <c>&lt;reply&gt;</c>,
/// <c>&lt;client&gt;</c>, <c>&lt;disable&gt;</c> or <c>&lt;verify&gt;</c> - and its parts in
/// file order.
/// </summary>
/// <param name="Name">The tag name.</param>
/// <param name="Attributes">Its attributes by name.</param>
/// <param name="Parts">Its parts in file order; a name may repeat (several <c>&lt;file&gt;</c>).</param>
/// <param name="Lines">
/// The text lines directly inside the section, outside every part, with their newlines: the
/// items of <c>&lt;disable&gt;</c>, or a stray note such as test 1206's in its
/// <c>&lt;verify&gt;</c>, which upstream's <c>getpart</c> never reads.
/// </param>
public sealed record UpstreamTestCaseSection(
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<UpstreamTestCasePart> Parts,
    IReadOnlyList<string> Lines)
{
    /// <summary>
    /// Finds the first part named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The part's tag name.</param>
    /// <returns>The part, or <see langword="null"/> when the section has none.</returns>
    /// <remarks>
    /// Only the first: upstream's <c>getpart</c> joins the lines of every part of that name in the
    /// section, so a caller reading a name that may repeat walks <see cref="Parts"/> instead.
    /// </remarks>
    public UpstreamTestCasePart? FindPart(string name) =>
        Parts.FirstOrDefault(part => part.Name == name);
}
