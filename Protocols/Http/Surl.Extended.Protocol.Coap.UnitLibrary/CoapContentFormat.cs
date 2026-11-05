namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The Content-Format of a representation (RFC 7252 sections 5.10.3 and 12.3; ADR-0097 decision
/// 1): chosen from a file name's extension, compared ordinally ignoring case.
/// </summary>
internal static class CoapContentFormat
{
    /// <summary><c>text/plain;charset=utf-8</c>, 0.</summary>
    public const uint TextPlain = 0;

    /// <summary><c>application/link-format</c>, 40: <c>/.well-known/core</c>.</summary>
    public const uint LinkFormat = 40;

    /// <summary><c>application/octet-stream</c>, 42: every file with no other format.</summary>
    public const uint OctetStream = 42;

    /// <summary>
    /// The format of a file named <paramref name="fileName"/>: <c>.txt</c> 0, <c>.xml</c> 41,
    /// <c>.exi</c> 47, <c>.json</c> 50, <c>.cbor</c> 60, anything else 42.
    /// </summary>
    /// <param name="fileName">The file's name or path.</param>
    /// <returns>The Content-Format number.</returns>
    public static uint ForFileName(string fileName) => Path.GetExtension(fileName).ToUpperInvariant() switch
    {
        ".TXT" => TextPlain,
        ".XML" => 41,
        ".EXI" => 47,
        ".JSON" => 50,
        ".CBOR" => 60,
        _ => OctetStream,
    };
}
