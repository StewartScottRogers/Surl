using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

[TestClass]
public sealed class CommandLineParserTests
{
    private const string Url = "http://127.0.0.1:8080/";

    private static readonly Dictionary<string, Func<SurlCommandLine, bool>> FlagValues = new(StringComparer.Ordinal)
    {
        ["verbose"] = c => c.Verbose,
        ["allow-uploads"] = c => c.AllowUploads,
        ["list-directories"] = c => c.ListDirectories,
        ["follow-symlinks"] = c => c.FollowSymlinks,
        ["serve-dot-files"] = c => c.ServeDotFiles,
    };

    [TestMethod]
    public void Parse_NullArguments_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.Parse(null!));

    [TestMethod]
    public void Parse_OnlyAListenUrl_ServesItWithEveryDefault()
    {
        var commandLine = Served(Url);

        CollectionAssert.AreEqual(new[] { new ListenUrl("http", "127.0.0.1", 8080) }, commandLine.ListenUrls.ToArray());
        Assert.AreEqual(new SurlCommandLine(), commandLine with { ListenUrls = [] });
    }

    [TestMethod]
    public void NewSurlCommandLine_HoldsTheAdrDefaults()
    {
        var defaults = new SurlCommandLine();

        Assert.AreEqual(0, defaults.ListenUrls.Count);
        Assert.AreEqual(".", defaults.ServedDirectory);
        Assert.IsFalse(defaults.Verbose);
        Assert.IsFalse(defaults.AllowUploads);
        Assert.IsFalse(defaults.ListDirectories);
        Assert.IsFalse(defaults.FollowSymlinks);
        Assert.IsFalse(defaults.ServeDotFiles);
        Assert.AreEqual(1024, defaults.MaxConnections);
        Assert.AreEqual(100, defaults.MaxConnectionsPerAddress);
        Assert.AreEqual(TimeSpan.FromSeconds(120), defaults.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(3600), defaults.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(30), defaults.HeadTimeout);
        Assert.AreEqual(102400L, defaults.MaxRequestHeadBytes);
        Assert.AreEqual(8192L, defaults.MaxLineBytes);
        Assert.AreEqual(1048576L, defaults.MaxMessageBytes);
        Assert.AreEqual(104857600L, defaults.MaxUploadBytes);
        Assert.AreEqual(SslProtocols.Tls12, defaults.LowestTlsVersion);
        Assert.AreEqual(SslProtocols.Tls13, defaults.HighestTlsVersion);
        Assert.IsNull(defaults.CertificateFile);
        Assert.IsNull(defaults.KeyFile);
        Assert.IsNull(defaults.CaCertificateFile);
    }

    [TestMethod]
    public void Parse_OnlyAListenUrl_HasTheCertificateFormatAndPassphraseDefaults()
    {
        var commandLine = Served(Url);

        Assert.AreEqual(CertificateFileFormat.Pem, commandLine.CertificateType);
        Assert.AreEqual(CertificateFileFormat.Pem, commandLine.KeyType);
        Assert.IsNull(commandLine.KeyPassphrase);
    }

    // Flags: -v/--verbose and the four exposure flags.

    [TestMethod]
    [DataRow("verbose", "--verbose")]
    [DataRow("verbose", "-v")]
    [DataRow("allow-uploads", "--allow-uploads")]
    [DataRow("list-directories", "--list-directories")]
    [DataRow("follow-symlinks", "--follow-symlinks")]
    [DataRow("serve-dot-files", "--serve-dot-files")]
    public void Parse_Flag_TurnsItOn(string longName, string written) =>
        Assert.IsTrue(FlagValues[longName](Served(written, Url)));

    [TestMethod]
    [DataRow("verbose")]
    [DataRow("allow-uploads")]
    [DataRow("list-directories")]
    [DataRow("follow-symlinks")]
    [DataRow("serve-dot-files")]
    public void Parse_NegatedAfterFlag_LaterWinsAndTurnsItOff(string longName)
    {
        Assert.IsFalse(FlagValues[longName](Served($"--{longName}", $"--no-{longName}", Url)));
        Assert.IsTrue(FlagValues[longName](Served($"--no-{longName}", $"--{longName}", Url)));
    }

    [TestMethod]
    [DataRow("--verbose=no")]
    [DataRow("--allow-uploads=no")]
    [DataRow("--list-directories=")]
    [DataRow("--follow-symlinks=yes")]
    [DataRow("--serve-dot-files=1")]
    [DataRow("--no-verbose=no")]
    [DataRow("--tlsv1.3=yes")]
    [DataRow("--help=all")]
    [DataRow("--version=1")]
    public void Parse_ValueOnAnOptionThatTakesNone_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: does not take a parameter");

    [TestMethod]
    [DataRow("--no-help")]
    [DataRow("--no-version")]
    [DataRow("--no-max-time")]
    [DataRow("--no-directory")]
    [DataRow("--no-tlsv1.2")]
    [DataRow("--no-tls-max")]
    [DataRow("--no-cert")]
    public void Parse_NoPrefixOnANonNegatableOption_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: the given option cannot be reversed with a --no- prefix");

    [TestMethod]
    public void Parse_ShortFlagsBundled_SetsEach()
    {
        var commandLine = Served("-vm30", Url);

        Assert.IsTrue(commandLine.Verbose);
        Assert.AreEqual(TimeSpan.FromSeconds(30), commandLine.MaxTime);
    }

    // <seconds>: --idle-timeout, -m/--max-time, --head-timeout.

    [TestMethod]
    [DataRow("--idle-timeout")]
    [DataRow("--max-time")]
    [DataRow("--head-timeout")]
    public void Parse_SecondsOption_AcceptedSeparateAndAfterEquals(string name)
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), SecondsOf(name, Served(name, "30", Url)));
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), SecondsOf(name, Served($"{name}=0.5", Url)));
    }

    [TestMethod]
    [DataRow("-m", "30")]
    [DataRow("-m30", null)]
    [DataRow("-vm", "30")]
    [DataRow("-vm30", null)]
    public void Parse_ShortMaxTime_AcceptedAttachedSeparateAndEndingABundle(string first, string? second)
    {
        string[] arguments = second is null ? [first, Url] : [first, second, Url];

        Assert.AreEqual(TimeSpan.FromSeconds(30), Served(arguments).MaxTime);
    }

    [TestMethod]
    [DataRow("0", -1L, DisplayName = "0 is no limit")]
    [DataRow("30", 300_000_000L)]
    [DataRow("0.5", 5_000_000L)]
    [DataRow("0030.250", 302_500_000L, DisplayName = "Leading and trailing zeros")]
    [DataRow("2147483.647", 21_474_836_470_000L, DisplayName = "Highest")]
    [DataRow("0.00000001", 1L, DisplayName = "Below a tick rounds up to one tick")]
    public void Parse_SecondsValue_BecomesTimeSpan(string argument, long ticks)
    {
        var expected = ticks < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromTicks(ticks);

        Assert.AreEqual(expected, Served("--max-time", argument, Url).MaxTime);
    }

    [TestMethod]
    [DataRow("--idle-timeout")]
    [DataRow("--max-time")]
    [DataRow("--head-timeout")]
    public void Parse_SecondsZero_IsNoLimit(string name) =>
        Assert.AreEqual(Timeout.InfiniteTimeSpan, SecondsOf(name, Served(name, "0", Url)));

    [TestMethod]
    [DataRow("-1", DisplayName = "Negative (row 29)")]
    [DataRow("+1", DisplayName = "Sign")]
    [DataRow("1,5", DisplayName = "Comma")]
    [DataRow("1e3", DisplayName = "Exponent")]
    [DataRow(".5", DisplayName = "No leading digit")]
    [DataRow("5.", DisplayName = "No digit after the point")]
    [DataRow("1.2.3", DisplayName = "Two points")]
    [DataRow("abc", DisplayName = "Word")]
    [DataRow(" 1", DisplayName = "Space")]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("2147483.648", DisplayName = "Above the highest")]
    [DataRow("99999999999999999999", DisplayName = "Too large (row 32)")]
    [DataRow("999999999999999999999999999999", DisplayName = "Beyond decimal")]
    public void Parse_BadSeconds_IsRefused(string argument)
    {
        foreach (var name in new[] { "--idle-timeout", "--max-time", "--head-timeout" })
        {
            AssertOptionRefused([name, argument, Url], $"option {name}: expected a proper numerical parameter");
        }

        AssertOptionRefused(["-m", argument, Url], "option -m: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_EmptyValueAfterEquals_IsRefusedNamingTheWrittenOption() =>
        AssertOptionRefused(["--max-time=", Url], "option --max-time=: expected a proper numerical parameter");

    [TestMethod]
    public void Parse_BadValueAfterEquals_NamesTheOptionAsWritten() =>
        AssertOptionRefused(["--max-time=abc", Url], "option --max-time=abc: expected a proper numerical parameter");

    [TestMethod]
    public void Parse_ShortAttachedArgumentStartingWithEquals_IsTakenLiterally() =>
        AssertOptionRefused(["-m=30", Url], "option -m=30: expected a proper numerical parameter");

    // <number>: --max-connections, --max-connections-per-address.

    [TestMethod]
    [DataRow("--max-connections")]
    [DataRow("--max-connections-per-address")]
    public void Parse_NumberOption_AcceptedSeparateAndAfterEquals(string name)
    {
        Assert.AreEqual(7, NumberOf(name, Served(name, "7", Url)));
        Assert.AreEqual(0, NumberOf(name, Served($"{name}=0", Url)));
        Assert.AreEqual(int.MaxValue, NumberOf(name, Served($"{name}=2147483647", Url)));
    }

    [TestMethod]
    [DataRow("2147483648")]
    [DataRow("-1")]
    [DataRow("1.5")]
    [DataRow("1k")]
    [DataRow("")]
    [DataRow("ten")]
    public void Parse_BadNumber_IsRefused(string argument)
    {
        foreach (var name in new[] { "--max-connections", "--max-connections-per-address" })
        {
            AssertOptionRefused([name, argument, Url], $"option {name}: expected a proper numerical parameter");
        }
    }

    // <bytes>: --max-request-head, --max-line, --max-message, --max-filesize.

    [TestMethod]
    [DataRow("--max-request-head")]
    [DataRow("--max-line")]
    [DataRow("--max-message")]
    [DataRow("--max-filesize")]
    public void Parse_BytesOption_AcceptedSeparateAndAfterEquals(string name)
    {
        Assert.AreEqual(102400L, BytesOf(name, Served(name, "100k", Url)));
        Assert.AreEqual(0L, BytesOf(name, Served($"{name}=0", Url)));
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("1", 1L)]
    [DataRow("100k", 102400L)]
    [DataRow("100K", 102400L)]
    [DataRow("2.5M", 2621440L)]
    [DataRow("1m", 1048576L)]
    [DataRow("1g", 1073741824L)]
    [DataRow("1G", 1073741824L)]
    [DataRow("1t", 1099511627776L)]
    [DataRow("1T", 1099511627776L)]
    [DataRow("1p", 1125899906842624L)]
    [DataRow("1P", 1125899906842624L)]
    [DataRow("1.5", 1L, DisplayName = "Truncated to whole bytes")]
    [DataRow("0.0001k", 0L, DisplayName = "Fraction of a byte truncates to 0")]
    [DataRow("9223372036854775807", long.MaxValue, DisplayName = "Highest")]
    public void Parse_BytesValue_BecomesByteCount(string argument, long expected) =>
        Assert.AreEqual(expected, Served("--max-filesize", argument, Url).MaxUploadBytes);

    [TestMethod]
    [DataRow("9223372036854775808", "too large number", DisplayName = "Above long.MaxValue")]
    [DataRow("99999999999999999999", "too large number", DisplayName = "Row 33")]
    [DataRow("999999999999999999999999999999", "too large number", DisplayName = "Beyond decimal")]
    [DataRow("8192p", "too large number", DisplayName = "Suffix takes it past long.MaxValue")]
    [DataRow("1q", "is badly used here", DisplayName = "Unknown suffix (row 35)")]
    [DataRow("1b", "is badly used here", DisplayName = "b is not a suffix")]
    [DataRow("1.", "is badly used here", DisplayName = "Trailing point")]
    [DataRow("1kk", "expected a proper numerical parameter", DisplayName = "Two suffixes")]
    [DataRow("k", "expected a proper numerical parameter", DisplayName = "Suffix alone")]
    [DataRow("abc", "expected a proper numerical parameter", DisplayName = "Word")]
    [DataRow("-1", "expected a proper numerical parameter", DisplayName = "Negative")]
    [DataRow("1,5k", "expected a proper numerical parameter", DisplayName = "Comma")]
    [DataRow("", "expected a proper numerical parameter", DisplayName = "Empty")]
    public void Parse_BadBytes_IsRefused(string argument, string reason)
    {
        foreach (var name in new[] { "--max-request-head", "--max-line", "--max-message", "--max-filesize" })
        {
            AssertOptionRefused([name, argument, Url], $"option {name}: {reason}");
        }
    }

    // <file> and <directory>: --directory, --cert, --key, --cacert.

    [TestMethod]
    [DataRow("--directory")]
    [DataRow("--cert")]
    [DataRow("--key")]
    [DataRow("--cacert")]
    public void Parse_PathOption_AcceptedSeparateAndAfterEquals_KeptAsGiven(string name)
    {
        // --cert comes first because --key needs it; a later --cert replaces it.
        Assert.AreEqual("some dir/file.pem", PathOf(name, Served("--cert", "c.pem", name, "some dir/file.pem", Url)));
        Assert.AreEqual("-leading-dash", PathOf(name, Served("--cert", "c.pem", name, "-leading-dash", Url)));
        Assert.AreEqual("a=b", PathOf(name, Served("--cert", "c.pem", $"{name}=a=b", Url)));
    }

    [TestMethod]
    [DataRow("--directory")]
    [DataRow("--cert")]
    [DataRow("--key")]
    [DataRow("--cacert")]
    public void Parse_EmptyPath_IsRefused(string name)
    {
        AssertOptionRefused([name, "", Url], $"option {name}: blank argument where content is expected");
        AssertOptionRefused([$"{name}=", Url], $"option {name}=: blank argument where content is expected");
    }

    // Certificate formats and passphrase: --cert-type, --key-type, --pass (ADR-0010 section 3).

    [TestMethod]
    [DataRow("pem", CertificateFileFormat.Pem)]
    [DataRow("PEM", CertificateFileFormat.Pem)]
    [DataRow("der", CertificateFileFormat.Der)]
    [DataRow("P12", CertificateFileFormat.Pkcs12)]
    [DataRow("p12", CertificateFileFormat.Pkcs12)]
    public void Parse_CertType_SetsTheCertificateType(string argument, CertificateFileFormat expected)
    {
        Assert.AreEqual(expected, Served("--cert", "c.pem", "--cert-type", argument, Url).CertificateType);
        Assert.AreEqual(expected, Served("--cert", "c.pem", $"--cert-type={argument}", Url).CertificateType);
    }

    [TestMethod]
    [DataRow("PEM", CertificateFileFormat.Pem)]
    [DataRow("der", CertificateFileFormat.Der)]
    public void Parse_KeyType_SetsTheKeyType(string argument, CertificateFileFormat expected) =>
        Assert.AreEqual(expected, Served("--cert", "c.pem", "--key", "k.pem", "--key-type", argument, Url).KeyType);

    [TestMethod]
    [DataRow("ENG")]
    [DataRow("PROV")]
    [DataRow("X")]
    [DataRow("")]
    public void Parse_BadCertType_IsRefused(string argument) =>
        AssertOptionRefused(["--cert", "c.pem", "--cert-type", argument, Url], "option --cert-type: is badly used here");

    [TestMethod]
    [DataRow("P12")]
    [DataRow("ENG")]
    [DataRow("")]
    public void Parse_BadKeyType_IsRefused(string argument) =>
        AssertOptionRefused(["--cert", "c.pem", "--key-type", argument, Url], "option --key-type: is badly used here");

    [TestMethod]
    [DataRow("secret")]
    [DataRow("")]
    [DataRow("-leading-dash")]
    public void Parse_Pass_SetsTheKeyPassphraseAsGiven(string argument)
    {
        Assert.AreEqual(argument, Served("--cert", "c.p12", "--pass", argument, Url).KeyPassphrase);
        Assert.AreEqual(argument, Served("--cert", "c.p12", $"--pass={argument}", Url).KeyPassphrase);
    }

    [TestMethod]
    [DataRow("--cert-type")]
    [DataRow("--key-type")]
    [DataRow("--pass")]
    public void Parse_CertificateFormatOrPassWithoutItsArgument_IsRefused(string written) =>
        AssertOptionRefused(["--cert", "c.pem", Url, written], $"option {written}: requires parameter");

    [TestMethod]
    [DataRow(new[] { "--key", "k.pem" }, "--key", DisplayName = "--key without --cert")]
    [DataRow(new[] { "--key-type", "PEM" }, "--key-type", DisplayName = "--key-type without --cert")]
    [DataRow(new[] { "--pass", "x" }, "--pass", DisplayName = "--pass without --cert")]
    [DataRow(new[] { "--pass", "" }, "--pass", DisplayName = "Empty --pass without --cert")]
    [DataRow(new[] { "--cert", "c.p12", "--cert-type", "P12", "--key", "k.pem" }, "--key", DisplayName = "--key with --cert-type P12")]
    [DataRow(new[] { "--key", "k.pem", "--cert-type", "p12", "--cert", "c.p12" }, "--key", DisplayName = "--key with --cert-type P12, any order")]
    [DataRow(new[] { "--pass", "x", "--key-type", "DER", "--key", "k.der" }, "--key", DisplayName = "All three without --cert: --key first")]
    [DataRow(new[] { "--pass", "x", "--key-type", "DER" }, "--key-type", DisplayName = "--key-type before --pass")]
    public void Parse_CertificateOptionThatCannotBeUsed_IsRefusedAfterReading(string[] options, string named) =>
        AssertOptionRefused([.. options, Url], $"option {named}: is badly used here");

    [TestMethod]
    public void Parse_CertTypeDerWithAKeyAndEveryOption_IsServed()
    {
        var commandLine = Served("--cert", "c.der", "--cert-type", "DER", "--key", "k.der", "--key-type", "DER", "--pass", "x", Url);

        Assert.AreEqual(CertificateFileFormat.Der, commandLine.CertificateType);
        Assert.AreEqual(CertificateFileFormat.Der, commandLine.KeyType);
        Assert.AreEqual("x", commandLine.KeyPassphrase);
    }

    [TestMethod]
    public void Parse_CertTypeWithoutCert_IsServed() =>
        Assert.AreEqual(CertificateFileFormat.Der, Served("--cert-type", "DER", Url).CertificateType);

    // TLS versions: --tlsv1.0 to --tlsv1.3 and --tls-max.

#pragma warning disable SYSLIB0039 // The old TLS versions are named on purpose.
    [TestMethod]
    [DataRow("--tlsv1.0", SslProtocols.Tls)]
    [DataRow("--tlsv1.1", SslProtocols.Tls11)]
    [DataRow("--tlsv1.2", SslProtocols.Tls12)]
    [DataRow("--tlsv1.3", SslProtocols.Tls13)]
    public void Parse_TlsVersionFlag_SetsTheLowestVersion(string written, SslProtocols expected) =>
        Assert.AreEqual(expected, Served(written, Url).LowestTlsVersion);

    [TestMethod]
    public void Parse_SeveralTlsVersionFlags_LastWins() =>
        Assert.AreEqual(SslProtocols.Tls, Served("--tlsv1.3", "--tlsv1.0", Url).LowestTlsVersion);

    [TestMethod]
    [DataRow("1.0", SslProtocols.Tls)]
    [DataRow("1.1", SslProtocols.Tls11)]
    [DataRow("1.2", SslProtocols.Tls12)]
    [DataRow("1.3", SslProtocols.Tls13)]
    public void Parse_TlsMax_SetsTheHighestVersion(string argument, SslProtocols expected)
    {
        Assert.AreEqual(expected, Served("--tlsv1.0", "--tls-max", argument, Url).HighestTlsVersion);
        Assert.AreEqual(expected, Served("--tlsv1.0", $"--tls-max={argument}", Url).HighestTlsVersion);
    }
#pragma warning restore SYSLIB0039

    [TestMethod]
    [DataRow("9", DisplayName = "Row 36")]
    [DataRow("1.4")]
    [DataRow("1")]
    [DataRow("1.20")]
    [DataRow("")]
    public void Parse_BadTlsMax_IsRefused(string argument) =>
        AssertOptionRefused(["--tls-max", argument, Url], "option --tls-max: is badly used here");

    [TestMethod]
    [DataRow("--tlsv1.3", "--tls-max", "1.2", DisplayName = "Row 37")]
    [DataRow("--tls-max", "1.1", "--tlsv1.2", DisplayName = "Default lowest above the highest")]
    [DataRow("--tls-max=1.2", "--tlsv1.3", Url, DisplayName = "Written with =, named without")]
    public void Parse_LowestTlsVersionAboveHighest_IsRefusedAfterReading(string first, string second, string third) =>
        AssertOptionRefused([first, second, third, Url], "option --tls-max: is badly used here");

    [TestMethod]
    public void Parse_LowestTlsVersionEqualToHighest_IsServed() =>
        Assert.AreEqual(SslProtocols.Tls13, Served("--tlsv1.3", "--tls-max", "1.3", Url).LowestTlsVersion);

    // Reading conventions and command-line errors.

    [TestMethod]
    [DataRow("--no-such", DisplayName = "Unknown long option (row 38)")]
    [DataRow("--silen", DisplayName = "Abbreviation (row 21)")]
    [DataRow("--verb", DisplayName = "Abbreviation of an option surl has")]
    [DataRow("--silent", DisplayName = "curl option surl leaves out")]
    [DataRow("--no-no-verbose", DisplayName = "--no-no- (row 18)")]
    [DataRow("--no-", DisplayName = "--no- alone")]
    [DataRow("--=x", DisplayName = "No name before =")]
    [DataRow("--VERBOSE", DisplayName = "Names are case-sensitive")]
    [DataRow("-~", DisplayName = "Unknown short option (row 22)")]
    [DataRow("-v~", DisplayName = "Unknown short option in a bundle")]
    [DataRow("-s", DisplayName = "curl short option surl leaves out")]
    [DataRow("-", DisplayName = "A lone - (row 23)")]
    public void Parse_UnknownOption_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: is unknown");

    [TestMethod]
    [DataRow("--max-time")]
    [DataRow("--directory")]
    [DataRow("--tls-max")]
    [DataRow("-m")]
    [DataRow("-vm", DisplayName = "Bundle missing its argument (row 26)")]
    public void Parse_OptionMissingItsArgument_IsRefused(string written) =>
        AssertOptionRefused([Url, written], $"option {written}: requires parameter");

    [TestMethod]
    public void Parse_OptionArgument_IsTheNextArgumentEvenWhenItStartsWithADash() =>
        AssertOptionRefused(["--max-time", "-v", Url], "option --max-time: expected a proper numerical parameter");

    [TestMethod]
    public void Parse_NoListenUrl_IsRefused()
    {
        AssertOptionRefused([], "(2) no URL specified");
        AssertOptionRefused(["-v", "--max-time", "5"], "(2) no URL specified");
    }

    [TestMethod]
    public void Parse_EmptyArgument_IsRefusedAsBlank()
    {
        AssertOptionRefused([""], "option : blank argument where content is expected");
        AssertOptionRefused([Url, "", "--no-such"], "option : blank argument where content is expected");
        AssertOptionRefused(["--", ""], "option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_SeveralListenUrls_KeepsCommandLineOrder()
    {
        var commandLine = Served("tftp://0.0.0.0:0", "-v", "http://[::1]:8080", "http://localhost/");

        CollectionAssert.AreEqual(
            new[]
            {
                new ListenUrl("tftp", "0.0.0.0", 0),
                new ListenUrl("http", "::1", 8080),
                new ListenUrl("http", "localhost", 80),
            },
            commandLine.ListenUrls.ToArray());
    }

    [TestMethod]
    public void Parse_OptionsAfterTheListenUrl_AreRead() =>
        Assert.IsTrue(Served(Url, "-v").Verbose);

    [TestMethod]
    public void Parse_RepeatedOption_LastWins() =>
        Assert.AreEqual(TimeSpan.FromSeconds(6), Served("-m", "5", Url, "--max-time", "6").MaxTime);

    [TestMethod]
    public void Parse_DoubleDashThenAListenUrl_ServesIt()
    {
        var commandLine = Served("-v", "--", Url);

        Assert.IsTrue(commandLine.Verbose);
        Assert.AreEqual(1, commandLine.ListenUrls.Count);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--help")]
    [DataRow("--")]
    public void Parse_DoubleDashThenADashArgument_ReadsItAsAListenUrl(string argument)
    {
        var failure = Refused(Url, "--", argument);

        Assert.AreEqual(SurlExitCode.MalformedUrl, failure.ExitCode);
        Assert.AreEqual("(3) URL rejected: Malformed input to a URL function", failure.Message);
        Assert.IsFalse(failure.FollowedByTryHelpLine);
    }

    [TestMethod]
    public void Parse_RefusedListenUrl_EndsReadingWithItsFailure()
    {
        var failure = Refused("NOSUCH://127.0.0.1/", "--no-such");

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, failure.ExitCode);
        Assert.AreEqual("(1) Protocol \"nosuch\" not supported", failure.Message);
        Assert.IsFalse(failure.FollowedByTryHelpLine);
    }

    // --help and --version.

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    [DataRow("-vh", DisplayName = "Ending a bundle")]
    [DataRow("-hV", DisplayName = "First of help and version wins")]
    public void Parse_Help_ShowsHelp(string written) =>
        Assert.AreSame(CommandLineParseResult.ShowHelp, CommandLineParser.Parse([written]));

    [TestMethod]
    [DataRow("--version")]
    [DataRow("-V")]
    [DataRow("-vV", DisplayName = "Ending a bundle")]
    [DataRow("-Vh", DisplayName = "First of help and version wins")]
    public void Parse_Version_ShowsVersion(string written) =>
        Assert.AreSame(CommandLineParseResult.ShowVersion, CommandLineParser.Parse([written]));

    [TestMethod]
    public void Parse_HelpOrVersionBeforeAnError_EndsReadingWhereItStands()
    {
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["-V", "--no-such"]).Outcome);
        Assert.AreEqual(CommandLineOutcome.ShowHelp, CommandLineParser.Parse([Url, "--help", "--max-time", "x"]).Outcome);
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["--version", "--help"]).Outcome);
    }

    [TestMethod]
    public void Parse_ErrorBeforeHelpOrVersion_IsReported()
    {
        AssertOptionRefused(["--no-such", "-V"], "option --no-such: is unknown");
        AssertOptionRefused(["-m", "x", "--help"], "option -m: expected a proper numerical parameter");
    }

    [TestMethod]
    public void ShowHelpAndShowVersion_CarryNeitherCommandLineNorFailure()
    {
        foreach (var result in new[] { CommandLineParseResult.ShowHelp, CommandLineParseResult.ShowVersion })
        {
            Assert.IsNull(result.CommandLine);
            Assert.IsNull(result.Failure);
        }
    }

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.IsNull(result.Failure);
        Assert.IsNotNull(result.CommandLine);
        return result.CommandLine;
    }

    private static CommandLineFailure Refused(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.IsNull(result.CommandLine);
        Assert.IsNotNull(result.Failure);
        return result.Failure;
    }

    private static void AssertOptionRefused(string[] arguments, string message)
    {
        var failure = Refused(arguments);

        Assert.AreEqual(SurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
        Assert.IsTrue(failure.FollowedByTryHelpLine);
    }

    private static TimeSpan SecondsOf(string name, SurlCommandLine commandLine) => name switch
    {
        "--idle-timeout" => commandLine.IdleTimeout,
        "--max-time" => commandLine.MaxTime,
        _ => commandLine.HeadTimeout,
    };

    private static int NumberOf(string name, SurlCommandLine commandLine) =>
        name == "--max-connections" ? commandLine.MaxConnections : commandLine.MaxConnectionsPerAddress;

    private static long BytesOf(string name, SurlCommandLine commandLine) => name switch
    {
        "--max-request-head" => commandLine.MaxRequestHeadBytes,
        "--max-line" => commandLine.MaxLineBytes,
        "--max-message" => commandLine.MaxMessageBytes,
        _ => commandLine.MaxUploadBytes,
    };

    private static string? PathOf(string name, SurlCommandLine commandLine) => name switch
    {
        "--directory" => commandLine.ServedDirectory,
        "--cert" => commandLine.CertificateFile,
        "--key" => commandLine.KeyFile,
        _ => commandLine.CaCertificateFile,
    };
}
