namespace Surl.LineProtocol;

/// <summary>
/// The result of reading one dot-stuffed message body.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="BytesWritten">How many unstuffed body bytes were written to the destination.</param>
public sealed record DotStuffedBodyReadResult(DotStuffedBodyReadOutcome Outcome, long BytesWritten);
