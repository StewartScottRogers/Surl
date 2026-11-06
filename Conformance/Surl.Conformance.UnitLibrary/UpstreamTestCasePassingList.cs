using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A platform's passing list (ADR-0080 decision 5):
/// <c>Documentation/Conformance/upstream-test-cases-passing.&lt;platform&gt;.txt</c>, the numbers of
/// the upstream test cases that pass against Surl on that platform's reference build, one per line
/// in ascending order. The suite's guard fails when a listed case stops passing.
/// </summary>
public static class UpstreamTestCasePassingList
{
    /// <summary>
    /// The folder, relative to the repository root, that holds the passing lists and the
    /// pass-rate file.
    /// </summary>
    public const string Folder = "Documentation/Conformance";

    /// <summary>
    /// The name of a platform's passing list: <c>upstream-test-cases-passing.&lt;platform&gt;.txt</c>.
    /// </summary>
    /// <param name="platform">The pin's platform, such as <c>win-x64</c>.</param>
    /// <returns>The file name.</returns>
    public static string FileNameFor(string platform) => $"upstream-test-cases-passing.{platform}.txt";

    /// <summary>
    /// Reads a passing list: one case number per line, ascending, no repeats. Blank lines are
    /// ignored, and a carriage return before a line feed is allowed.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The case numbers, ascending.</returns>
    /// <exception cref="FormatException">
    /// A line is not a number, or a number is not greater than the one before it.
    /// </exception>
    public static IReadOnlyList<int> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var numbers = new List<int>();
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"Line {lineNumber} of the passing list is not a case number: '{trimmed}'.");
            }

            if (numbers.Count > 0 && number <= numbers[^1])
            {
                throw new FormatException($"Line {lineNumber} of the passing list, {number}, does not follow {numbers[^1]} in ascending order.");
            }

            numbers.Add(number);
        }

        return numbers;
    }

    /// <summary>
    /// Writes a passing list's text: the numbers of the passing results, ascending, each on its
    /// own line ending with a line feed.
    /// </summary>
    /// <param name="results">A run's results.</param>
    /// <returns>The text.</returns>
    public static string Format(IEnumerable<UpstreamTestCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var text = new StringBuilder();
        foreach (var number in results.Where(result => result.Outcome == UpstreamTestCaseOutcome.Pass).Select(result => result.Number).Order())
        {
            text.Append(number.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return text.ToString();
    }
}
