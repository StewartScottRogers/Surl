using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Surl.Cli;

/// <summary>
/// Expands a <c>-w</c> / <c>--write-out</c> format for one exchange, by the rules measured from upstream
/// curl 8.21.0 (BL-1663): <c>%{name}</c> is a variable, <c>%%</c> is a percent sign, <c>\r</c>, <c>\n</c>
/// and <c>\t</c> are the control characters, every other <c>\</c> and <c>%</c> is itself (so is a
/// <c>%{</c> with no closing brace), and an unknown <c>%{name}</c> prints nothing and is reported.
/// <c>%{json}</c> is every variable as one JSON object.
/// </summary>
public static class WriteOutFormat
{
    /// <summary>The variable names, in ordinal order; <c>json</c> is one of them.</summary>
    public static IReadOnlyList<string> VariableNames { get; } =
        ["exchange_id", "json", "listen_url", "remote_ip", "remote_port", "scheme", "size_download", "size_upload", "time_total"];

    /// <summary>Expands <paramref name="format"/> for one exchange.</summary>
    /// <param name="format">The text given to <c>-w</c>.</param>
    /// <param name="values">What the exchange tells.</param>
    /// <returns>The text and the unknown variable names.</returns>
    public static WriteOutResult Expand(string format, WriteOutValues values)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(values);
        var text = new StringBuilder();
        var unknown = new List<string>();
        var index = 0;
        while (index < format.Length)
        {
            index += ReadOne(format, index, values, text, unknown);
        }

        return new WriteOutResult(text.ToString(), unknown);
    }

    // Appends what the character at index stands for and returns how many characters it used.
    private static int ReadOne(string format, int index, WriteOutValues values, StringBuilder text, List<string> unknown)
    {
        var next = index + 1 < format.Length ? format[index + 1] : '\0';
        switch (format[index])
        {
            case '%' when next == '%':
                text.Append('%');
                return 2;
            case '%' when next == '{' && format.IndexOf('}', index + 2) is var close and >= 0:
                AppendVariable(format[(index + 2)..close], values, text, unknown);
                return close + 1 - index;
            case '\\' when next is 'r' or 'n' or 't':
                text.Append(next == 'r' ? '\r' : next == 'n' ? '\n' : '\t');
                return 2;
            default:
                text.Append(format[index]);
                return 1;
        }
    }

    private static void AppendVariable(string name, WriteOutValues values, StringBuilder text, List<string> unknown)
    {
        var value = Resolve(name, values);
        if (value is null)
        {
            unknown.Add(name);
            return;
        }

        text.Append(value);
    }

    private static string? Resolve(string name, WriteOutValues values) => name switch
    {
        "exchange_id" => Number(values.ExchangeId),
        "json" => Json(values),
        "listen_url" => values.ListenUrl,
        "remote_ip" => values.RemoteIp,
        "remote_port" => Number(values.RemotePort),
        "scheme" => values.Scheme,
        "size_download" => Number(values.SizeDownload),
        "size_upload" => Number(values.SizeUpload),
        "time_total" => values.TimeTotal.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Quoted(string value) => "\"" + JsonEncodedText.Encode(value) + "\"";

    private static string Json(WriteOutValues values) =>
        "{"
        + $"\"exchange_id\":{Number(values.ExchangeId)},"
        + $"\"listen_url\":{Quoted(values.ListenUrl)},"
        + $"\"remote_ip\":{Quoted(values.RemoteIp)},"
        + $"\"remote_port\":{Number(values.RemotePort)},"
        + $"\"scheme\":{Quoted(values.Scheme)},"
        + $"\"size_download\":{Number(values.SizeDownload)},"
        + $"\"size_upload\":{Number(values.SizeUpload)},"
        + $"\"time_total\":{Resolve("time_total", values)}"
        + "}";
}
