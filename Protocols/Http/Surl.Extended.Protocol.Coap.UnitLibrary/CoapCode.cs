namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The 8-bit Code of a CoAP message: a 3-bit class and a 5-bit detail, written <c>c.dd</c>
/// (RFC 7252 section 3). Class 0 is a request (0.00 the Empty message), 2, 4 and 5 are
/// responses, and every other class is reserved.
/// </summary>
/// <param name="Value">The code byte as it is on the wire.</param>
public readonly record struct CoapCode(byte Value)
{
    /// <summary>0.00, the Empty message (section 4.1).</summary>
    public static CoapCode Empty { get; } = new(0x00);

    /// <summary>0.01 GET (section 5.8.1).</summary>
    public static CoapCode Get { get; } = new(0x01);

    /// <summary>0.02 POST (section 5.8.2).</summary>
    public static CoapCode Post { get; } = new(0x02);

    /// <summary>0.03 PUT (section 5.8.3).</summary>
    public static CoapCode Put { get; } = new(0x03);

    /// <summary>0.04 DELETE (section 5.8.4).</summary>
    public static CoapCode Delete { get; } = new(0x04);

    /// <summary>2.01 Created (section 5.9.1.1).</summary>
    public static CoapCode Created { get; } = new(0x41);

    /// <summary>2.02 Deleted (section 5.9.1.2).</summary>
    public static CoapCode Deleted { get; } = new(0x42);

    /// <summary>2.03 Valid (section 5.9.1.3).</summary>
    public static CoapCode Valid { get; } = new(0x43);

    /// <summary>2.04 Changed (section 5.9.1.4).</summary>
    public static CoapCode Changed { get; } = new(0x44);

    /// <summary>2.05 Content (section 5.9.1.4).</summary>
    public static CoapCode Content { get; } = new(0x45);

    /// <summary>4.00 Bad Request (section 5.9.2.1).</summary>
    public static CoapCode BadRequest { get; } = new(0x80);

    /// <summary>4.01 Unauthorized (section 5.9.2.2).</summary>
    public static CoapCode Unauthorized { get; } = new(0x81);

    /// <summary>4.02 Bad Option (section 5.9.2.3).</summary>
    public static CoapCode BadOption { get; } = new(0x82);

    /// <summary>4.04 Not Found (section 5.9.2.5).</summary>
    public static CoapCode NotFound { get; } = new(0x84);

    /// <summary>4.05 Method Not Allowed (section 5.9.2.6).</summary>
    public static CoapCode MethodNotAllowed { get; } = new(0x85);

    /// <summary>4.06 Not Acceptable (section 5.9.2.7).</summary>
    public static CoapCode NotAcceptable { get; } = new(0x86);

    /// <summary>4.12 Precondition Failed (section 5.9.2.8).</summary>
    public static CoapCode PreconditionFailed { get; } = new(0x8C);

    /// <summary>4.13 Request Entity Too Large (section 5.9.2.9).</summary>
    public static CoapCode RequestEntityTooLarge { get; } = new(0x8D);

    /// <summary>5.00 Internal Server Error (section 5.9.3.1).</summary>
    public static CoapCode InternalServerError { get; } = new(0xA0);

    /// <summary>5.05 Proxying Not Supported (section 5.9.3.6).</summary>
    public static CoapCode ProxyingNotSupported { get; } = new(0xA5);

    /// <summary>The class, 0 to 7: the code's top three bits.</summary>
    public int Class => Value >> 5;

    /// <summary>The detail, 0 to 31: the code's low five bits.</summary>
    public int Detail => Value & 0x1F;

    /// <summary>Whether this is 0.00, the Empty message.</summary>
    public bool IsEmpty => Value == 0;

    /// <summary>Whether this is a method code, 0.01 to 0.31.</summary>
    public bool IsRequest => Class == 0 && Detail != 0;

    /// <summary>
    /// Makes the code <paramref name="codeClass"/>.<paramref name="detail"/>.
    /// </summary>
    /// <param name="codeClass">The class, 0 to 7.</param>
    /// <param name="detail">The detail, 0 to 31.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either part is outside its range.</exception>
    public static CoapCode From(int codeClass, int detail)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(codeClass);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(codeClass, 7);
        ArgumentOutOfRangeException.ThrowIfNegative(detail);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(detail, 31);

        return new CoapCode((byte)((codeClass << 5) | detail));
    }

    /// <summary>The code as RFC 7252 writes it, <c>c.dd</c>, such as <c>4.04</c>.</summary>
    /// <returns>The code's text.</returns>
    public override string ToString() => $"{Class}.{Detail:D2}";
}
