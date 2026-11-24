using System.Globalization;

namespace Surl.Cookies;

/// <summary>
/// Reads a <c>--set-cookie</c> spec, <c>name=value[;Attribute[=value]]...[;on-path=/prefix][;on-listener=URL]</c>, into a
/// <see cref="CookieDefinition"/> (ADR-0189 decisions 1 to 3; https://curl.se/docs/http-cookies.html, curl 8.21.0).
/// Attribute names are accepted in any case; an unknown or repeated attribute, an empty or malformed name, a control
/// character, a negative <c>Max-Age</c>, an <c>Expires</c> that is not an IMF-fixdate or a <c>SameSite</c> other than
/// <c>Strict</c>, <c>Lax</c> or <c>None</c> is refused with a text naming the problem.
/// </summary>
public static class CookieDefinitionReader
{
    private const string ImfFixdate = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";

    private static readonly string[] SameSiteValues = ["Strict", "Lax", "None"];

    private static readonly string[] ValuedAttributes = ["domain", "path", "expires", "max-age", "samesite", "on-path", "on-listener"];

    /// <summary>Reads one spec; on failure <paramref name="problem"/> says what is wrong and <paramref name="cookie"/> is <see langword="null"/>.</summary>
    public static bool TryRead(string? spec, out CookieDefinition? cookie, out string problem)
    {
        cookie = null;
        string[] segments = (spec ?? string.Empty).Split(';');
        problem = ReadNameAndValue(segments[0], out string name, out string value);
        if (problem.Length == 0)
        {
            problem = CollectAttributes(segments, out Dictionary<string, string?> attributes);
            if (problem.Length == 0)
            {
                problem = BuildDefinition(name, value, attributes, out cookie);
            }
        }

        return problem.Length == 0;
    }

    private static string ReadNameAndValue(string segment, out string name, out string value)
    {
        int equals = segment.IndexOf('=', StringComparison.Ordinal);
        name = equals < 0 ? segment : segment[..equals];
        value = equals < 0 ? string.Empty : segment[(equals + 1)..];
        if (equals < 0)
        {
            return "the cookie needs a name=value pair";
        }

        if (name.Length == 0 || name.Any(c => char.IsControl(c) || c is ' ' or ',' or '='))
        {
            return "the cookie name is empty or holds a control character, space or ','";
        }

        return value.Any(char.IsControl) ? "the cookie value holds a control character" : string.Empty;
    }

    private static string CollectAttributes(string[] segments, out Dictionary<string, string?> attributes)
    {
        attributes = [];
        foreach (string segment in segments.Skip(1))
        {
            string trimmed = segment.Trim();
            int equals = trimmed.IndexOf('=', StringComparison.Ordinal);
            string key = (equals < 0 ? trimmed : trimmed[..equals]).Trim().ToLowerInvariant();
            string? value = equals < 0 ? null : trimmed[(equals + 1)..].Trim();
            string problem = CheckAttribute(key, value, attributes);
            if (problem.Length > 0)
            {
                return problem;
            }

            attributes[key] = value;
        }

        return string.Empty;
    }

    private static string CheckAttribute(string key, string? value, Dictionary<string, string?> seen)
    {
        bool valued = ValuedAttributes.Contains(key, StringComparer.Ordinal);
        if (!valued && key is not ("secure" or "httponly"))
        {
            return $"the cookie attribute '{key}' is not one of Domain, Path, Expires, Max-Age, Secure, HttpOnly, SameSite, on-path or on-listener";
        }

        if (seen.ContainsKey(key))
        {
            return $"the cookie attribute '{key}' is given twice";
        }

        if (valued != !string.IsNullOrEmpty(value))
        {
            return valued ? $"the cookie attribute '{key}' needs a value" : $"the cookie attribute '{key}' takes no value";
        }

        return value is not null && value.Any(char.IsControl) ? $"the cookie attribute '{key}' holds a control character" : string.Empty;
    }

    private static string BuildDefinition(string name, string value, Dictionary<string, string?> attributes, out CookieDefinition? cookie)
    {
        cookie = null;
        string? expires = attributes.GetValueOrDefault("expires");
        string? maxAgeText = attributes.GetValueOrDefault("max-age");
        string? sameSite = SameSiteValues.FirstOrDefault(v => string.Equals(v, attributes.GetValueOrDefault("samesite"), StringComparison.OrdinalIgnoreCase));
        long maxAge = 0;
        string problem = string.Empty;
        if (expires is not null && !DateTimeOffset.TryParseExact(expires, ImfFixdate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
        {
            problem = "the cookie attribute 'expires' is not an IMF-fixdate such as 'Wed, 09 Jun 2027 10:18:14 GMT'";
        }
        else if (maxAgeText is not null && !long.TryParse(maxAgeText, NumberStyles.None, CultureInfo.InvariantCulture, out maxAge))
        {
            problem = "the cookie attribute 'max-age' is not a non-negative integer";
        }
        else if (attributes.ContainsKey("samesite") && sameSite is null)
        {
            problem = "the cookie attribute 'samesite' is not Strict, Lax or None";
        }

        if (problem.Length == 0)
        {
            cookie = new CookieDefinition(
                name,
                value,
                attributes.GetValueOrDefault("domain"),
                attributes.GetValueOrDefault("path"),
                expires,
                maxAgeText is null ? null : maxAge,
                attributes.ContainsKey("secure"),
                attributes.ContainsKey("httponly"),
                sameSite,
                attributes.GetValueOrDefault("on-path"),
                attributes.GetValueOrDefault("on-listener"));
        }

        return problem;
    }
}
