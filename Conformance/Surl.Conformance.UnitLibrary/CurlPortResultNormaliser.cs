using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Removes from a Phase 7 run what differs between any two runs whoever the client is, so what is
/// left can be compared (ADR-0089 decision 4's normalisation column). Text is read as Latin-1, so
/// every byte the rules do not replace survives unchanged.
/// </summary>
public static class CurlPortResultNormaliser
{
    /// <summary>The text a port number is replaced by.</summary>
    public const string PortPlaceholder = "<port>";

    /// <summary>The text a <c>Date:</c> or <c>Last-Modified:</c> field value is replaced by.</summary>
    public const string DatePlaceholder = "<date>";

    /// <summary>The text a measured time, speed or time stamp is replaced by.</summary>
    public const string TimePlaceholder = "<time>";

    /// <summary>The text a <c>User-Agent:</c> field value in request bytes is replaced by.</summary>
    public const string UserAgentPlaceholder = "<user-agent>";

    /// <summary>The text a multipart boundary in request bytes is replaced by, everywhere it occurs.</summary>
    public const string BoundaryPlaceholder = "<boundary>";

    /// <summary>The text a Digest <c>cnonce</c> value in request bytes is replaced by.</summary>
    public const string CnoncePlaceholder = "<cnonce>";

    /// <summary>The text a Digest <c>response</c> value in request bytes is replaced by.</summary>
    public const string DigestResponsePlaceholder = "<response>";

    /// <summary>
    /// The text a Digest <c>nonce</c> value in request bytes is replaced by: the nonce is the one each run's
    /// own <c>surl</c> chose at random, which the client only echoes.
    /// </summary>
    public const string DigestNoncePlaceholder = "<nonce>";

    private const string TimedGroup = "timed";

