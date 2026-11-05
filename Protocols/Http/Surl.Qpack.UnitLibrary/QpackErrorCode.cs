namespace Surl.Qpack;

/// <summary>The HTTP/3 error codes RFC 9204 section 6 defines for QPACK failures.</summary>
public enum QpackErrorCode
{
    /// <summary>The decoder could not interpret an encoded field section (0x0200).</summary>
    DecompressionFailed = 0x0200,

    /// <summary>The decoder could not interpret an instruction on the encoder stream (0x0201).</summary>
    EncoderStreamError = 0x0201,

    /// <summary>The encoder could not interpret an instruction on the decoder stream (0x0202).</summary>
    DecoderStreamError = 0x0202,
}
