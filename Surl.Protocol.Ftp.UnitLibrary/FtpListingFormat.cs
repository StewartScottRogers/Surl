using System.Globalization;
using System.Text;
using Surl.Content;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Formats the lines of a directory listing, one per content-store entry: <c>LIST</c>'s Unix
/// <c>ls -l</c> form, <c>NLST</c>'s bare names, and <c>MLSD</c>'s and <c>MLST</c>'s RFC 3659
/// facts (ADR-0052, decision 7).
/// </summary>
/// <remarks>
/// Every time is the entry's last write in UTC, formatted with the invariant culture, so a
/// listing never depends on the machine's culture or time zone. A name is written as the file
/// system spells it; <see cref="Encode"/> turns the lines into the UTF-8 bytes sent over the data
/// connection, each ended by CRLF.
/// </remarks>
internal static class FtpListingFormat
{
    /// <summary>How recent a last write must be for <see cref="LongLine"/> to show its time rather than its year.</summary>
    public static readonly TimeSpan RecentWriteAge = TimeSpan.FromDays(180);

    /// <summary>
    /// <c>LIST</c>'s line: <c>-rw-r--r-- 1 surl surl &lt;size&gt; &lt;date&gt; &lt;name&gt;</c> for a
    /// file and <c>drwxr-xr-x 1 surl surl 0 &lt;date&gt; &lt;name&gt;</c> for a directory, the size
    /// right-aligned in 12 columns, the date <c>MMM dd HH:mm</c> (day space-padded) for a write
    /// within <see cref="RecentWriteAge"/> before <paramref name="now"/>, else <c>MMM dd  yyyy</c>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="now">The server's clock, which says whether the write is recent.</param>
    /// <returns>The line, without its line ending.</returns>
    public static string LongLine(ContentDirectoryEntry entry, DateTimeOffset now)
    {
        var written = entry.LastModifiedUtc.UtcDateTime;
        var age = now - entry.LastModifiedUtc;
        var timeOrYear = age >= TimeSpan.Zero && age <= RecentWriteAge
            ? written.ToString("HH:mm", CultureInfo.InvariantCulture)
            : written.ToString(" yyyy", CultureInfo.InvariantCulture);
        var mode = entry.Kind == ContentEntryKind.Directory ? "drwxr-xr-x" : "-rw-r--r--";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{mode} 1 surl surl {entry.Length ?? 0,12} {written.ToString("MMM", CultureInfo.InvariantCulture)} {written.Day,2} {timeOrYear} {entry.Name}");
    }

    /// <summary>
    /// <c>NLST</c>'s line: the name alone.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="now">Unused: a name has no date.</param>
    /// <returns>The line, without its line ending.</returns>
    public static string NameLine(ContentDirectoryEntry entry, DateTimeOffset now) => entry.Name;

    /// <summary>
    /// <c>MLSD</c>'s line: <see cref="Facts"/>, a space and the name.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="now">Unused: a fact's time is absolute.</param>
    /// <returns>The line, without its line ending.</returns>
    public static string FactsLine(ContentDirectoryEntry entry, DateTimeOffset now) => $"{Facts(entry)} {entry.Name}";

    /// <summary>
    /// The RFC 3659 facts of an entry: <c>type=file;size=&lt;n&gt;;modify=&lt;yyyyMMddHHmmss&gt;;</c>
    /// or <c>type=dir;modify=&lt;yyyyMMddHHmmss&gt;;</c>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The facts, each ended by <c>;</c>.</returns>
    public static string Facts(ContentDirectoryEntry entry)
    {
        var modify = entry.LastModifiedUtc.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        return entry.Kind == ContentEntryKind.Directory
            ? $"type=dir;modify={modify};"
            : string.Create(CultureInfo.InvariantCulture, $"type=file;size={entry.Length};modify={modify};");
    }

    /// <summary>
    /// The bytes of a listing: each entry's line as <paramref name="formatLine"/> writes it, in
    /// the order given, in UTF-8, each ended by CRLF.
    /// </summary>
    /// <param name="entries">The entries, in the order they are listed.</param>
    /// <param name="formatLine">Writes one entry's line.</param>
    /// <param name="now">The server's clock, passed to <paramref name="formatLine"/>.</param>
    /// <returns>The listing's bytes; none for no entries.</returns>
    public static byte[] Encode(
        IEnumerable<ContentDirectoryEntry> entries, Func<ContentDirectoryEntry, DateTimeOffset, string> formatLine, DateTimeOffset now) =>
        Encoding.UTF8.GetBytes(string.Concat(entries.Select(entry => formatLine(entry, now) + "\r\n")));
}
