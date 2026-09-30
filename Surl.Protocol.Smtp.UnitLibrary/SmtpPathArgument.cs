using System.Text;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The argument of <c>MAIL FROM:</c> or <c>RCPT TO:</c> read into its path and parameters
/// (ADR-0053, decision 4): the keyword matched without regard to case, at most one space, the
/// path in angle brackets, then the parameters, each after a space.
/// </summary>
/// <param name="Path">The bytes between the angle brackets as text, empty for <c>&lt;&gt;</c>.</param>
/// <param name="Parameters">The parameters after the path, in the order sent.</param>
internal sealed record SmtpPathArgument(string Path, IReadOnlyList<string> Parameters)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads <paramref name="argument"/> after <paramref name="keyword"/>.
    /// </summary>
    /// <param name="argument">The command's argument, or <see langword="null"/> when it has none.</param>
    /// <param name="keyword">The keyword the path follows: <c>FROM:</c> or <c>TO:</c>.</param>
    /// <param name="pathArgument">The path and parameters, when the argument is well formed.</param>
    /// <returns><see langword="false"/> when the argument is missing, is not UTF-8, lacks the
    /// keyword or the brackets, holds a <c>&lt;</c> inside them, or runs on after the
    /// <c>&gt;</c> without a space.</returns>
    public static bool TryRead(byte[]? argument, string keyword, out SmtpPathArgument? pathArgument)
    {
        var text = Decode(argument);
        pathArgument = text is not null && text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
            ? ReadAfterKeyword(text[keyword.Length..])
            : null;
        return pathArgument is not null;
    }

    private static string? Decode(byte[]? argument)
    {
        try
        {
            return argument is null ? null : StrictUtf8.GetString(argument);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static SmtpPathArgument? ReadAfterKeyword(string rest)
    {
        rest = rest.StartsWith(' ') ? rest[1..] : rest;
        var close = rest.IndexOf('>');
        return IsBracketedPath(rest, close) && IsParameterStart(rest, close + 1)
            ? new SmtpPathArgument(rest[1..close], rest[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            : null;
    }

    private static bool IsBracketedPath(string rest, int close) =>
        rest.StartsWith('<') && close > 0 && rest.IndexOf('<', 1, close - 1) < 0;

    private static bool IsParameterStart(string rest, int index) => index == rest.Length || rest[index] == ' ';
}
