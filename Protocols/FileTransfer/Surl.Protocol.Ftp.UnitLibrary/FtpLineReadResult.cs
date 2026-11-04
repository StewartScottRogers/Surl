namespace Surl.Protocol.Ftp;

/// <summary>
/// The result of reading one FTP command line: the line's bytes, or the reason there are none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Line">
/// The line's bytes as sent, without its LF or a CR before it; <see langword="null"/> unless
/// <paramref name="Outcome"/> is <see cref="FtpLineReadOutcome.LineRead"/>. They stay bytes so a
/// password reaches the authentication policy exactly as sent (ADR-0052, decision 3).
/// </param>
internal sealed record FtpLineReadResult(FtpLineReadOutcome Outcome, byte[]? Line)
{
    /// <summary>
    /// A result that carries a line.
    /// </summary>
    /// <param name="line">The line's bytes.</param>
    /// <returns>A <see cref="FtpLineReadOutcome.LineRead"/> result.</returns>
    public static FtpLineReadResult Read(byte[] line) => new(FtpLineReadOutcome.LineRead, line);

    /// <summary>
    /// A result that carries no line.
    /// </summary>
    /// <param name="outcome">Why there is no line.</param>
    /// <returns>A result with <see cref="Line"/> <see langword="null"/>.</returns>
    public static FtpLineReadResult NoLine(FtpLineReadOutcome outcome) => new(outcome, null);
}
