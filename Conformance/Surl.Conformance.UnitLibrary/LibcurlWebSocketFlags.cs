namespace Surl.Conformance;

/// <summary>
/// libcurl's <c>CURLWS_*</c> frame flags (<c>include/curl/websockets.h</c> at tag <c>curl-8_21_0</c>),
/// by the names a libcurl WebSocket driver's script and report use.
/// </summary>
public static class LibcurlWebSocketFlags
{
    private static readonly (string Name, uint Value)[] Flags =
        [("TEXT", 1), ("BINARY", 2), ("CONT", 4), ("CLOSE", 8), ("PING", 16), ("OFFSET", 32), ("PONG", 64)];

    /// <summary>
    /// Gets the value of a flag a script may name: <c>TEXT</c>, <c>BINARY</c>, <c>CONT</c>,
    /// <c>CLOSE</c>, <c>PING</c> or <c>PONG</c> (never <c>OFFSET</c>, which a script does not send).
    /// </summary>
    /// <param name="name">The flag's name.</param>
    /// <returns>Its value, or <see langword="null"/> for any other name.</returns>
    public static uint? ValueOf(string name) =>
        Flags.Where(flag => flag.Name == name && flag.Name != "OFFSET").Select(flag => (uint?)flag.Value).FirstOrDefault();

    /// <summary>
    /// Names the flags set in <paramref name="flags"/>, <c>+</c>-joined in value order, or <c>0</c> for none.
    /// </summary>
    /// <param name="flags">The <c>CURLWS_*</c> bits.</param>
    /// <returns>The names, such as <c>TEXT+CONT</c>.</returns>
    public static string NamesOf(uint flags)
    {
        var names = string.Join('+', Flags.Where(flag => (flags & flag.Value) != 0).Select(flag => flag.Name));
        return names.Length == 0 ? "0" : names;
    }
}
