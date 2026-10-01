using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>ADR-0046 decision 9's completeness tests for the facts <c>--aihelp</c> reads.</summary>
[TestClass]
public sealed class AiHelpFactsTests
{
    // The topics ADR-0046 decision 3 adds beside the help categories.
    private static readonly string[] TopicsAddedBesideTheCategories = ["exit-codes", "listen-urls"];

    [TestMethod]
    public void EveryOption_HasAnArgumentTypeAndAllowedValues()
    {
        foreach (var option in CommandLineOptions.All)
        {
            Assert.IsNotNull(option.ArgumentType, option.LongName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(option.ArgumentType.Name), option.LongName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(option.ArgumentType.AllowedValues), option.LongName);
        }
    }

    [TestMethod]
    public void EveryOption_HasTheArgumentTypeOfItsKind()
    {
        foreach (var option in CommandLineOptions.All)
        {
            var expected = option.Kind switch
            {
                CommandLineOptionKind.Help => OptionArgumentType.OptionalSubject,
                CommandLineOptionKind.Version or CommandLineOptionKind.Manual => OptionArgumentType.None,
                CommandLineOptionKind.Flag when option.Negatable => new("flag", $"--no-{option.LongName} turns it off"),
                CommandLineOptionKind.Flag => new("flag", "no --no- form"),
                _ => null,
            };

            if (expected is null)
            {
                Assert.IsNotNull(option.Help.ArgumentName, option.LongName);
                Assert.AreNotEqual("flag", option.ArgumentType.Name, option.LongName);
            }
            else
            {
                Assert.AreEqual(expected, option.ArgumentType, option.LongName);
            }
        }
    }

    [TestMethod]
    [DataRow("log-level", "word")]
    [DataRow("tls-max", "TLS version")]
    [DataRow("auth", "word list")]
    [DataRow("max-time", "seconds")]
    [DataRow("idle-timeout", "seconds")]
    [DataRow("head-timeout", "seconds")]
    [DataRow("max-connections", "number")]
    [DataRow("max-connections-per-address", "number")]
    [DataRow("max-request-head", "bytes")]
    [DataRow("max-line", "bytes")]
    [DataRow("max-message", "bytes")]
    [DataRow("max-filesize", "bytes")]
    [DataRow("trace", "path")]
    [DataRow("directory", "path")]
    [DataRow("pass", "text")]
    [DataRow("user", "user:password")]
    [DataRow("authorized-keys", "user:file")]
    [DataRow("hostkey", "path")]
    [DataRow("hostcert", "path")]
    [DataRow("cert-type", "word")]
    [DataRow("key-type", "word")]
    public void ArgumentOption_HasTheArgumentTypeOfItsReader(string longName, string argumentType)
    {
        Assert.IsTrue(CommandLineOptions.TryFindLong(longName, out var option));

        Assert.AreEqual(argumentType, option.ArgumentType.Name);
    }

