namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// What the server answers a request with, before the message layer gives it a type and a
/// Message ID: the response code, its options and its payload (RFC 7252 section 5.2).
/// </summary>
/// <param name="Code">The response code.</param>
/// <param name="Options">The response's options.</param>
/// <param name="Payload">The payload; empty when there is none.</param>
internal sealed record CoapResponse(CoapCode Code, IReadOnlyList<CoapOption> Options, ReadOnlyMemory<byte> Payload)
{
    /// <summary>A response with <paramref name="code"/>, no option and no payload.</summary>
    /// <param name="code">The response code.</param>
    /// <returns>The response.</returns>
    public static CoapResponse Bare(CoapCode code) => new(code, [], ReadOnlyMemory<byte>.Empty);

    /// <summary>A response with <paramref name="code"/> and options, and no payload.</summary>
    /// <param name="code">The response code.</param>
    /// <param name="options">The options.</param>
    /// <returns>The response.</returns>
    public static CoapResponse WithOptions(CoapCode code, params CoapOption[] options) => new(code, options, ReadOnlyMemory<byte>.Empty);
}
