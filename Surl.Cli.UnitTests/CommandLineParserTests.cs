using System.Security.Authentication;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

[TestClass]
public sealed class CommandLineParserTests
{
    private const string Url = "http://127.0.0.1:8080/";

    private static readonly Dictionary<string, Func<SurlCommandLine, bool>> FlagValues = new(StringComparer.Ordinal)
    {
        ["verbose"] = c => c.LogLevel == LogLevel.Verbose,
        ["allow-uploads"] = c => c.AllowUploads,
        ["list-directories"] = c => c.ListDirectories,
        ["follow-symlinks"] = c => c.FollowSymlinks,
        ["serve-dot-files"] = c => c.ServeDotFiles,
        ["allow-anonymous"] = c => c.AllowAnonymous,
        ["allow-plaintext-auth"] = c => c.AllowPlaintextAuthentication,
        ["self-signed"] = c => c.SelfSigned,
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
    public void NewSurlCommandLine_LimitsAreExchangeLimitsDefault() =>
        Assert.AreEqual(ExchangeLimits.Default, new SurlCommandLine().Limits);

    [TestMethod]
    public void Parse_OneLimitOption_LeavesTheOtherLimitsAtTheirDefaults() =>
        Assert.AreEqual(
            ExchangeLimits.Default with { MaxLineBytes = 16 },
            Served("--max-line", "16", Url).Limits);

    [TestMethod]
    public void NewSurlCommandLine_HoldsTheAdrDefaults()
    {
        var defaults = new SurlCommandLine();

        Assert.AreEqual(0, defaults.ListenUrls.Count);
        Assert.IsNull(defaults.DataDirectory);
        Assert.IsFalse(defaults.AllowUploads);
        Assert.IsFalse(defaults.ListDirectories);
        Assert.IsFalse(defaults.FollowSymlinks);
        Assert.IsFalse(defaults.ServeDotFiles);
        Assert.AreEqual(1024, defaults.MaxConnections);
        Assert.AreEqual(100, defaults.MaxConnectionsPerAddress);
        Assert.AreEqual(TimeSpan.FromSeconds(120), defaults.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(3600), defaults.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(30), defaults.Limits.HeadTimeout);
        Assert.AreEqual(102400L, defaults.Limits.MaxRequestHeadBytes);
        Assert.AreEqual(8192L, defaults.Limits.MaxLineBytes);
        Assert.AreEqual(1048576L, defaults.Limits.MaxMessageBytes);
        Assert.AreEqual(104857600L, defaults.Limits.MaxUploadBytes);
        Assert.AreEqual(SslProtocols.Tls12, defaults.LowestTlsVersion);
        Assert.AreEqual(SslProtocols.Tls13, defaults.HighestTlsVersion);
        Assert.IsNull(defaults.CertificateFile);
        Assert.IsNull(defaults.KeyFile);
        Assert.IsNull(defaults.CaCertificateFile);
        Assert.AreEqual(LogLevel.Info, defaults.LogLevel);
        Assert.IsFalse(defaults.ShowError);
        Assert.IsNull(defaults.TraceFile);
        Assert.AreEqual(TraceDumpLayout.HexAndAscii, defaults.TraceLayout);
        Assert.IsFalse(defaults.TraceTime);
        Assert.IsNull(defaults.LogFile);
    }

    [TestMethod]
    public void Parse_NoLoggingOption_HasTheInfoLevelAndNoTraceOrLogFile()
    {
        var commandLine = Served(Url);

        Assert.AreEqual(LogLevel.Info, commandLine.LogLevel);
        Assert.IsNull(commandLine.TraceFile);
        Assert.IsFalse(commandLine.TraceTime);
        Assert.IsNull(commandLine.LogFile);
    }

    // Log levels: -s, -S, -v, --log-level, --trace and --trace-ascii (ADR-0033 section 2).

    [TestMethod]
    [DataRow(LogLevel.None, new[] { "-s" }, DisplayName = "-s")]
    [DataRow(LogLevel.None, new[] { "--silent" }, DisplayName = "--silent")]
    [DataRow(LogLevel.Error, new[] { "-s", "-S" }, DisplayName = "-s -S")]
    [DataRow(LogLevel.Error, new[] { "-sS" }, DisplayName = "-sS")]
    [DataRow(LogLevel.Error, new[] { "-S", "-s" }, DisplayName = "-S -s: -S applies after the whole line")]
    [DataRow(LogLevel.Error, new[] { "--silent", "--show-error" }, DisplayName = "--silent --show-error")]
    [DataRow(LogLevel.None, new[] { "-s", "-S", "--no-show-error" }, DisplayName = "--no-show-error")]
    [DataRow(LogLevel.Info, new[] { "-S" }, DisplayName = "-S alone changes nothing")]
    [DataRow(LogLevel.Verbose, new[] { "-v", "-S" }, DisplayName = "-v -S")]
    [DataRow(LogLevel.Verbose, new[] { "-v" }, DisplayName = "-v")]
    [DataRow(LogLevel.Verbose, new[] { "-s", "-v" }, DisplayName = "-s -v")]
    [DataRow(LogLevel.None, new[] { "-v", "-s" }, DisplayName = "-v -s")]
    [DataRow(LogLevel.Info, new[] { "-s", "--no-silent" }, DisplayName = "--no-silent")]
    [DataRow(LogLevel.Info, new[] { "-v", "--no-verbose" }, DisplayName = "--no-verbose")]
    [DataRow(LogLevel.Info, new[] { "-s", "--no-verbose" }, DisplayName = "--no-verbose after -s")]
    [DataRow(LogLevel.Error, new[] { "-v", "--log-level", "error" }, DisplayName = "-v --log-level error")]
    [DataRow(LogLevel.Verbose, new[] { "--log-level", "error", "-v" }, DisplayName = "--log-level error -v")]
    [DataRow(LogLevel.Error, new[] { "--log-level", "none", "-S" }, DisplayName = "--log-level none -S")]
    [DataRow(LogLevel.Verbose, new[] { "--trace", "f", "-v" }, DisplayName = "--trace f -v")]
    [DataRow(LogLevel.Trace, new[] { "-v", "--trace", "f" }, DisplayName = "-v --trace f")]
    [DataRow(LogLevel.Trace, new[] { "-s", "--trace-ascii", "f" }, DisplayName = "-s --trace-ascii f")]
    public void Parse_LevelOptions_LastWinsThenShowErrorApplies(LogLevel expected, string[] options)
    {
        var commandLine = Served([.. options, Url]);

        Assert.AreEqual(expected, commandLine.LogLevel);
    }

    [TestMethod]
    [DataRow("none", LogLevel.None)]
    [DataRow("error", LogLevel.Error)]
    [DataRow("info", LogLevel.Info)]
    [DataRow("verbose", LogLevel.Verbose)]
    [DataRow("trace", LogLevel.Trace)]
    [DataRow("TRACE", LogLevel.Trace)]
    [DataRow("Verbose", LogLevel.Verbose)]
    public void Parse_LogLevelWord_InAnyCase_SetsThatLevel(string word, LogLevel expected)
    {
        Assert.AreEqual(expected, Served("--log-level", word, Url).LogLevel);
        Assert.AreEqual(expected, Served($"--log-level={word}", Url).LogLevel);
    }

    [TestMethod]
    [DataRow("debug")]
    [DataRow("warning")]
    [DataRow(" info")]
    [DataRow("2")]
    [DataRow("-v")]
    public void Parse_LogLevelWordNotALevel_IsBadlyUsed(string word) =>
        AssertOptionRefused(["--log-level", word, Url], "option --log-level: is badly used here");

    [TestMethod]
    public void Parse_LogLevelTraceWithoutATraceOption_DumpsHexToTheLogStream()
    {
        var commandLine = Served("--log-level", "trace", Url);

        Assert.AreEqual(LogLevel.Trace, commandLine.LogLevel);
        Assert.IsNull(commandLine.TraceFile);
        Assert.AreEqual(TraceDumpLayout.HexAndAscii, commandLine.TraceLayout);
    }

    [TestMethod]
    public void Parse_Trace_SetsTheTraceLevelAndTheHexDumpFile()
    {
        var commandLine = Served("--trace", "t.txt", Url);

        Assert.AreEqual(LogLevel.Trace, commandLine.LogLevel);
        Assert.AreEqual("t.txt", commandLine.TraceFile);
        Assert.AreEqual(TraceDumpLayout.HexAndAscii, commandLine.TraceLayout);
    }

    [TestMethod]
    public void Parse_TraceAscii_SetsTheTraceLevelAndTheAsciiDumpFile()
    {
        var commandLine = Served("--trace-ascii=-", Url);

        Assert.AreEqual(LogLevel.Trace, commandLine.LogLevel);
        Assert.AreEqual("-", commandLine.TraceFile);
        Assert.AreEqual(TraceDumpLayout.Ascii, commandLine.TraceLayout);
    }

    [TestMethod]
    public void Parse_TraceThenTraceAscii_DumpsAsciiToTheLastFileOnly()
    {
        var commandLine = Served("--trace", "f", "--trace-ascii", "g", Url);

        Assert.AreEqual(LogLevel.Trace, commandLine.LogLevel);
        Assert.AreEqual("g", commandLine.TraceFile);
        Assert.AreEqual(TraceDumpLayout.Ascii, commandLine.TraceLayout);
    }

    [TestMethod]
    public void Parse_TraceAsciiThenLaterLogLevelTrace_KeepsTheTraceFile()
    {
        var commandLine = Served("--trace-ascii", "g", "-v", "--log-level", "trace", Url);

        Assert.AreEqual(LogLevel.Trace, commandLine.LogLevel);
        Assert.AreEqual("g", commandLine.TraceFile);
        Assert.AreEqual(TraceDumpLayout.Ascii, commandLine.TraceLayout);
    }

    [TestMethod]
    [DataRow("-v", DisplayName = "Overridden by -v")]
    [DataRow("-s", DisplayName = "Overridden by -s")]
    [DataRow("-sS", DisplayName = "Overridden by -s -S")]
    public void Parse_TraceOverriddenByALaterLevel_KeepsNoTraceFile(string later) =>
        Assert.IsNull(Served("--trace", "f", later, Url).TraceFile);

    [TestMethod]
    public void Parse_TraceTime_TurnsOnAndNegatedLaterTurnsOff()
    {
        Assert.IsTrue(Served("--trace-time", Url).TraceTime);
        Assert.IsFalse(Served("--trace-time", "--no-trace-time", Url).TraceTime);
    }

    [TestMethod]
    public void Parse_LogFile_KeptAsGiven()
    {
        Assert.AreEqual("surl.log", Served("--log-file", "surl.log", Url).LogFile);
        Assert.AreEqual("-", Served("--log-file=-", Url).LogFile);
        Assert.AreEqual("b.log", Served("--log-file", "a.log", "--log-file", "b.log", Url).LogFile);
    }

    [TestMethod]
    [DataRow("--log-level")]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--log-file")]
    public void Parse_LoggingOptionWithEmptyArgument_IsBlank(string name)
    {
        AssertOptionRefused([name, "", Url], $"option {name}: blank argument where content is expected");
        AssertOptionRefused([$"{name}=", Url], $"option {name}=: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--log-level")]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--log-file")]
    public void Parse_LoggingOptionMissingItsArgument_RequiresParameter(string name) =>
        AssertOptionRefused([Url, name], $"option {name}: requires parameter");

    [TestMethod]
    [DataRow("--silent=yes")]
    [DataRow("--show-error=1")]
    [DataRow("--trace-time=on")]
    [DataRow("--no-silent=x")]
    [DataRow("--no-show-error=")]
    [DataRow("--no-trace-time=no")]
    public void Parse_ValueOnALoggingFlag_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: does not take a parameter");

    [TestMethod]
    [DataRow("--no-log-level")]
    [DataRow("--no-trace")]
    [DataRow("--no-trace-ascii")]
    [DataRow("--no-log-file")]
    public void Parse_NoPrefixOnANonNegatableLoggingOption_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: the given option cannot be reversed with a --no- prefix");

    [TestMethod]
    [DataRow("--trace-ids", DisplayName = "--trace-ids: never (ADR-0033 section 8)")]
    [DataRow("--trace-config", DisplayName = "--trace-config: later")]
    [DataRow("--stderr", DisplayName = "--stderr: never")]
    public void Parse_CurlLoggingOptionSurlLeavesOut_IsUnknown(string written) =>
        AssertOptionRefused([written, "x", Url], $"option {written}: is unknown");

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
    [DataRow("allow-anonymous", "--allow-anonymous")]
    [DataRow("allow-plaintext-auth", "--allow-plaintext-auth")]
    [DataRow("self-signed", "--self-signed")]
    public void Parse_Flag_TurnsItOn(string longName, string written) =>
        Assert.IsTrue(FlagValues[longName](Served(written, Url)));

    [TestMethod]
    [DataRow("verbose")]
    [DataRow("allow-uploads")]
    [DataRow("list-directories")]
    [DataRow("follow-symlinks")]
    [DataRow("serve-dot-files")]
    [DataRow("allow-anonymous")]
    [DataRow("allow-plaintext-auth")]
    [DataRow("self-signed")]
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
    [DataRow("--version=1")]
    [DataRow("--allow-anonymous=yes")]
    [DataRow("--allow-plaintext-auth=")]
    [DataRow("--self-signed=1")]
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
    [DataRow("--no-user")]
    [DataRow("--no-user-file")]
    [DataRow("--no-auth")]
    public void Parse_NoPrefixOnANonNegatableOption_IsRefused(string written) =>
        AssertOptionRefused([written, Url], $"option {written}: the given option cannot be reversed with a --no- prefix");

    // -u/--user, --user-file, --auth and --self-signed (ADR-0032 section 1).

    [TestMethod]
    public void NewSurlCommandLine_HoldsTheAuthenticationDefaults()
    {
        var defaults = new SurlCommandLine();

        Assert.IsEmpty(defaults.Accounts);
        Assert.IsNull(defaults.UserFile);
        Assert.IsFalse(defaults.AllowAnonymous);
        Assert.IsFalse(defaults.AllowPlaintextAuthentication);
        Assert.IsFalse(defaults.SelfSigned);
        Assert.IsNull(defaults.GivenAuthenticationMethods);
        CollectionAssert.AreEqual(new[] { "digest", "basic", "bearer", "aws-sigv4" }, defaults.AcceptedAuthenticationMethods.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--user", "alice:secret" }, DisplayName = "--user, separate")]
    [DataRow(new[] { "--user=alice:secret" }, DisplayName = "--user=")]
    [DataRow(new[] { "-u", "alice:secret" }, DisplayName = "-u, separate")]
    [DataRow(new[] { "-ualice:secret" }, DisplayName = "-u, attached")]
    [DataRow(new[] { "-vu", "alice:secret" }, DisplayName = "-u ending a bundle")]
    public void Parse_User_AddsTheAccount(string[] arguments) =>
        CollectionAssert.AreEqual(
            new[] { new CommandLineAccount("alice", "secret") },
            Served([.. arguments, Url]).Accounts.ToArray());

    [TestMethod]
    public void Parse_UserRepeated_AddsAnAccountEachTimeInOrder() =>
        CollectionAssert.AreEqual(
            new[] { new CommandLineAccount("alice", "a"), new CommandLineAccount("bob", "b"), new CommandLineAccount(string.Empty, "tok") },
            Served("-u", "alice:a", Url, "--user=bob:b", "--user", ":tok").Accounts.ToArray());

    [TestMethod]
    [DataRow("a:b:c", "a", "b:c", DisplayName = "Split at the first colon")]
    [DataRow(":tok", "", "tok", DisplayName = "An empty name is a Bearer token")]
    [DataRow(" a :: b ", " a ", ": b ", DisplayName = "Nothing trimmed")]
    [DataRow("hé:päss", "hé", "päss", DisplayName = "Non-ASCII")]
    public void Parse_UserValue_IsSplitAtItsFirstColon(string value, string userName, string password) =>
        Assert.AreEqual(new CommandLineAccount(userName, password), Served("--user", value, Url).Accounts.Single());

    [TestMethod]
    [DataRow(new[] { "--user", "alice" }, "option --user: expected <user:password>", DisplayName = "No colon")]
    [DataRow(new[] { "--user=alice" }, "option --user: expected <user:password>", DisplayName = "No colon, after =")]
    [DataRow(new[] { "-ualice" }, "option -u: expected <user:password>", DisplayName = "No colon, attached")]
    [DataRow(new[] { "-vualice" }, "option -u: expected <user:password>", DisplayName = "No colon, in a bundle")]
    [DataRow(new[] { "--user", "alice:" }, "option --user: the password is empty", DisplayName = "Empty password")]
    [DataRow(new[] { "-u", ":" }, "option -u: the password is empty", DisplayName = "Empty name and password")]
    [DataRow(new[] { "--user", "al\tice:secret" }, "option --user: the user name holds a control character", DisplayName = "TAB in the name")]
    [DataRow(new[] { "--user", "al\u0000ice:secret" }, "option --user: the user name holds a control character", DisplayName = "NUL in the name")]
    [DataRow(new[] { "--user=al\u007Fice:secret" }, "option --user: the user name holds a control character", DisplayName = "DEL in the name")]
    [DataRow(new[] { "--user", "" }, "option --user: blank argument where content is expected", DisplayName = "Empty argument")]
    [DataRow(new[] { "--user=" }, "option --user: blank argument where content is expected", DisplayName = "Empty argument after =")]
    [DataRow(new[] { "--no-user=a:secret" }, "option --no-user: the given option cannot be reversed with a --no- prefix", DisplayName = "--no-user with a value")]
    public void Parse_UserRefused_NamesTheOptionWithoutItsValue(string[] arguments, string message)
    {
        var failure = Refused([.. arguments, Url]);

        Assert.AreEqual(message, failure.Message);
        Assert.DoesNotContain("secret", failure.Message);
        Assert.AreEqual(SurlExitCode.FailedInit, failure.ExitCode);
        Assert.IsTrue(failure.FollowedByTryHelpLine);
    }

    [TestMethod]
    [DataRow("--user")]
    [DataRow("-u")]
    [DataRow("--user-file")]
    [DataRow("--auth")]
    public void Parse_AuthenticationOptionMissingItsArgument_RequiresParameter(string name) =>
        AssertOptionRefused([Url, name], $"option {name}: requires parameter");

    [TestMethod]
    public void Parse_UserInABundleMissingItsArgument_NamesTheShortOption() =>
        AssertOptionRefused([Url, "-vu"], "option -u: requires parameter");

    [TestMethod]
    [DataRow(new[] { "--user", "alice:a", "--user", "alice:b" }, "option --user: user alice is given twice", DisplayName = "--user twice")]
    [DataRow(new[] { "--user", "alice:a", "-u", "alice:b" }, "option -u: user alice is given twice", DisplayName = "Named by the repeat's own form")]
    [DataRow(new[] { "-ubob:b", "-ualice:a", "--user=alice:a" }, "option --user: user alice is given twice", DisplayName = "The same account twice")]
    [DataRow(new[] { "-u", ":t1", "-u", ":t2" }, "option -u: user  is given twice", DisplayName = "The empty name twice")]
    public void Parse_UserNameGivenTwice_IsRefused(string[] arguments, string message) =>
        AssertOptionRefused([.. arguments, Url], message);

    [TestMethod]
    public void Parse_UserNamesDifferingOnlyInCase_AreTwoAccounts() =>
        Assert.HasCount(2, Served("-u", "alice:a", "-u", "Alice:a", Url).Accounts);

    [TestMethod]
    public void Parse_UserNameGivenTwice_IsCheckedAfterTheWholeLine() =>
        AssertOptionRefused(["-u", "a:a", "-u", "a:b", "--nosuch", Url], "option --nosuch: is unknown");

    [TestMethod]
    public void Parse_UserFile_KeepsThePathAsGivenAndLastWins()
    {
        Assert.AreEqual("users.txt", Served("--user-file", "users.txt", Url).UserFile);
        Assert.AreEqual("b", Served("--user-file=a", "--user-file", "b", Url).UserFile);
    }

    [TestMethod]
    [DataRow("--user-file", "")]
    [DataRow("--user-file=", null)]
    public void Parse_UserFileEmpty_IsBlank(string first, string? second)
    {
        string[] arguments = second is null ? [first, Url] : [first, second, Url];

        AssertOptionRefused(arguments, $"option {first}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("basic", new[] { "basic" })]
    [DataRow("NTLM", new[] { "ntlm" }, DisplayName = "Any case, stored lower-case")]
    [DataRow("aws-sigv4,Basic,negotiate", new[] { "negotiate", "basic", "aws-sigv4" }, DisplayName = "Section 3's order")]
    [DataRow("basic,bearer,digest,ntlm,negotiate,aws-sigv4", new[] { "negotiate", "ntlm", "digest", "basic", "bearer", "aws-sigv4" }, DisplayName = "Every word")]
    [DataRow("digest,DIGEST,digest", new[] { "digest" }, DisplayName = "A word given twice counts once")]
    public void Parse_Auth_IsTheAcceptedMethodSet(string argument, string[] methods)
    {
        var commandLine = Served("--auth", argument, Url);

        CollectionAssert.AreEqual(methods, commandLine.GivenAuthenticationMethods!.ToArray());
        CollectionAssert.AreEqual(methods, commandLine.AcceptedAuthenticationMethods.ToArray());
    }

    [TestMethod]
    public void Parse_AuthRepeated_LastValueWins() =>
        CollectionAssert.AreEqual(new[] { "ntlm" }, Served("--auth", "basic", "--auth=ntlm", Url).AcceptedAuthenticationMethods.ToArray());

    [TestMethod]
    [DataRow("--auth", "kerberos", DisplayName = "An unknown word")]
    [DataRow("--auth", "basic,,digest", DisplayName = "An empty item")]
    [DataRow("--auth", "basic,", DisplayName = "A trailing comma")]
    [DataRow("--auth", "basic, digest", DisplayName = "A space")]
    [DataRow("--auth", "", DisplayName = "Empty argument")]
    public void Parse_AuthBadWord_IsBadlyUsed(string name, string argument) =>
        AssertOptionRefused([name, argument, Url], "option --auth: is badly used here");

    [TestMethod]
    public void Parse_AuthBadWordAfterEquals_NamesTheOptionAsWritten() =>
        AssertOptionRefused(["--auth=nosuch", Url], "option --auth=nosuch: is badly used here");

    [TestMethod]
    [DataRow(new[] { "--self-signed", "--cert", "c.pem" }, DisplayName = "Before --cert")]
    [DataRow(new[] { "--cert", "c.pem", "--self-signed" }, DisplayName = "After --cert")]
    public void Parse_SelfSignedWithCert_IsRefused(string[] arguments) =>
        AssertOptionRefused([.. arguments, Url], "option --self-signed: cannot be used with --cert");

    [TestMethod]
    public void Parse_SelfSignedReversedWithCert_IsServed()
    {
        var commandLine = Served("--self-signed", "--no-self-signed", "--cert", "c.pem", Url);

        Assert.IsFalse(commandLine.SelfSigned);
        Assert.AreEqual("c.pem", commandLine.CertificateFile);
    }

    [TestMethod]
    public void SurlCommandLineToString_WithAccounts_HoldsNoPassword()
    {
        var commandLine = new SurlCommandLine { Accounts = [new("alice", "s3cr3t-pw"), new(string.Empty, "t0ken-value")] };

        var text = commandLine.ToString();

        Assert.DoesNotContain("s3cr3t-pw", text);
        Assert.DoesNotContain("t0ken-value", text);
    }

    [TestMethod]
    public void CommandLineAccountToString_NamesTheUserWithoutThePassword()
    {
        var text = new CommandLineAccount("alice", "s3cr3t-pw").ToString();

        Assert.AreEqual("CommandLineAccount { UserName = alice }", text);
    }

    [TestMethod]
    public void Parse_ShortFlagsBundled_SetsEach()
    {
        var commandLine = Served("-vm30", Url);

        Assert.AreEqual(LogLevel.Verbose, commandLine.LogLevel);
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
        Assert.AreEqual(expected, Served("--max-filesize", argument, Url).Limits.MaxUploadBytes);

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

    [TestMethod]
    public void Parse_NoDirectory_LeavesTheDataDirectoryNullForInMemoryServing()
    {
        Assert.IsNull(Served(Url).DataDirectory);
    }

    [TestMethod]
    public void Parse_Directory_KeepsTheDataDirectoryAsGiven()
    {
        Assert.AreEqual("srv/data", Served("--directory", "srv/data", Url).DataDirectory);
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
    [DataRow("--insecure", DisplayName = "curl option surl leaves out")]
    [DataRow("--no-no-verbose", DisplayName = "--no-no- (row 18)")]
    [DataRow("--no-", DisplayName = "--no- alone")]
    [DataRow("--=x", DisplayName = "No name before =")]
    [DataRow("--VERBOSE", DisplayName = "Names are case-sensitive")]
    [DataRow("-~", DisplayName = "Unknown short option (row 22)")]
    [DataRow("-v~", DisplayName = "Unknown short option in a bundle")]
    [DataRow("-k", DisplayName = "curl short option surl leaves out")]
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
        Assert.AreEqual(LogLevel.Verbose, Served(Url, "-v").LogLevel);

    [TestMethod]
    public void Parse_RepeatedOption_LastWins() =>
        Assert.AreEqual(TimeSpan.FromSeconds(6), Served("-m", "5", Url, "--max-time", "6").MaxTime);

    [TestMethod]
    public void Parse_DoubleDashThenAListenUrl_ServesIt()
    {
        var commandLine = Served("-v", "--", Url);

        Assert.AreEqual(LogLevel.Verbose, commandLine.LogLevel);
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
    [DataRow("--help=", DisplayName = "An empty attached subject")]
    public void Parse_HelpWithNothingAfterIt_ShowsHelpWithNoSubject(string written) =>
        AssertHelp(null, written);

    [TestMethod]
    [DataRow("all", new[] { "--help", "all" })]
    [DataRow("auth", new[] { "-h", "auth" })]
    [DataRow("auth", new[] { "-vh", "auth" }, DisplayName = "-h ending a bundle takes the next argument")]
    [DataRow("auth", new[] { "--help=auth" }, DisplayName = "Attached with =")]
    [DataRow("auth", new[] { "-hauth" }, DisplayName = "The rest of the argument after -h")]
    [DataRow("V", new[] { "-hV" }, DisplayName = "-hV asks for help on V")]
    [DataRow("-v", new[] { "-h", "-v" }, DisplayName = "The next argument whatever it looks like")]
    [DataRow("--", new[] { "--help", "--" }, DisplayName = "Even the end of options")]
    [DataRow("http://127.0.0.1:1/", new[] { "--help", "http://127.0.0.1:1/" }, DisplayName = "A listen URL")]
    [DataRow(null, new[] { "--help", "" }, DisplayName = "An empty next argument")]
    [DataRow("auth", new[] { "--help", "auth", "--nosuch" }, DisplayName = "Nothing after the subject is read")]
    [DataRow("--max-time", new[] { "http://127.0.0.1:1/", "--help", "--max-time", "x" }, DisplayName = "After a listen URL")]
    public void Parse_HelpAndASubject_ShowsHelpForTheSubject(string? subject, string[] arguments) =>
        AssertHelp(subject, arguments);

    [TestMethod]
    public void Parse_NoHelp_IsRefusedAsNotReversible() =>
        AssertOptionRefused(["--no-help"], "option --no-help: the given option cannot be reversed with a --no- prefix");

    [TestMethod]
    [DataRow("--version")]
    [DataRow("-V")]
    [DataRow("-vV", DisplayName = "Ending a bundle")]
    [DataRow("-Vh", DisplayName = "First of help and version wins")]
    public void Parse_Version_ShowsVersion(string written) =>
        Assert.AreSame(CommandLineParseResult.ShowVersion, CommandLineParser.Parse([written]));

    [TestMethod]
    [DataRow("--manual")]
    [DataRow("-M")]
    [DataRow("-vM", DisplayName = "Ending a bundle")]
    [DataRow("-Mh", DisplayName = "First of manual and help wins")]
    public void Parse_Manual_ShowsManual(string written) =>
        Assert.AreSame(CommandLineParseResult.ShowManual, CommandLineParser.Parse([written]));

    [TestMethod]
    public void Parse_ManualBeforeAnythingElse_EndsReadingWhereItStands()
    {
        Assert.AreSame(CommandLineParseResult.ShowManual, CommandLineParser.Parse(["-M", "--no-such"]));
        Assert.AreSame(CommandLineParseResult.ShowManual, CommandLineParser.Parse(["--manual", "--help"]));
        Assert.AreSame(CommandLineParseResult.ShowManual, CommandLineParser.Parse(["-s", "--manual", Url]));
        Assert.AreSame(CommandLineParseResult.ShowVersion, CommandLineParser.Parse(["-V", "--manual"]));
        Assert.AreEqual(CommandLineOutcome.ShowHelp, CommandLineParser.Parse(["--help", "--manual"]).Outcome);
        AssertOptionRefused(["--no-such", "--manual"], "option --no-such: is unknown");
    }

    [TestMethod]
    public void Parse_ManualWithAValueOrNegated_IsRefused()
    {
        AssertOptionRefused(["--manual=1"], "option --manual=1: does not take a parameter");
        AssertOptionRefused(["--no-manual"], "option --no-manual: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_HelpOrVersionBeforeAnError_EndsReadingWhereItStands()
    {
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["-V", "--no-such"]).Outcome);
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["--version", "--help"]).Outcome);
        Assert.AreEqual(CommandLineOutcome.ShowVersion, CommandLineParser.Parse(["-Vh"]).Outcome);
    }

    [TestMethod]
    public void Parse_ErrorBeforeHelpOrVersion_IsReported()
    {
        AssertOptionRefused(["--no-such", "-V"], "option --no-such: is unknown");
        AssertOptionRefused(["--no-such", "--help"], "option --no-such: is unknown");
        AssertOptionRefused(["-m", "x", "--help"], "option -m: expected a proper numerical parameter");
    }

    [TestMethod]
    public void ShowHelpAndShowVersion_CarryNeitherCommandLineNorFailure()
    {
        foreach (var result in new[] { CommandLineParseResult.ShowHelp("auth"), CommandLineParseResult.ShowVersion, CommandLineParseResult.ShowManual })
        {
            Assert.IsNull(result.CommandLine);
            Assert.IsNull(result.Failure);
        }
    }

    [TestMethod]
    public void HelpSubject_IsNullForEveryOutcomeButShowHelp()
    {
        Assert.IsNull(CommandLineParseResult.ShowVersion.HelpSubject);
        Assert.IsNull(CommandLineParseResult.ShowManual.HelpSubject);
        Assert.IsNull(CommandLineParser.Parse(["--no-such"]).HelpSubject);
        Assert.IsNull(CommandLineParser.Parse([Url]).HelpSubject);
    }

    private static void AssertHelp(string? subject, params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.ShowHelp, result.Outcome, result.Failure?.Message);
        Assert.AreEqual(subject, result.HelpSubject);
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
        _ => commandLine.Limits.HeadTimeout,
    };

    private static int NumberOf(string name, SurlCommandLine commandLine) =>
        name == "--max-connections" ? commandLine.MaxConnections : commandLine.MaxConnectionsPerAddress;

    private static long BytesOf(string name, SurlCommandLine commandLine) => name switch
    {
        "--max-request-head" => commandLine.Limits.MaxRequestHeadBytes,
        "--max-line" => commandLine.Limits.MaxLineBytes,
        "--max-message" => commandLine.Limits.MaxMessageBytes,
        _ => commandLine.Limits.MaxUploadBytes,
    };

    private static string? PathOf(string name, SurlCommandLine commandLine) => name switch
    {
        "--directory" => commandLine.DataDirectory,
        "--cert" => commandLine.CertificateFile,
        "--key" => commandLine.KeyFile,
        _ => commandLine.CaCertificateFile,
    };
}
