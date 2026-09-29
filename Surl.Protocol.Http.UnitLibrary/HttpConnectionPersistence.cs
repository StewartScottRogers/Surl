namespace Surl.Protocol.Http;

/// <summary>
/// Decides whether a connection stays open after the response to a request (RFC 9112,
/// section 9.3), and what the response's <c>Connection</c> field says about it.
/// </summary>
internal static class HttpConnectionPersistence
{
    /// <summary>
    /// Whether the connection stays open for another request after this one is answered.
    /// </summary>
    /// <remarks>
    /// A <c>close</c> connection option closes it. An HTTP/1.1 request otherwise keeps it
    /// open, and an HTTP/1.0 request keeps it open only with a <c>keep-alive</c> option. A
    /// request that announces a body (<c>Transfer-Encoding</c>, or a <c>Content-Length</c>
    /// other than <c>0</c>) closes it too: the server does not read request bodies yet, so
    /// the next request could not be found after one.
    /// </remarks>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open.</returns>
    public static bool KeepsConnectionOpen(HttpRequestHead head)
    {
        var options = head.GetFieldValues("Connection")
            .SelectMany(value => value.Split(','))
            .Select(option => option.Trim(' ', '\t'))
            .ToArray();

        if (HasOption(options, "close") || AnnouncesBody(head))
        {
            return false;
        }

        return head.Version.Minor >= 1 || HasOption(options, "keep-alive");
    }

    /// <summary>
    /// The value of the response's <c>Connection</c> field.
    /// </summary>
    /// <param name="keepsConnectionOpen">Whether the connection stays open after the response.</param>
    /// <param name="requestVersion">The version the request was treated as.</param>
    /// <returns>
    /// <c>close</c> when the connection closes; <c>keep-alive</c> when an HTTP/1.0
    /// connection stays open; <see langword="null"/>, no field, when an HTTP/1.1 connection
    /// stays open, which is its default.
    /// </returns>
    public static string? ConnectionFieldValue(bool keepsConnectionOpen, Version requestVersion)
    {
        if (!keepsConnectionOpen)
        {
            return "close";
        }

        return requestVersion.Minor >= 1 ? null : "keep-alive";
    }

    private static bool HasOption(string[] options, string option) =>
        options.Contains(option, StringComparer.OrdinalIgnoreCase);

    private static bool AnnouncesBody(HttpRequestHead head) =>
        head.GetFieldValues("Transfer-Encoding").Count > 0
        || head.GetFieldValues("Content-Length").Any(value => value != "0");
}
