namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Edits the ClientHello pinned upstream curl sent for HTTP/3 into one a DNS over QUIC server accepts,
/// since upstream curl has no DNS over QUIC. <c>Surl.Composition.UnitTests</c> compiles it too, by link.
/// </summary>
internal static class DnsOverQuicClientHello
{
    /// <summary>
    /// The quic_transport_parameters the recorded build sends after initial_source_connection_id:
    /// bidi_local, bidi_remote, uni, max_data, streams_bidi, streams_uni, then version_information
    /// (the last ten bytes).
    /// </summary>
    private const string RecordedParametersAfterSourceConnectionId =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "110800000001" + "00000001";

    /// <summary>
    /// Replaces the ALPN list's <c>h3</c> with <c>doq</c> (keeping <c>h3-29</c>, which the server does not offer), one byte longer, and keeps the
    /// ClientHello's length by putting the transport parameters' version_information (optional,
    /// RFC 9368 section 3) in a reserved parameter one byte shorter; both extensions' lengths follow.
    /// </summary>
    public static byte[] OfferDoq(byte[] hello)
    {
        byte[] h3 = Convert.FromHexString("0010000B00090268330568332D3239"); // h3, h3-29
        byte[] doq = Convert.FromHexString("0010000C000A03646F710568332D3239"); // doq, h3-29
        var parameters = Convert.FromHexString(RecordedParametersAfterSourceConnectionId);
        byte[] shorter = [.. parameters[..^10], 0x1b, 0x07, .. new byte[7]];

        var parametersAt = hello.AsSpan().IndexOf(parameters);
        Assert.IsGreaterThanOrEqualTo(0, parametersAt, "The ClientHello does not hold the recorded transport parameters.");
        var edited = hello.ToArray();
        var extensionAt = TransportParametersExtensionAt(edited, parametersAt, parameters.Length);
        var extensionLength = (edited[extensionAt + 2] << 8) | edited[extensionAt + 3];
        edited[extensionAt + 2] = (byte)((extensionLength - 1) >> 8);
        edited[extensionAt + 3] = (byte)(extensionLength - 1);
        edited = Splice(edited, parameters, shorter);
        edited = Splice(edited, h3, doq);
        Assert.HasCount(hello.Length, edited);
        return edited;
    }

    // The quic_transport_parameters extension (type 0x0039) whose body holds the parameters at parametersAt.
    private static int TransportParametersExtensionAt(byte[] hello, int parametersAt, int parametersLength)
    {
        for (var index = parametersAt - 4; index >= 0; index--)
        {
            var length = (hello[index + 2] << 8) | hello[index + 3];
            if (hello[index] == 0x00 && hello[index + 1] == 0x39 && index + 4 + length >= parametersAt + parametersLength)
            {
                return index;
            }
        }

        throw new AssertFailedException("The ClientHello has no quic_transport_parameters extension.");
    }

    private static byte[] Splice(byte[] bytes, byte[] from, byte[] to)
    {
        var index = bytes.AsSpan().IndexOf(from);
        Assert.IsGreaterThanOrEqualTo(0, index, $"The ClientHello does not hold {Convert.ToHexString(from)}");
        return [.. bytes[..index], .. to, .. bytes[(index + from.Length)..]];
    }
}