    private static readonly Regex DateField = new(
        @"^((?:Date|Last-Modified):[ \t]*)[^\r\n]*", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex LoopbackPort = new(
        @"(?<=(?:127\.0\.0\.1|\[::1\]|localhost)(?::| port ))[0-9]+", RegexOptions.CultureInvariant);

    private static readonly Regex TimeStamp = new(@"[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{6}", RegexOptions.CultureInvariant);

    private static readonly Regex WriteOutVariable = new(@"%\{([^}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Normalises a client's standard output: the <c>surl</c> port, <c>Date:</c> and
    /// <c>Last-Modified:</c> field values, and the values of <c>-w</c> variables that measure time
    /// (<c>%{time_*}</c>, <c>%{speed_*}</c>).
    /// </summary>
    /// <param name="output">The bytes the client wrote.</param>
    /// <param name="surlPort">The port that run's <c>surl</c> listened on.</param>
    /// <param name="arguments">The case's arguments, read for a <c>-w</c> or <c>--write-out</c> format.</param>
    /// <returns>The normalised bytes.</returns>
    public static byte[] NormaliseOutput(byte[] output, int surlPort, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Encoding.Latin1.GetBytes(NormaliseOutputText(Encoding.Latin1.GetString(output), surlPort, arguments));
    }

    /// <summary>
    /// Normalises the standard error lines the mirrored test asserts on, as standard output is
    /// normalised; every other line is dropped, so the port is not held to upstream's wording where
    /// the mirrored test does not pin it.
    /// </summary>
    /// <param name="standardError">The text the client wrote to stderr.</param>
    /// <param name="surlPort">The port that run's <c>surl</c> listened on.</param>
    /// <param name="arguments">The case's arguments.</param>
    /// <param name="assertedFragments">The fragments of the lines the mirrored test asserts on.</param>
    /// <returns>The kept lines, normalised, each ending in a line feed.</returns>
    public static string NormaliseStandardError(
        string standardError, int surlPort, IReadOnlyList<string> arguments, IReadOnlyList<string> assertedFragments)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(assertedFragments);
        var kept = standardError
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => assertedFragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)));
        return string.Concat(kept.Select(line => NormaliseOutputText(line, surlPort, arguments) + "\n"));
    }

    /// <summary>
    /// Normalises <c>surl</c>'s <c>-v</c> notes: its own port, any client port beside a loopback
    /// address, and time stamps.
    /// </summary>
    /// <param name="notes">The notes.</param>
    /// <param name="surlPort">The port that run's <c>surl</c> listened on.</param>
    /// <returns>The normalised notes.</returns>
    public static string NormaliseSurlNotes(string notes, int surlPort)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var text = LoopbackPort.Replace(ReplacePort(notes, surlPort), PortPlaceholder);
        return TimeStamp.Replace(text, TimePlaceholder);
    }

    /// <summary>
    /// Normalises the bytes a client sent on one connection (ADR-0089 decision 4): the port the client
    /// was pointed at, the client ports FTP's <c>PORT</c> and <c>EPRT</c> name, the <c>User-Agent:</c>
    /// value, every multipart boundary, Digest's <c>nonce</c> (surl's own, echoed), <c>cnonce</c> and <c>response</c> (also inside a base64
    /// SASL <c>DIGEST-MD5</c> line), and the NTLM client challenge, time stamp and what is derived from
    /// them (the LM and NT responses, the encrypted session key and the MIC are zeroed, raw or in base64).
    /// </summary>
    /// <param name="requestBytes">The bytes sent.</param>
    /// <param name="clientFacingPort">The port the client connected to: the recording relay's.</param>
    /// <returns>The normalised bytes.</returns>
    public static byte[] NormaliseRequestBytes(byte[] requestBytes, int clientFacingPort)
    {
        ArgumentNullException.ThrowIfNull(requestBytes);
        var text = RequestBytesNormalisation.Normalise(
            Encoding.Latin1.GetBytes(ReplacePort(Encoding.Latin1.GetString(requestBytes), clientFacingPort)));
        return Encoding.Latin1.GetBytes(text);
    }

    private static string NormaliseOutputText(string text, int surlPort, IReadOnlyList<string> arguments)
    {
        text = DateField.Replace(ReplacePort(text, surlPort), "$1" + DatePlaceholder);
        var timedWriteOut = TimedWriteOutPattern(arguments);
        return timedWriteOut is null ? text : timedWriteOut.Replace(text, match => ReplaceTimedValues(match));
    }

    private static string ReplacePort(string text, int surlPort) =>
        Regex.Replace(
            text,
            "(?<![0-9])" + surlPort.ToString(CultureInfo.InvariantCulture) + "(?![0-9])",
            PortPlaceholder,
            RegexOptions.CultureInvariant);

    /// <summary>
    /// A pattern matching what the case's <c>-w</c> format writes, its time-measuring values captured,
    /// or <see langword="null"/> when the case writes none.
    /// </summary>
    private static Regex? TimedWriteOutPattern(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var format = WriteOutFormat(arguments);
        if (format is null || !WriteOutVariable.Matches(format).Any(variable => IsTimed(variable.Groups[1].Value)))
        {
            return null;
        }

        var pieces = WriteOutVariable.Split(format);
        var pattern = new StringBuilder();
        for (var index = 0; index < pieces.Length; index++)
        {
            pattern.Append(index % 2 == 0 ? Regex.Escape(UnescapeWriteOutLiteral(pieces[index])) : VariablePattern(pieces[index]));
        }

        return new Regex(pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    private static string? WriteOutFormat(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] is "-w" or "--write-out")
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static bool IsTimed(string variable) =>
        variable.StartsWith("time_", StringComparison.Ordinal) || variable.StartsWith("speed_", StringComparison.Ordinal);

    private static string VariablePattern(string variable) =>
        IsTimed(variable) ? $"(?<{TimedGroup}>[0-9]+(?:\\.[0-9]+)?)" : ".*?";

    private static string UnescapeWriteOutLiteral(string literal) =>
        literal
            .Replace("%%", "%", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

    private static string ReplaceTimedValues(Match match)
    {
        var text = new StringBuilder();
        var at = match.Index;
        foreach (Capture value in match.Groups[TimedGroup].Captures)
        {
            text.Append(match.Value, at - match.Index, value.Index - at).Append(TimePlaceholder);
            at = value.Index + value.Length;
        }

        return text.Append(match.Value, at - match.Index, match.Index + match.Length - at).ToString();
    }
}
