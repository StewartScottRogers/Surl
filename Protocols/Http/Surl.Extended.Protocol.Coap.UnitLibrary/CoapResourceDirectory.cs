using System.Globalization;
using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Writes <c>/.well-known/core</c> (RFC 7252 section 7.2; RFC 6690; ADR-0097 decision 1): one link
/// per visible file of the content store, <c>&lt;/path&gt;;ct=&lt;format&gt;;sz=&lt;length&gt;;obs</c>,
/// in ordinal order of path, at most <see cref="MaxLinks"/>, filtered by one query argument.
/// </summary>
/// <remarks>
/// The store is walked from its root through its own listing, so every exposure rule applies: a
/// dot-file, a link that is not followed and <c>/.surl</c> are never listed, and an entry the store
/// is listed only when its name passes the store's path rules. The walk goes at most <see cref="MaxDepth"/> directories
/// deep, which also ends a cycle of followed symbolic links. Each path segment is percent-encoded
/// as RFC 3986 requires.
/// </remarks>
internal static class CoapResourceDirectory
{
    /// <summary>The most links written (ADR-0006: every answer a peer can grow is bounded).</summary>
    public const int MaxLinks = 1000;

    /// <summary>The deepest directory walked below the root.</summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// Writes the link format for <paramref name="store"/>.
    /// </summary>
    /// <param name="store">The content store; its listing switch must be on.</param>
    /// <param name="queries">
    /// The request's Uri-Query arguments: the first <c>name=value</c> or <c>name=prefix*</c> filters
    /// the links on <c>href</c>, <c>ct</c>, <c>sz</c> or <c>obs</c> (RFC 6690 section 4.1); the rest
    /// are ignored.
    /// </param>
    /// <param name="cancellationToken">Checked while the store is listed.</param>
    /// <returns>The payload's UTF-8 bytes; empty when no link is left.</returns>
    public static byte[] Write(ContentStore store, IReadOnlyList<string> queries, CancellationToken cancellationToken)
    {
        var links = new List<Link>();
        Walk(store, string.Empty, 0, links, cancellationToken);
        var filter = queries.FirstOrDefault(query => query.Contains('=', StringComparison.Ordinal));

        var written = links
            .Where(link => filter is null || link.Matches(filter))
            .OrderBy(link => link.Href, StringComparer.Ordinal)
            .Take(MaxLinks)
            .Select(link => link.ToString());

        return Encoding.UTF8.GetBytes(string.Join(',', written));
    }

    private static void Walk(ContentStore store, string directoryHref, int depth, List<Link> links, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapRequestPath(directoryHref + "/"), cancellationToken);

        foreach (var entry in listing.Entries)
        {
            var href = directoryHref + "/" + Uri.EscapeDataString(entry.Name);

            if (entry.Kind == ContentEntryKind.File)
            {
                links.Add(new Link(href, CoapContentFormat.ForFileName(entry.Name), entry.Length.GetValueOrDefault()));
            }
            else if (depth < MaxDepth)
            {
                Walk(store, href, depth + 1, links, cancellationToken);
            }
        }
    }

    private sealed record Link(string Href, uint Format, long Size)
    {
        public bool Matches(string filter)
        {
            var separator = filter.IndexOf('=', StringComparison.Ordinal);
            var pattern = filter[(separator + 1)..];
            var value = AttributeValue(filter[..separator]);

            if (value is null)
            {
                return false;
            }

            return pattern.EndsWith('*')
                ? value.StartsWith(pattern[..^1], StringComparison.Ordinal)
                : string.Equals(value, pattern, StringComparison.Ordinal);
        }

        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"<{Href}>;ct={Format};sz={Size};obs");

        private string? AttributeValue(string name) => name switch
        {
            "href" => Href,
            "ct" => Format.ToString(CultureInfo.InvariantCulture),
            "sz" => Size.ToString(CultureInfo.InvariantCulture),
            "obs" => string.Empty,
            _ => null,
        };
    }
}
