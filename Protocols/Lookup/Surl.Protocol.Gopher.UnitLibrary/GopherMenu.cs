using System.Globalization;
using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Writes the RFC 1436 menus the Gopher server sends: a directory's entries, and the one-item
/// error menu.
/// </summary>
internal static class GopherMenu
{
    /// <summary>
    /// The error menu: one item of type <c>3</c> with fixed text, an empty selector, the
    /// conventional host <c>error.host</c> and port 1, then the <c>.</c> line.
    /// </summary>
    public static readonly byte[] NothingServedHere =
        "3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n"u8.ToArray();

    // Item types by file extension (RFC 1436 section 3.8, and the widely used h and I);
    // any other file is 9, binary.
    private static readonly Dictionary<string, char> FileItemTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = '0',
        [".text"] = '0',
        [".md"] = '0',
        [".csv"] = '0',
        [".log"] = '0',
        [".gif"] = 'g',
        [".png"] = 'I',
        [".jpg"] = 'I',
        [".jpeg"] = 'I',
        [".bmp"] = 'I',
        [".html"] = 'h',
        [".htm"] = 'h',
    };

    /// <summary>
    /// Returns the menu of a directory: one item per entry, in the listing's order, then the
    /// <c>.</c> line.
    /// </summary>
    /// <param name="directoryPath">The directory's request path, the base of every item's selector.</param>
    /// <param name="entries">The directory's entries, as the content store listed them.</param>
    /// <param name="context">The exchange, whose listen URL and local endpoint give each item's host and port.</param>
    /// <returns>The menu's bytes, UTF-8.</returns>
    public static byte[] ForDirectory(string directoryPath, IReadOnlyList<ContentDirectoryEntry> entries, ExchangeContext context)
    {
        var hostAndPort = string.Create(CultureInfo.InvariantCulture, $"\t{MenuHost(context)}\t{context.ListenUrl.BoundPort ?? context.ListenUrl.Port}\r\n");
        var menu = new StringBuilder();
        foreach (var entry in entries)
        {
            menu.Append(ItemType(entry))
                .Append(entry.Name)
                .Append('\t')
                .Append(GopherSelector.ForEntry(directoryPath, entry.Name))
                .Append(hostAndPort);
        }

        return Encoding.UTF8.GetBytes(menu.Append(".\r\n").ToString());
    }

    /// <summary>
    /// Returns the item type of an entry: <c>1</c> for a directory; for a file, the type its
    /// extension names in the table above, or <c>9</c>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The item type character.</returns>
    public static char ItemType(ContentDirectoryEntry entry) =>
        entry.Kind == ContentEntryKind.Directory
            ? '1'
            : FileItemTypesByExtension.GetValueOrDefault(Path.GetExtension(entry.Name), '9');

    /// <summary>
    /// Returns the host every item names: the listen URL's host, unless it is the wildcard
    /// <c>0.0.0.0</c> or <c>::</c>, which no client can connect to; then the address the
    /// client reached, the connection's local address, IPv4-mapped addresses as IPv4 and
    /// IPv6 addresses without their zone.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <returns>A host name or address literal, without brackets.</returns>
    public static string MenuHost(ExchangeContext context)
    {
        var isWildcard = IPAddress.TryParse(context.ListenUrl.Host, out var listenAddress)
            && (listenAddress.Equals(IPAddress.Any) || listenAddress.Equals(IPAddress.IPv6Any));
        if (!isWildcard || context.LocalEndPoint is not IPEndPoint local)
        {
            return context.ListenUrl.Host;
        }

        // Rebuilt from its bytes, an IPv6 address loses its zone (%12), which names an
        // interface of this host and means nothing to the client.
        var address = local.Address.IsIPv4MappedToIPv6 ? local.Address.MapToIPv4() : local.Address;
        return new IPAddress(address.GetAddressBytes()).ToString();
    }
}
