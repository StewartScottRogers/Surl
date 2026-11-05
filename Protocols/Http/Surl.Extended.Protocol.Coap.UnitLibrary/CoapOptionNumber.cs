namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The option numbers the server reads or writes (RFC 7252 section 12.2, RFC 7641 section 7.2,
/// RFC 7959 section 6).
/// </summary>
public static class CoapOptionNumber
{
    /// <summary>If-Match, 1 (RFC 7252 section 5.10.8.1).</summary>
    public const int IfMatch = 1;

    /// <summary>Uri-Host, 3 (section 5.10.1).</summary>
    public const int UriHost = 3;

    /// <summary>ETag, 4 (section 5.10.6).</summary>
    public const int ETag = 4;

    /// <summary>If-None-Match, 5 (section 5.10.8.2).</summary>
    public const int IfNoneMatch = 5;

    /// <summary>Observe, 6 (RFC 7641 section 2).</summary>
    public const int Observe = 6;

    /// <summary>Uri-Port, 7 (section 5.10.1).</summary>
    public const int UriPort = 7;

    /// <summary>Location-Path, 8 (section 5.10.7).</summary>
    public const int LocationPath = 8;

    /// <summary>Uri-Path, 11 (section 5.10.1).</summary>
    public const int UriPath = 11;

    /// <summary>Content-Format, 12 (section 5.10.3).</summary>
    public const int ContentFormat = 12;

    /// <summary>Max-Age, 14 (section 5.10.5).</summary>
    public const int MaxAge = 14;

    /// <summary>Uri-Query, 15 (section 5.10.1).</summary>
    public const int UriQuery = 15;

    /// <summary>Accept, 17 (section 5.10.4).</summary>
    public const int Accept = 17;

    /// <summary>Block2, 23 (RFC 7959 section 2.1).</summary>
    public const int Block2 = 23;

    /// <summary>Block1, 27 (RFC 7959 section 2.1).</summary>
    public const int Block1 = 27;

    /// <summary>Size2, 28 (RFC 7959 section 4).</summary>
    public const int Size2 = 28;

    /// <summary>Proxy-Uri, 35 (section 5.10.2).</summary>
    public const int ProxyUri = 35;

    /// <summary>Proxy-Scheme, 39 (section 5.10.2).</summary>
    public const int ProxyScheme = 39;

    /// <summary>Size1, 60 (section 5.10.9).</summary>
    public const int Size1 = 60;
}
