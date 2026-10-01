namespace Surl.Protocol.Ws;

/// <summary>
/// The status codes a close frame may carry on the wire (RFC 6455 section 7.4).
/// </summary>
/// <remarks>
/// Allowed: 1000-1003 and 1007-1011, which section 7.4.1 defines; 1012-1014, which the IANA
/// WebSocket Close Code Number Registry has since assigned from the range section 7.4.2 reserves
/// for it; and 3000-4999, the registered and private-use ranges. Everything else is refused:
/// 0-999 is never used, 1004 is reserved, 1005, 1006 and 1015 must never be sent in a close frame,
/// and the rest of 1000-2999 is unassigned.
/// </remarks>
internal static class WebSocketCloseCodes
{
    /// <summary>
    /// Whether <paramref name="code"/> may appear in a close frame.
    /// </summary>
    /// <param name="code">The status code, the first two payload bytes of a close frame in network order.</param>
    /// <returns><see langword="true"/> when section 7.4 and the IANA registry allow it on the wire.</returns>
    public static bool IsAllowedOnTheWire(int code) =>
        code >= 3000 ? code <= 4999 : IsAssignedByTheRfcOrIana(code);

    // 1000-1014 less 1004-1006.
    private static bool IsAssignedByTheRfcOrIana(int code) => (uint)(code - 1000) <= 14 && (uint)(code - 1004) > 2;
}
