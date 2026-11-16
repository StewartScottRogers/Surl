namespace Surl.Conformance;

/// <summary>
/// The upstream test cases played with a libtest program (ADR-0174 decisions 3 and 5), and how
/// the program's name is read from a case.
/// </summary>
public static class UpstreamTestCaseLibtests
{
    /// <summary>The file name of the one multi-program binary curl 8.21.0 links every libtest into.</summary>
    public const string ProgramFileName = "libtests";

    /// <summary>
    /// Gets the cases the runner admits so far: stage 1 of ADR-0174 decision 5, 2402, 2404, 2407
    /// (which runs <c>lib2405</c>) and 2502. Every other libtest case keeps skip reason <c>libtest</c>.
    /// </summary>
    public static IReadOnlySet<int> AdmittedNumbers { get; } = new HashSet<int> { 2402, 2404, 2407, 2502 };

    /// <summary>
    /// Reads the libtest program name a case runs: its <c>&lt;tool&gt;</c> with <c>%TESTNUMBER</c>
    /// replaced by <paramref name="number"/>, so <c>lib%TESTNUMBER</c> in test 2402 is <c>lib2402</c>.
    /// </summary>
    /// <param name="testCase">The parsed case.</param>
    /// <param name="number">The case's number.</param>
    /// <returns>The program name, or an empty string when the case names no tool.</returns>
    public static string ProgramName(UpstreamTestCase testCase, int number)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var tool = (testCase.FindPart("client", "tool")?.Lines ?? [])
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;

        return tool.Replace("%TESTNUMBER", number.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
