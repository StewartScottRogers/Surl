using System.Globalization;

namespace Surl.Protocol.Smtp;

/// <summary>
/// Checks the parameters of <c>MAIL FROM:</c> (ADR-0053, decision 4): <c>SIZE=</c>,
/// <c>BODY=7BIT</c> or <c>BODY=8BITMIME</c>, <c>SMTPUTF8</c> and <c>AUTH=</c>, each at most once,
/// matched without regard to case.
/// </summary>
internal static class SmtpMailParameters
{
    private const int MaxSizeDigits = 20;

    // Each known keyword's check of its value (null when it has no '='), given --max-filesize.
    private static readonly Dictionary<string, Func<string?, long, string?>> Checks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SIZE"] = CheckSize,
        ["BODY"] = (value, _) => IsBodyType(value) ? null : SmtpReplies.InvalidBody,
        ["SMTPUTF8"] = (value, _) => value is null ? null : SmtpReplies.UnsupportedParameter,
        ["AUTH"] = (value, _) => value is null ? SmtpReplies.UnsupportedParameter : null,
    };

    /// <summary>
    /// The reply refusing the first parameter that is refused, or <see langword="null"/> when
    /// every one is accepted.
    /// </summary>
    /// <param name="parameters">The parameters, in the order sent.</param>
    /// <param name="maxUploadBytes"><c>--max-filesize</c>; 0 means no limit.</param>
    /// <returns>The refusal's reply line, or <see langword="null"/>.</returns>
    public static string? FindRefusal(IReadOnlyList<string> parameters, long maxUploadBytes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return parameters.Select(parameter => Check(parameter, seen, maxUploadBytes)).FirstOrDefault(refusal => refusal is not null);
    }

    // The refusal of one parameter, or null; a keyword already in seen is refused as unsupported.
    private static string? Check(string parameter, HashSet<string> seen, long maxUploadBytes)
    {
        var equals = parameter.IndexOf('=');
        var keyword = equals < 0 ? parameter : parameter[..equals];
        var value = equals < 0 ? null : parameter[(equals + 1)..];
        return seen.Add(keyword) && Checks.TryGetValue(keyword, out var check)
            ? check(value, maxUploadBytes)
            : SmtpReplies.UnsupportedParameter;
    }

    private static bool IsBodyType(string? value) =>
        string.Equals(value, "7BIT", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "8BITMIME", StringComparison.OrdinalIgnoreCase);

    private static string? CheckSize(string? value, long maxUploadBytes)
    {
        if (!IsSizeValue(value))
        {
            return SmtpReplies.InvalidSize;
        }

        return maxUploadBytes > 0 && decimal.Parse(value!, NumberStyles.None, CultureInfo.InvariantCulture) > maxUploadBytes
            ? SmtpReplies.SizeTooLarge
            : null;
    }

    private static bool IsSizeValue(string? value) =>
        value is { Length: > 0 and <= MaxSizeDigits } && !value.AsSpan().ContainsAnyExceptInRange('0', '9');
}
