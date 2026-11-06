namespace Surl.Conformance;

/// <summary>
/// ADR-0080 decision 3's skip reasons, each the fixed text the result lines and the pass-rate
/// file carry. The reasons with a name after the colon are built by the methods.
/// </summary>
public static class UpstreamTestCaseSkipReasons
{
    /// <summary>The case is listed in <c>tests/data/DISABLED</c>.</summary>
    public const string DisabledUpstream = "disabled-upstream";

    /// <summary>The case runs a <c>unit</c> program, or needs the <c>unittest</c> feature.</summary>
    public const string UnitTest = "unittest";

    /// <summary>The case runs any other program built against libcurl's test hooks.</summary>
    public const string LibTest = "libtest";

    /// <summary>The case needs <c>Debug</c> or <c>TrackMemory</c>, which no release build has.</summary>
    public const string DebugBuild = "debug-build";

    /// <summary>The case's command is a Perl or shell script.</summary>
    public const string PerlCommand = "perl-command";

    /// <summary>The case's command or a file names Perl, a <c>.pl</c> script or a file outside the imported data.</summary>
    public const string NeedsPerl = "needs-perl";

    /// <summary>The case has a <c>&lt;precheck&gt;</c>.</summary>
    public const string Precheck = "precheck";

    /// <summary>The case has a <c>&lt;postcheck&gt;</c>.</summary>
    public const string Postcheck = "postcheck";

    /// <summary>The case names no server, or only <c>file</c> or <c>none</c>.</summary>
    public const string NoServerHalf = "no-server-half";

    /// <summary>A strip line falls outside ADR-0080 decision 4's Perl subset.</summary>
    public const string StripPartUnsupported = "strippart-unsupported";

    /// <summary>Decision 5's exclusion list names the case.</summary>
    public const string TestServerChoice = "test-server-choice";

    /// <summary>A server the case names has no server half yet.</summary>
    /// <param name="server">The server name.</param>
    /// <returns><c>server-not-built:</c> and the name.</returns>
    public static string ServerNotBuilt(string server) => "server-not-built:" + server;

    /// <summary>The pinned build's <c>protocols</c> line lacks a scheme the case needs.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns><c>protocol-missing:</c> and the scheme.</returns>
    public static string ProtocolMissing(string scheme) => "protocol-missing:" + scheme;

    /// <summary>
    /// A feature the case requires is absent, or one it requires absent (written <c>!name</c>)
    /// is present.
    /// </summary>
    /// <param name="feature">The feature as the case writes it, <c>!</c> included.</param>
    /// <returns><c>feature-missing:</c> and the feature.</returns>
    public static string FeatureMissing(string feature) => "feature-missing:" + feature;

    /// <summary>The server half does not implement a server command the case uses.</summary>
    /// <param name="command">The command, such as <c>REPLY</c>.</param>
    /// <returns><c>servercmd-unsupported:</c> and the command.</returns>
    public static string ServerCommandUnsupported(string command) => "servercmd-unsupported:" + command;
}
