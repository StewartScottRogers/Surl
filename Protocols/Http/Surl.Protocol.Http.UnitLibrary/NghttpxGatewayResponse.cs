namespace Surl.Protocol.Http;

/// <summary>The HTTP/2 or HTTP/3 response nghttpx builds from a backend's HTTP/1 reply (ADR-0154 decision 3).</summary>
/// <param name="Status">The <c>:status</c> value.</param>
/// <param name="Fields">The response fields, lower-cased, in the order the frontend writes them.</param>
/// <param name="Body">The body, de-chunked, empty for none.</param>
public sealed record NghttpxGatewayResponse(
    int Status,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    byte[] Body);
