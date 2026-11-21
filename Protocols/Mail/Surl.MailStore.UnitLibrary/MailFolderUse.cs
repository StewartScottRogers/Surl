namespace Surl.MailStore;

/// <summary>
/// The folder uses RFC 6154 section 2 defines, which a mailbox may hold (ADR-0055, Amendment 1).
/// </summary>
public static class MailFolderUse
{
    /// <summary>Every use in its canonical spelling, a backslash and the attribute's name.</summary>
    public static IReadOnlyList<string> All { get; } = [@"\All", @"\Archive", @"\Drafts", @"\Flagged", @"\Junk", @"\Sent", @"\Trash"];

    /// <summary>
    /// The canonical spelling of <paramref name="use"/>, matched without regard to case and with or
    /// without the leading backslash.
    /// </summary>
    /// <param name="use">The use as given.</param>
    /// <returns>The canonical spelling, or <see langword="null"/> when it is no RFC 6154 section 2 use.</returns>
    public static string? Canonical(string? use)
    {
        var bare = use?.TrimStart('\\');
        return All.FirstOrDefault(known => string.Equals(known[1..], bare, StringComparison.OrdinalIgnoreCase));
    }
}
