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

    /// <summary>2.05 Content (section 5.9.1.4).</summary>
    public static CoapCode Content { get; } = new(0x45);

    /// <summary>4.04 Not Found (section 5.9.2.5).</summary>
    public static CoapCode NotFound { get; } = new(0x84);

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
