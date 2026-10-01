namespace Surl.Protocol.Imap;

/// <summary>
/// What one command is answered with: its untagged lines, then its tagged completion.
/// </summary>
/// <param name="Untagged">The untagged lines, each without its CRLF, in order.</param>
/// <param name="Completion">The tagged completion's text, without the tag.</param>
internal sealed record ImapResponse(IReadOnlyList<string> Untagged, string Completion)
{
    /// <summary>
    /// A response with no untagged lines.
    /// </summary>
    /// <param name="completion">The tagged completion's text, without the tag.</param>
    /// <returns>The response.</returns>
    public static ImapResponse Only(string completion) => new([], completion);
}
