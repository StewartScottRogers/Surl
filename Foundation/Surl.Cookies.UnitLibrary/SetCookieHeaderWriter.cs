using System.Globalization;
using System.Text;

namespace Surl.Cookies;

/// <summary>
/// Writes a <see cref="CookieDefinition"/> as a <c>Set-Cookie</c> field: <c>name=value</c>, then each attribute given in the
/// order <c>Domain</c>, <c>Path</c>, <c>Expires</c>, <c>Max-Age</c>, <c>Secure</c>, <c>HttpOnly</c>, <c>SameSite</c>, with
/// <c>; </c> between each and no default added (ADR-0189 decision 3; https://curl.se/docs/http-cookies.html, curl 8.21.0).
/// The scoping keys <c>OnPath</c> and <c>OnListener</c> are never written.
/// </summary>
public static class SetCookieHeaderWriter
{
    /// <summary>The header name, without the colon.</summary>
    public const string HeaderName = "Set-Cookie";

    /// <summary>Writes the field value, for example <c>sess=xyz; Path=/</c>.</summary>
    public static string WriteFieldValue(CookieDefinition cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        StringBuilder text = new StringBuilder(cookie.Name).Append('=').Append(cookie.Value);
        AppendValued(text, "Domain", cookie.Domain);
        AppendValued(text, "Path", cookie.Path);
        AppendValued(text, "Expires", cookie.Expires);
        AppendValued(text, "Max-Age", cookie.MaxAge?.ToString(CultureInfo.InvariantCulture));
        AppendFlag(text, "Secure", cookie.Secure);
        AppendFlag(text, "HttpOnly", cookie.HttpOnly);
        AppendValued(text, "SameSite", cookie.SameSite);
        return text.ToString();
    }

    /// <summary>Writes the whole header line without its line ending, for example <c>Set-Cookie: sess=xyz; Path=/</c>.</summary>
    public static string WriteHeaderLine(CookieDefinition cookie) => HeaderName + ": " + WriteFieldValue(cookie);

    private static void AppendValued(StringBuilder text, string attribute, string? value)
    {
        if (value is not null)
        {
            text.Append("; ").Append(attribute).Append('=').Append(value);
        }
    }

    private static void AppendFlag(StringBuilder text, string attribute, bool given)
    {
        if (given)
        {
            text.Append("; ").Append(attribute);
        }
    }
}