    [TestMethod]
    public void LoosensSecurityForTestsOnly_IsExactlyTheFiveTestingOptions()
    {
        var testing = CommandLineOptions.All.Where(option => option.Help.Categories.Contains("testing")).ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "allow-anonymous", "allow-plaintext-auth", "auth", "self-signed", "throwaway-hostkey" },
            testing.Select(option => option.LongName).ToArray());
        foreach (var option in testing)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(option.Help.Explanation), option.LongName);
            CollectionAssert.Contains(option.Help.Categories.ToArray(), "security", option.LongName);
        }
    }

    [TestMethod]
    public void WidensWhatAPeerMayDo_IsExactlyTheSecurityOptionsOutsideTesting()
    {
        var widening = CommandLineOptions.All
            .Where(option => option.Help.Categories.Contains("security") && !option.Help.Categories.Contains("testing"))
            .Select(option => option.LongName)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "allow-uploads", "list-directories", "follow-symlinks", "serve-dot-files", "tlsv1.0", "tlsv1.1", "allow-weak-ssh-algorithms" },
            widening);
    }

    [TestMethod]
    public void ExitCodeGuidance_HasExactlyOneRowPerSurlExitCodeMember()
    {
        var codes = ExitCodeGuidanceTable.All.Select(row => row.Code).ToArray();

        CollectionAssert.AreEqual(Enum.GetValues<SurlExitCode>().OrderBy(code => (int)code).ToArray(), codes);
        foreach (var row in ExitCodeGuidanceTable.All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Meaning), row.Code.ToString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.NextStep), row.Code.ToString());
        }
    }

    [TestMethod]
    public void ExitCodeGuidance_EveryRowNamesOnlyTopicsThatExist()
    {
        var topics = HelpCategories.All.Select(category => category.Name).Concat(TopicsAddedBesideTheCategories).ToArray();

        foreach (var row in ExitCodeGuidanceTable.All)
        {
            Assert.IsNotEmpty(row.Topics, row.Code.ToString());
            foreach (var topic in row.Topics)
            {
                CollectionAssert.Contains(topics, topic, row.Code.ToString());
            }
        }
    }

    [TestMethod]
    public void EveryProtocolCategoryScheme_HasADefaultPort()
    {
        var schemes = HelpCategories.All.SelectMany(category => category.Schemes).ToArray();

        CollectionAssert.AllItemsAreUnique(schemes);
        foreach (var scheme in schemes)
        {
            Assert.IsTrue(SchemeDefaultPorts.TryGetDefaultPort(scheme, out _), scheme);
        }
    }

    [TestMethod]
    [DataRow("dict", new[] { "dict" })]
    [DataRow("gopher", new[] { "gopher", "gophers" })]
    [DataRow("http", new[] { "http", "https" })]
    [DataRow("imap", new[] { "imap", "imaps" })]
    [DataRow("mqtt", new[] { "mqtt", "mqtts" })]
    [DataRow("pop3", new[] { "pop3", "pop3s" })]
    [DataRow("smb", new[] { "smb", "smbs" })]
    [DataRow("smtp", new[] { "smtp", "smtps" })]
    [DataRow("telnet", new[] { "telnet" })]
    [DataRow("tftp", new[] { "tftp" })]
    public void ProtocolCategory_ClaimsItsSchemes(string name, string[] schemes)
    {
        Assert.IsTrue(HelpCategories.TryFind(name, out var category));

        CollectionAssert.AreEqual(schemes, category.Schemes.ToArray());
    }

    [TestMethod]
    public void OnlyTheProtocolCategories_HaveSchemes()
    {
        var withSchemes = HelpCategories.All.Where(category => category.Schemes.Count > 0).Select(category => category.Name);

        CollectionAssert.AreEqual(new[] { "dict", "ftp", "gopher", "http", "imap", "mqtt", "pop3", "rtsp", "smb", "smtp", "ssh", "telnet", "tftp", "websocket" }, withSchemes.ToArray());
    }

    [TestMethod]
    public void FactTexts_HoldNoBacktickPipeOrLineBreak()
    {
        var texts = CommandLineOptions.All
            .SelectMany(option => new[]
            {
                option.Help.ArgumentName,
                option.Help.Description,
                option.Help.Default,
                option.Help.Explanation,
                option.ArgumentType.Name,
                option.ArgumentType.AllowedValues,
            }.Concat(option.Help.Categories))
            .Concat(HelpCategories.All.SelectMany(category => new[] { category.Name, category.Description }.Concat(category.Schemes)))
            .Concat(ExitCodeGuidanceTable.All.SelectMany(row => new[] { row.Meaning, row.NextStep }.Concat(row.Topics)))
            .OfType<string>();

        foreach (var text in texts)
        {
            Assert.AreEqual(-1, text.AsSpan().IndexOfAny("`|\r\n"), text);
        }
    }

    // Allowed-values texts, pinned and checked against the reader at their edges.

    [TestMethod]
    public void LogLevel_AllowedValues_AreTheWordsTheReaderAcceptsInAnyCase()
    {
        Assert.AreEqual("none, error, info, verbose or trace, in any case", AllowedValuesOf("log-level"));

        foreach (var word in new[] { "none", "error", "info", "verbose", "trace" })
        {
            Assert.IsNull(OptionArgumentReader.ReadLogLevel(word, out _), word);
            Assert.IsNull(OptionArgumentReader.ReadLogLevel(word.ToUpperInvariant(), out _), word);
        }

        Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadLogLevel("debug", out _));
        Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadLogLevel("info ", out _));
    }

    [TestMethod]
    public void TlsMax_AllowedValues_AreTheVersionsTheReaderAcceptsExactly()
    {
        Assert.AreEqual("1.0, 1.1, 1.2 or 1.3", AllowedValuesOf("tls-max"));

        foreach (var version in new[] { "1.0", "1.1", "1.2", "1.3" })
        {
            Assert.IsNull(OptionArgumentReader.ReadTlsVersion(version, out _), version);
        }

        foreach (var refused in new[] { "1.4", "0.9", "1.30", "1", "v1.2" })
        {
            Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadTlsVersion(refused, out _), refused);
        }
    }

    [TestMethod]
    public void Auth_AllowedValues_AreTheMethodWordsTheReaderAccepts()
    {
        Assert.AreEqual(
            "comma-separated, in any case, no empty item: negotiate, gssapi, ntlm, ntlmv1, digest, digest-md5, cram-md5, apop, basic, plain, login, bearer, oauthbearer, xoauth2, external, aws-sigv4",
            AllowedValuesOf("auth"));

        foreach (var word in OptionArgumentReader.AuthenticationMethodWords)
        {
            Assert.IsNull(OptionArgumentReader.ReadAuthenticationMethods(word.ToUpperInvariant(), out _), word);
        }

        Assert.IsNull(OptionArgumentReader.ReadAuthenticationMethods(string.Join(",", OptionArgumentReader.AuthenticationMethodWords), out _));
        foreach (var refused in new[] { string.Empty, "basic,", ",basic", "basic,,digest", "kerberos", "basic digest" })
        {
            Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadAuthenticationMethods(refused, out _), refused);
        }
    }

    [TestMethod]
    public void MaxTime_AllowedValues_AreTheSecondsTheReaderAccepts()
    {
        Assert.AreEqual("0 to 2147483.647, digits with an optional decimal point and more digits", AllowedValuesOf("max-time"));

        foreach (var accepted in new[] { "0", "2147483.647", "2147483", "0.5", "007" })
        {
            Assert.IsNull(OptionArgumentReader.ReadSeconds(accepted, out _), accepted);
        }

        foreach (var refused in new[] { "2147483.648", "2147484", "-1", "1.", ".5", "1e3", "+1", "1,5", string.Empty })
        {
            Assert.AreEqual(OptionArgumentReader.NotANumber, OptionArgumentReader.ReadSeconds(refused, out _), refused);
        }
    }

    [TestMethod]
    public void MaxFilesize_AllowedValues_AreTheBytesTheReaderAccepts()
    {
        Assert.AreEqual(
            "digits with an optional decimal point and more digits, then at most one suffix k, m, g, t or p in either case, "
            + "each 1024 times the one before; at most 9223372036854775807 bytes",
            AllowedValuesOf("max-filesize"));

        Assert.IsNull(OptionArgumentReader.ReadBytes("9223372036854775807", out var largest));
        Assert.AreEqual(long.MaxValue, largest);
        Assert.IsNull(OptionArgumentReader.ReadBytes("1.5k", out var fraction));
        Assert.AreEqual(1536L, fraction);
        Assert.IsNull(OptionArgumentReader.ReadBytes("1P", out var peta));
        Assert.AreEqual(1L << 50, peta);
        Assert.IsNull(OptionArgumentReader.ReadBytes("8191.99p", out _));

        Assert.AreEqual(OptionArgumentReader.TooLarge, OptionArgumentReader.ReadBytes("9223372036854775808", out _));
        Assert.AreEqual(OptionArgumentReader.TooLarge, OptionArgumentReader.ReadBytes("8192p", out _));
        Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadBytes("1e", out _));
        Assert.AreEqual(OptionArgumentReader.NotANumber, OptionArgumentReader.ReadBytes("1kk", out _));
        Assert.AreEqual(OptionArgumentReader.NotANumber, OptionArgumentReader.ReadBytes("-1", out _));
    }

    [TestMethod]
    public void MaxConnections_AllowedValues_AreTheNumbersTheReaderAccepts()
    {
        Assert.AreEqual("0 to 2147483647, digits only", AllowedValuesOf("max-connections"));

        Assert.IsNull(OptionArgumentReader.ReadNumber("0", out _));
        Assert.IsNull(OptionArgumentReader.ReadNumber("2147483647", out var largest));
        Assert.AreEqual(int.MaxValue, largest);

        foreach (var refused in new[] { "2147483648", "-1", "+1", "1.5", " 1", string.Empty })
        {
            Assert.AreEqual(OptionArgumentReader.NotANumber, OptionArgumentReader.ReadNumber(refused, out _), refused);
        }
    }

    private static string AllowedValuesOf(string longName)
    {
        Assert.IsTrue(CommandLineOptions.TryFindLong(longName, out var option), longName);
        return option.ArgumentType.AllowedValues;
    }
}
