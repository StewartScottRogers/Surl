using Surl.Cookies;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary><c>--set-cookie</c> read into <see cref="SurlCommandLine.Cookies"/> (ADR-0189 decision 1, BL-1661).</summary>
[TestClass]
public sealed class CommandLineParserSetCookieTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoSetCookie_HasNoCookies() =>
        Assert.IsEmpty(Served(Url).Cookies);

    [TestMethod]
    public void Parse_TwoSetCookies_AreKeptInOrderWithEveryAttribute()
    {
        var cookies = Served(
            "--set-cookie", "sid=abc123;Domain=127.0.0.1;Path=/;Expires=Wed, 09 Jun 2027 10:18:14 GMT;Max-Age=3600;Secure;HttpOnly;SameSite=Lax;on-path=/app",
            "--set-cookie=sess=xyz",
            Url).Cookies;

        Assert.HasCount(2, cookies);
        Assert.AreEqual("sid", cookies[0].Name);
        Assert.AreEqual("abc123", cookies[0].Value);
        Assert.AreEqual("127.0.0.1", cookies[0].Domain);
        Assert.AreEqual(3600L, cookies[0].MaxAge);
        Assert.IsTrue(cookies[0].Secure);
        Assert.IsTrue(cookies[0].HttpOnly);
        Assert.AreEqual("Lax", cookies[0].SameSite);
        Assert.AreEqual("/app", cookies[0].OnPath);
        Assert.AreEqual("sess", cookies[1].Name);
    }

    [TestMethod]
    [DataRow("noequals", "option --set-cookie: the cookie needs a name=value pair")]
    [DataRow("=v", "option --set-cookie: the cookie name is empty or holds a control character, space or ','")]
    [DataRow("a=b;Max-Age=-1", null)]
    [DataRow("a=b;Bogus=1", null)]
    [DataRow("a=b;SameSite=Sometimes", null)]
    public void Parse_BadSetCookie_IsRefusedExitTwo(string spec, string? message)
    {
        var result = CommandLineParser.Parse(["--set-cookie", spec, Url]);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure.ExitCode);
        Assert.StartsWith("option --set-cookie: ", result.Failure.Message);
        if (message is not null)
        {
            Assert.AreEqual(message, result.Failure.Message);
        }
    }

    [TestMethod]
    public void Parse_SixtyFourSetCookies_AreServed() =>
        Assert.HasCount(64, Served([.. CookieArguments(64), Url]).Cookies);

    [TestMethod]
    public void Parse_SixtyFiveSetCookies_IsRefused()
    {
        var result = CommandLineParser.Parse([.. CookieArguments(65), Url]);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual("option --set-cookie: more than 64 cookies", result.Failure?.Message);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure?.ExitCode);
    }

    [TestMethod]
    public void Parse_SetCookieFromAConfigFile_EqualsTheSameSpecOnTheCommandLine()
    {
        var fromFile = CommandLineParser.Parse(["-K", "a.cfg", Url], [], new FakeConfigFileSource(("a.cfg", "set-cookie = \"session=t1;Path=/;HttpOnly\"\n")));
        var fromCommandLine = Served("--set-cookie", "session=t1;Path=/;HttpOnly", Url);

        Assert.AreEqual(CommandLineOutcome.Serve, fromFile.Outcome, fromFile.Failure?.Message);
        Assert.IsNotNull(fromFile.CommandLine);
        CollectionAssert.AreEqual(fromCommandLine.Cookies.ToArray(), fromFile.CommandLine.Cookies.ToArray());
    }

    [TestMethod]
    public void Parse_SetCookieWithoutArgument_IsRefused()
    {
        var result = CommandLineParser.Parse([Url, "--set-cookie"]);

        Assert.AreEqual("option --set-cookie: requires parameter", result.Failure?.Message);
    }

    [TestMethod]
    public void Read_RefusedSpec_GivesAPlaceholderCookie()
    {
        var problem = ConfiguredCookieReader.Read("nope", out CookieDefinition value);

        Assert.IsNotNull(problem);
        Assert.AreEqual("none", value.Name);
    }

    private static IEnumerable<string> CookieArguments(int count) =>
        Enumerable.Range(0, count).SelectMany(index => new[] { "--set-cookie", $"c{index}=v" });

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.IsNotNull(result.CommandLine);
        return result.CommandLine;
    }
}
