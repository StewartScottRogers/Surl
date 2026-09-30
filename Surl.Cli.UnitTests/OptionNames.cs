using System.Text.RegularExpressions;

namespace Surl.Cli;

/// <summary>
/// Finds the option names a help or manual text writes (<c>--name</c>, <c>--no-name</c>,
/// <c>-x</c>) and asserts each one is in <see cref="CommandLineOptions.All"/>.
/// </summary>
internal static partial class OptionNames
{
    // Options of upstream curl's the texts name as curl's, never as surl's.
    private static readonly string[] CurlOptionsNamedAsCurls = ["--digest", "-k"];

    /// <summary>Asserts that every option name <paramref name="text"/> writes is one surl has.</summary>
    /// <param name="text">The help or manual text.</param>
    public static void AssertEveryNamedOptionExists(string text)
    {
        var longNames = LongOption().Matches(text).Select(match => match.Groups["name"].Value).ToArray();
        var shortNames = ShortOption().Matches(text).Select(match => match.Groups["name"].Value[0]).ToArray();
        Assert.IsNotEmpty(longNames);

        foreach (var name in longNames.Where(name => !CurlOptionsNamedAsCurls.Contains("--" + name)))
        {
            Assert.IsTrue(CommandLineOptions.TryFindLong(name, out _), "--" + name);
        }

        foreach (var name in shortNames.Where(name => !CurlOptionsNamedAsCurls.Contains("-" + name)))
        {
            Assert.IsTrue(CommandLineOptions.TryFindShort(name, out _), "-" + name);
        }
    }

    [GeneratedRegex(@"(?<![\w-])--(?:no-)?(?<name>[a-z0-9][a-z0-9.-]*[a-z0-9])")]
    private static partial Regex LongOption();

    [GeneratedRegex(@"(?<![\w-])-(?<name>[A-Za-z])(?![\w-])")]
    private static partial Regex ShortOption();
}
