using System.Globalization;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The transport a <c>SETUP</c> is answered with, chosen from its <c>Transport</c> field as
/// ADR-0074 decision 5 says: the first alternative that is <c>RTP/AVP/TCP</c> and not
/// <c>multicast</c>, interleaved on an even channel and the one after it.
/// </summary>
/// <param name="RtpChannel">The interleaved channel RTP packets are sent on; RTCP's is the next one.</param>
/// <param name="Records">Whether the alternative asks for <c>mode=record</c>.</param>
internal sealed record RtspTransport(byte RtpChannel, bool Records)
{
    private const string InterleavedTcpProfile = "RTP/AVP/TCP";

    private const string InterleavedParameter = "interleaved=";

    private const string ModeParameter = "mode=";

    /// <summary>
    /// The interleaved channel RTCP packets are sent on: the one after <see cref="RtpChannel"/>.
    /// </summary>
    public byte RtcpChannel => (byte)(RtpChannel + 1);

    /// <summary>
    /// The <c>Transport</c> field of the <c>SETUP</c> answer to play, with the SSRC packets are
    /// sent with.
    /// </summary>
    /// <param name="ssrc">The session's synchronization source.</param>
    /// <returns>The field's value.</returns>
    public string Describe(uint ssrc) => string.Create(
        CultureInfo.InvariantCulture,
        $"{InterleavedTcpProfile};unicast;interleaved={RtpChannel}-{RtcpChannel};ssrc={ssrc:X8}");

    /// <summary>
    /// Chooses the transport from a <c>SETUP</c>'s <c>Transport</c> field values.
    /// </summary>
    /// <param name="transportValues">Every <c>Transport</c> field value, in order; each a comma-separated list of alternatives.</param>
    /// <param name="whyUnsupported">Why none was taken, for the <c>461</c>'s note; <see langword="null"/> when one was.</param>
    /// <returns>The transport, or <see langword="null"/> when the answer is <c>461 Unsupported Transport</c>.</returns>
    public static RtspTransport? Choose(IEnumerable<string> transportValues, out string? whyUnsupported)
    {
        var alternative = transportValues
            .SelectMany(value => value.Split(','))
            .Select(ParametersOf)
            .FirstOrDefault(IsUnicastInterleavedTcp);
        if (alternative is null)
        {
            whyUnsupported = "no RTP/AVP/TCP alternative";

            return null;
        }

        return FromParameters(alternative, out whyUnsupported);
    }

    private static RtspTransport? FromParameters(string[] parameters, out string? whyUnsupported)
    {
        var channel = InterleavedRtpChannel(parameters);
        if (channel < 0)
        {
            whyUnsupported = "the interleaved channels are not an even channel and the one after it, both below 256";

            return null;
        }

        var records = ModeRecords(parameters);
        whyUnsupported = records is null ? "the mode is neither play nor record" : null;

        return records is null ? null : new RtspTransport((byte)channel, records.Value);
    }

    private static string[] ParametersOf(string alternative) =>
        alternative.Split(';').Select(parameter => parameter.Trim()).ToArray();

    private static bool IsUnicastInterleavedTcp(string[] parameters) =>
        parameters[0].Equals(InterleavedTcpProfile, StringComparison.OrdinalIgnoreCase)
        && !parameters.Contains("multicast", StringComparer.OrdinalIgnoreCase);

    // interleaved=<n>-<m>: n even, m = n + 1, both below 256; 0-1 when absent; -1 otherwise.
    private static int InterleavedRtpChannel(string[] parameters)
    {
        var channels = (ValueOf(parameters, InterleavedParameter) ?? "0-1").Split('-');
        if (channels.Length != 2)
        {
            return -1;
        }

        var rtp = Channel(channels[0]);
        var rtcp = Channel(channels[1]);

        return rtp % 2 == 0 && rtcp == rtp + 1 ? rtp : -1;
    }

    // A channel number below 256; -1 for anything else.
    private static int Channel(string text) =>
        byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var channel) ? channel : -1;

    // mode=play or mode=record, quoted or not, any case; absent is play; null for any other.
    private static bool? ModeRecords(string[] parameters) =>
        ValueOf(parameters, ModeParameter)?.Trim('"').ToUpperInvariant() switch
        {
            null or "PLAY" => false,
            "RECORD" => true,
            _ => null,
        };

    private static string? ValueOf(string[] parameters, string prefix) =>
        parameters
            .Where(parameter => parameter.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(parameter => parameter[prefix.Length..])
            .FirstOrDefault();
}
