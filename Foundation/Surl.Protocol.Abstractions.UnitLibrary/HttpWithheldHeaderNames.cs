namespace Surl.Protocol.Abstractions;

/// <summary>
/// The HTTP header fields whose values surl withholds (ADR-0182): as <c>****</c> in the exchange log
/// of an HTTP/1.x or RTSP request head and in the canonical request a Signature Version 4 mismatch shows,
/// and with the whole header block's payloads in the exchange log of HTTP/2
/// (<see cref="Credentials"/> always; <see cref="ApiKeysAndSessionTokens"/> for gRPC and DNS over HTTPS).
/// Field names are matched without regard to case.
/// </summary>
public static class HttpWithheldHeaderNames
{
    /// <summary>
    /// <c>Authorization</c> and <c>Proxy-Authorization</c>, whose values start with a scheme word
    /// that the exchange log keeps before the <c>****</c>.
    /// </summary>
    public static IReadOnlyList<string> Credentials { get; } = Array.AsReadOnly(
        ["Authorization", "Proxy-Authorization"]);

    /// <summary>
    /// The API-key and session-token fields, whose whole value is withheld.
    /// </summary>
    public static IReadOnlyList<string> ApiKeysAndSessionTokens { get; } = Array.AsReadOnly(
        ["X-Api-Key", "Api-Key", "X-Goog-Api-Key", "X-Amz-Security-Token", "Ocp-Apim-Subscription-Key"]);

    /// <summary>
    /// Every withheld field: <see cref="Credentials"/> and then <see cref="ApiKeysAndSessionTokens"/>.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(
        [.. Credentials, .. ApiKeysAndSessionTokens]);
}
