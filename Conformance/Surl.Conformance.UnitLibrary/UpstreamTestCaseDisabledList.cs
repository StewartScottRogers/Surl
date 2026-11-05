using System.Globalization;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Reads upstream's <c>tests/data/DISABLED</c>: the test cases upstream does not run, which
/// Surl skips as <see cref="UpstreamTestCaseSkipReasons.DisabledUpstream"/>.
/// </summary>
/// <remarks>
/// Read as runtests' <c>disabledtests</c> reads it: a line starting with <c>#</c> (after spaces)
/// is a comment, the <c>%if</c> lines and variables are preprocessed against the build's
/// features, and each other line's first number is a disabled case.
/// </remarks>
public static class UpstreamTestCaseDisabledList
{
    /// <summary>
    /// Parses the bytes of a <c>DISABLED</c> file.
    /// </summary>
    /// <param name="source">The file's bytes.</param>
    /// <param name="environment">The features its <c>%if</c> lines test.</param>
    /// <returns>The numbers of the disabled cases.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">
    /// A line that is not blank and not a comment holds no number, which runtests stops on.
    /// </exception>
    public static IReadOnlySet<int> Parse(byte[] source, UpstreamTestCaseEnvironment environment)
    {
        var disabled = new HashSet<int>();

        foreach (var line in UpstreamTestCasePreprocessor.Preprocess(source, environment))
        {
            var text = line.Text.Trim();

            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var number = FirstNumber.Match(text);

            if (!number.Success)
            {
                throw new UpstreamTestCaseFormatException(line.SourceLineNumber, $"DISABLED holds no test number on '{text}'");
            }

            disabled.Add(int.Parse(number.Value, NumberStyles.None, CultureInfo.InvariantCulture));
        }

        return disabled;
    }

    private static readonly Regex FirstNumber = new(@"\d+", RegexOptions.CultureInvariant);
}
