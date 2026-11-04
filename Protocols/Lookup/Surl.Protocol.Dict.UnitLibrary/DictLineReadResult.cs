namespace Surl.Protocol.Dict;

/// <summary>
/// The result of reading one DICT command line: the line, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Line">
/// The line decoded as UTF-8, without its LF or a CR before it; <see langword="null"/>
/// unless <paramref name="Outcome"/> is <see cref="DictLineReadOutcome.LineRead"/>.
/// </param>
internal sealed record DictLineReadResult(DictLineReadOutcome Outcome, string? Line)
{
    /// <summary>
    /// A result that carries a line.
    /// </summary>
    /// <param name="line">The decoded line.</param>
    /// <returns>A <see cref="DictLineReadOutcome.LineRead"/> result.</returns>
    public static DictLineReadResult Read(string line) => new(DictLineReadOutcome.LineRead, line);

    /// <summary>
    /// A result that carries no line.
    /// </summary>
    /// <param name="outcome">Why there is no line.</param>
    /// <returns>A result with <see cref="Line"/> <see langword="null"/>.</returns>
    public static DictLineReadResult NoLine(DictLineReadOutcome outcome) => new(outcome, null);
}
