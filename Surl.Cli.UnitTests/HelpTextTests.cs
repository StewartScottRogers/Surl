namespace Surl.Cli;

[TestClass]
public sealed class HelpTextTests
{
    private const int DescriptionColumn = 46;

    [TestMethod]
    public void Text_IsExactlyTheAdrHelp()
    {
        var nl = Environment.NewLine;
        var expected =
            "Usage: surl [options...] <url>..." + nl +
            "     --allow-uploads                         Accept uploads into the served directory" + nl +
            "     --cacert <file>                         CA certificates that verify client certificates" + nl +
            "     --cert <file>                           Server certificate for secure schemes" + nl +
            "     --directory <directory>                 Directory to serve (default: current directory)" + nl +
            "     --follow-symlinks                       Follow links that stay inside the directory" + nl +
            "     --head-timeout <seconds>                Time a peer has to send a request head (default 30)" + nl +
            " -h, --help                                  Show this help and quit" + nl +
            "     --idle-timeout <seconds>                Close an exchange idle this long (default 120)" + nl +
            "     --key <file>                            Private key for --cert" + nl +
            "     --list-directories                      Answer directory listings" + nl +
            "     --max-connections <number>              Connections at once, all listeners (default 1024)" + nl +
            "     --max-connections-per-address <number>  Connections at once from one address (default 100)" + nl +
            "     --max-filesize <bytes>                  Largest upload accepted (default 100M)" + nl +
            "     --max-line <bytes>                      Longest command line accepted (default 8192)" + nl +
            "     --max-message <bytes>                   Largest framed message accepted (default 1M)" + nl +
            "     --max-request-head <bytes>              Largest HTTP or RTSP request head (default 100k)" + nl +
            " -m, --max-time <seconds>                    Longest time one exchange may take (default 3600)" + nl +
            "     --serve-dot-files                       Serve names that start with a dot" + nl +
            "     --tls-max <version>                     Highest TLS version accepted (default 1.3)" + nl +
            "     --tlsv1.0                               Accept TLS 1.0 or later" + nl +
            "     --tlsv1.1                               Accept TLS 1.1 or later" + nl +
            "     --tlsv1.2                               Accept TLS 1.2 or later (default)" + nl +
            "     --tlsv1.3                               Accept TLS 1.3 or later" + nl +
            " -v, --verbose                               Log every exchange to stderr" + nl +
            " -V, --version                               Show version number and quit" + nl;

        Assert.AreEqual(expected, HelpText.Text);
    }

    [TestMethod]
    public void Text_EveryOptionLine_StartsItsDescriptionInColumn46WithNoTrailingSpace()
    {
        var optionLines = OptionLines();

        foreach (var line in optionLines)
        {
            Assert.AreEqual(' ', line[DescriptionColumn - 2], line);
            Assert.AreNotEqual(' ', line[DescriptionColumn - 1], line);
            Assert.AreEqual(line.TrimEnd(), line);
        }
    }

    [TestMethod]
    public void Text_ListsEveryOptionInTheTable_WithItsShortName()
    {
        var optionLines = OptionLines();

        Assert.AreEqual(CommandLineOptions.All.Count, optionLines.Length);
        foreach (var option in CommandLineOptions.All)
        {
            var shortPart = option.ShortName is null ? "    " : $" -{option.ShortName},";
            Assert.IsTrue(
                optionLines.Any(line => line.StartsWith($"{shortPart} --{option.LongName} ", StringComparison.Ordinal)),
                option.LongName);
        }
    }

    private static string[] OptionLines() =>
        HelpText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
}
