namespace Surl.Conformance;

/// <summary>
/// An upstream curl test case, preprocessed and read into its sections and parts
/// (FILEFORMAT.md's <c>&lt;testcase&gt;</c>).
/// </summary>
/// <param name="Attributes">The <c>&lt;testcase&gt;</c> tag's attributes by name.</param>
/// <param name="Sections">The main sections in file order.</param>
public sealed record UpstreamTestCase(
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<UpstreamTestCaseSection> Sections)
{
    /// <summary>
    /// Finds the first section named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The section's tag name, such as <c>reply</c>.</param>
    /// <returns>The section, or <see langword="null"/> when the case has none.</returns>
    public UpstreamTestCaseSection? FindSection(string name) =>
        Sections.FirstOrDefault(section => section.Name == name);

    /// <summary>
    /// Finds the first part named <paramref name="part"/> in the first section named
    /// <paramref name="section"/>.
    /// </summary>
    /// <param name="section">The section's tag name.</param>
    /// <param name="part">The part's tag name.</param>
    /// <returns>The part, or <see langword="null"/> when there is none.</returns>
    public UpstreamTestCasePart? FindPart(string section, string part) =>
        FindSection(section)?.FindPart(part);
}
