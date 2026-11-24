using Surl.Cookies;

namespace Surl.Cli;

/// <summary>
/// Reads one <c>--set-cookie &lt;spec&gt;</c> argument (ADR-0189 decision 1) into a <see cref="CookieDefinition"/>
/// with <see cref="CookieDefinitionReader"/>, so the command line, a <c>-K</c> file and every other host refuse a spec in
/// the same words. Every refusal follows <c>option &lt;name&gt;: </c>.
/// </summary>
internal static class ConfiguredCookieReader
{
    /// <summary>The most <c>--set-cookie</c> options one run holds (ADR-0189 decision 1).</summary>
    public const int MaxCookies = 64;

    /// <summary>A <c>--set-cookie</c> past <see cref="MaxCookies"/>.</summary>
    public const string TooManyCookies = "more than 64 cookies";

    /// <summary><see cref="Read"/> with its argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<CookieDefinition> Reading = new(
        Read,
        new("cookie", "name=value, then any of ;Domain=, ;Path=, ;Expires=<IMF-fixdate>, ;Max-Age=<seconds>, ;Secure, ;HttpOnly, ;SameSite=Strict, Lax or None, ;on-path=</prefix> and ;on-listener=<listen URL>"));

    /// <summary>Reads one <c>--set-cookie</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The cookie, when accepted; a nameless placeholder otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out CookieDefinition value)
    {
        var accepted = CookieDefinitionReader.TryRead(argument, out var cookie, out var problem);
        value = cookie ?? new CookieDefinition("none", string.Empty);

        return accepted ? null : problem;
    }
}
