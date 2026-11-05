namespace Surl.Quic;

/// <summary>Same-length edits of a recorded ClientHello, so a test can put a fault in what curl sent.</summary>
internal static class UnitTestClientHelloEdits
{
    /// <summary>
    /// The quic_transport_parameters every recorded build sends after initial_source_connection_id
    /// (ADR-0082's Measured table): bidi_local, bidi_remote, uni, max_data, streams_bidi,
    /// streams_uni and version_information.
    /// </summary>
    public const string RecordedParametersAfterSourceConnectionId =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "110800000001" + "00000001";

    /// <summary>Returns a copy of <paramref name="hello"/> with the one occurrence of <paramref name="fromHex"/> replaced by <paramref name="toHex"/>, as long.</summary>
    public static Func<byte[], byte[]> Replace(string fromHex, string toHex) => hello =>
    {
        var from = Convert.FromHexString(fromHex);
        var to = Convert.FromHexString(toHex);
        Assert.HasCount(from.Length, to);
        var index = hello.AsSpan().IndexOf(from);
        Assert.IsGreaterThanOrEqualTo(0, index, $"The ClientHello does not hold {fromHex}.");
        var copy = hello.ToArray();
        to.CopyTo(copy, index);
        return copy;
    };

    /// <summary>Replaces the recorded parameters after initial_source_connection_id with <paramref name="toHex"/>, padded with a reserved parameter (0x1b) to the same length.</summary>
    public static Func<byte[], byte[]> ReplaceParameters(string toHex)
    {
        var fillerValueLength = (RecordedParametersAfterSourceConnectionId.Length / 2) - (toHex.Length / 2) - 2;
        return Replace(RecordedParametersAfterSourceConnectionId, toHex + "1b" + fillerValueLength.ToString("x2") + new string('0', fillerValueLength * 2));
    }
}
