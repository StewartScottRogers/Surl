namespace Surl.Conformance;

/// <summary>
/// The outcome of checking a test case's <c>&lt;verify&gt;</c> section: pass, or fail with the
/// first section that differs and a readable difference.
/// </summary>
public sealed class UpstreamTestCaseVerifyResult
{
    private UpstreamTestCaseVerifyResult(string? failedSection, string? difference)
    {
        FailedSection = failedSection;
        Difference = difference;
    }

    /// <summary>
    /// Gets the result of a run every checked section agrees with.
    /// </summary>
    public static UpstreamTestCaseVerifyResult Pass { get; } = new(null, null);

    /// <summary>
    /// Gets a value indicating whether every checked section agreed.
    /// </summary>
    public bool Passed => FailedSection is null;

    /// <summary>
    /// Gets the first section that differed, such as <c>protocol</c> or <c>file1 (log/out.txt)</c>,
    /// or <see langword="null"/> on a pass.
    /// </summary>
    public string? FailedSection { get; }

    /// <summary>
    /// Gets how it differed - the first differing line of each side with its bytes escaped as
    /// NFR-020 escapes them - or <see langword="null"/> on a pass.
    /// </summary>
    public string? Difference { get; }

    /// <summary>
    /// Creates a failure.
    /// </summary>
    /// <param name="section">The section that differed.</param>
    /// <param name="difference">How it differed.</param>
    /// <returns>The failure.</returns>
    public static UpstreamTestCaseVerifyResult Fail(string section, string difference)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(difference);

        return new UpstreamTestCaseVerifyResult(section, difference);
    }

    /// <summary>
    /// Describes the result in one line: <c>pass</c>, or <c>fail</c>, the section and the difference.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() => Passed ? "pass" : $"fail {FailedSection}: {Difference}";
}
