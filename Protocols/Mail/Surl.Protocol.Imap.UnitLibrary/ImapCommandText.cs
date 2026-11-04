namespace Surl.Protocol.Imap;

/// <summary>
/// One IMAP command as it arrived: its lines, each without its CRLF, and the synchronizing
/// literal that followed each line but the last (RFC 3501, section 4.3). A line followed by a
/// literal is kept without its <c>{n}</c>, so literal <c>i</c> comes right after line <c>i</c>.
/// </summary>
/// <param name="Lines">The lines, one more than <paramref name="Literals"/>.</param>
/// <param name="Literals">The literals' bytes, in order.</param>
internal sealed record ImapCommandText(IReadOnlyList<byte[]> Lines, IReadOnlyList<byte[]> Literals);
