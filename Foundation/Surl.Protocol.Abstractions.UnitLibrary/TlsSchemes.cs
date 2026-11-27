namespace Surl.Protocol.Abstractions;

/// <summary>
/// The listen URL schemes that are TLS from the first byte by default (ADR-0010, section 1): schemes
/// upstream curl opens with a TLS handshake and extended schemes served over TLS, all listed in
/// <see cref="ImplicitTlsSchemes"/>. <see cref="ListenUrl.IsImplicitTls"/>, which the serving engine
/// and the listeners read, answers from this set unless it is set on the listen URL itself, as the
/// composition does to serve <c>s3</c> in plain text under <c>--s3-plaintext</c>.
/// </summary>
public static class TlsSchemes
{
    private static readonly HashSet<string> ImplicitTlsSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "wss", "ftps", "imaps", "pop3s", "smtps", "ldaps", "gophers", "mqtts", "smbs", "s3", "oci", "socketios", "xmpps", "stomps", "kafkas", "az", "abfss", "gs", "amqps", "mqtt5s", "signalrs", "rediss", "amqp1s", "mcps", "grpcs",
    };

    /// <summary>
    /// Whether a connection for <paramref name="scheme"/> starts with a TLS handshake.
    /// </summary>
    /// <param name="scheme">A URL scheme, compared case-insensitively.</param>
    /// <returns>
    /// <see langword="true"/> for exactly the schemes in <see cref="ImplicitTlsSchemes"/>.
    /// <c>ftps</c> is implicit FTPS; explicit FTPS is <c>ftp</c> with <c>AUTH TLS</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public static bool IsImplicitTls(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return ImplicitTlsSchemes.Contains(scheme);
    }
}
