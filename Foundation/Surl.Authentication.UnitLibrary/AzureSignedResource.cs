using System.Globalization;

namespace Surl.Authentication;

/// <summary>
/// A service shared access signature's canonicalized resource ("Create a service SAS",
/// "Signed resource"): <c>/blob/&lt;account&gt;/&lt;container&gt;</c> for <c>sr=c</c>, with
/// <c>/&lt;blob&gt;</c> after it for <c>sr=b</c>, and with the first <c>sdd</c> segments of the
/// path for <c>sr=d</c>. The resource is taken from the request, so a signature for another
/// resource never verifies.
/// </summary>
internal static class AzureSignedResource
{
    /// <summary>
    /// The canonicalized resource, or <see langword="null"/> when the request does not address a
    /// resource of the signed kind, or the kind is one not served (<c>bs</c>, <c>bv</c>).
    /// </summary>
    /// <param name="accountName">The account the request names.</param>
    /// <param name="resourcePath">The decoded resource path, <c>/&lt;container&gt;/&lt;name&gt;</c>.</param>
    /// <param name="signedResource">The <c>sr</c> field.</param>
    /// <param name="directoryDepth">The <c>sdd</c> field, which <c>sr=d</c> needs.</param>
    /// <returns>The resource, or <see langword="null"/>.</returns>
    public static string? Canonicalize(string accountName, string resourcePath, string? signedResource, string? directoryDepth)
    {
        var path = resourcePath.TrimStart('/');
        var slash = path.IndexOf('/', StringComparison.Ordinal);
        var container = slash < 0 ? path : path[..slash];
        var name = slash < 0 ? string.Empty : path[(slash + 1)..];
        if (container.Length == 0)
        {
            return null;
        }

        var prefix = $"/blob/{accountName}/{container}";

        return signedResource switch
        {
            "c" => prefix,
            "b" when name.Length > 0 => $"{prefix}/{name}",
            "d" => DirectoryOf(name, directoryDepth) is { } directory ? $"{prefix}/{directory}" : null,
            _ => null,
        };
    }

    // The first sdd segments of the name: sdd from 1 to the number of segments.
    private static string? DirectoryOf(string name, string? directoryDepth)
    {
        var segments = name.Split('/');
        var depth = int.TryParse(directoryDepth, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        return name.Length > 0 && depth >= 1 && depth <= segments.Length
            ? string.Join('/', segments[..depth])
            : null;
    }
}
