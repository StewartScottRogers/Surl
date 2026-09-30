namespace Surl.LineProtocol;

/// <summary>
/// The result of reading one CRLF-ended line: the line, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Line">
/// The line's bytes without its CRLF; a bare CR or a bare LF inside it is kept.
/// <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="CrlfLineReadOutcome.LineRead"/>.
/// </param>
public sealed record CrlfLineReadResult(CrlfLineReadOutcome Outcome, byte[]? Line)
{
    /// <summary>
    /// A result that carries a line.
    /// </summary>
    /// <param name="line">The line's bytes without its CRLF.</param>
    /// <returns>A <see cref="CrlfLineReadOutcome.LineRead"/> result.</returns>
    public static CrlfLineReadResult Read(byte[] line) => new(CrlfLineReadOutcome.LineRead, line);

    /// <summary>
    /// A result that carries no line.
    /// </summary>
    /// <param name="outcome">Why there is no line.</param>
    /// <returns>A result with <see cref="Line"/> <see langword="null"/>.</returns>
    public static CrlfLineReadResult NoLine(CrlfLineReadOutcome outcome) => new(outcome, null);
}
