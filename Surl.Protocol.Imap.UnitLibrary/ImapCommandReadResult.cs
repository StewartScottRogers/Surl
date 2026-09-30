namespace Surl.Protocol.Imap;

/// <summary>
/// The result of reading one IMAP command.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Tag">The tag of the command's first line, when that line was read and its tag
/// is valid; otherwise <see langword="null"/>.</param>
/// <param name="Command">The command, when <paramref name="Outcome"/> is
/// <see cref="ImapCommandReadOutcome.CommandRead"/>.</param>
internal sealed record ImapCommandReadResult(ImapCommandReadOutcome Outcome, string? Tag, ImapCommandText? Command);
