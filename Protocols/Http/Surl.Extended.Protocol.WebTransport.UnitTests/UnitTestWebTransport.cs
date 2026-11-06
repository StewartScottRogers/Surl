using System.Net;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Starts a <see cref="WebTransportApplicationProtocol"/> on an <see cref="Http3TestQuicClient"/>, and
/// writes the client's side of a WebTransport session from draft-ietf-webtrans-http3-16 and RFC 9297:
/// the CONNECT, stream headers (signal 0x41 or type 0x54, then the session ID) and capsules.
/// </summary>
internal static class UnitTestWebTransport
{
    /// <summary>The bytes of WT_DRAIN_SESSION (type 0x78ae, length 0) as the server writes it.</summary>
    public const string DrainSessionCapsuleHex = "800078AE" + "00";

    /// <summary>The header of a client bidirectional stream of session 0: signal 0x41, then session ID 0.</summary>
    public const string BidirectionalHeaderHex = "4041" + "00";

    /// <summary>The header of a unidirectional stream of session 0: type 0x54, then session ID 0.</summary>
    public const string UnidirectionalHeaderHex = "4054" + "00";

    /// <summary>The server's first unidirectional stream after its control and QPACK streams.</summary>
    public const ulong ServerAnswerStreamId = 15;

    /// <summary>The quic_transport_parameters pinned upstream curl sends after initial_source_connection_id (as Surl.Http3.UnitTests' UnitTestExtendedConnect names them).</summary>
    private const string RecordedParametersAfterSourceConnectionId =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "110800000001" + "00000001";

    /// <summary>The recorded parameters up to initial_max_streams_uni, which tests put back with their own ending.</summary>
    private const string RecordedParametersBeforeStreamsUni =
        "0504800080000604800080000704be8000000404be800000080480040000";

    /// <summary>The client's SETTINGS WTH3 3.1 requires: SETTINGS_H3_DATAGRAM = 1 and SETTINGS_WT_ENABLED (0x2c7cf000) = 1.</summary>
    public static byte[] WebTransportSettings => Settings("3301" + "AC7CF00001");

    /// <summary>A ClientHello edit whose parameters also offer DATAGRAM frames of up to 1024 bytes (RFC 9221 section 3, max_datagram_frame_size 0x20).</summary>
    public static Func<byte[], byte[]> ClientOfferingDatagramFrames => ReplaceParameters(RecordedParametersBeforeStreamsUni + "090480040000" + "20024400");

    /// <summary>A ClientHello edit whose initial_max_streams_uni is 3: room for the server's control and QPACK streams and no more.</summary>
    public static Func<byte[], byte[]> ClientAllowingThreeUnidirectionalStreams => ReplaceParameters(RecordedParametersBeforeStreamsUni + "090103");

    public static byte[] Settings(string payloadHex) => Http3TestQuicClient.Frame(Http3FrameType.Settings, Convert.FromHexString(payloadHex));

    /// <summary>A DATA frame carrying <paramref name="hex"/>: how capsules travel on the CONNECT stream.</summary>
    public static byte[] Data(string hex) => Http3TestQuicClient.Frame(Http3FrameType.Data, Convert.FromHexString(hex));

    /// <summary>An extended CONNECT for webtransport-h3 to <paramref name="path"/>, with any extra fields.</summary>
    public static byte[] Connect(Http3TestQuicClient client, string path = "/echo", params (string Name, string Value)[] extra) => client.Headers(
        [(":method", "CONNECT"), (":protocol", "webtransport-h3"), (":scheme", "https"), (":authority", "127.0.0.1:18050"), (":path", path), .. extra]);

    /// <summary>
    /// Starts the server for <paramref name="acceptedPath"/> and opens the client's control stream with
    /// <paramref name="clientSettings"/> (the WebTransport ones by default), or none when <paramref name="openControlStream"/> is false.
    /// </summary>
    public static async Task<(RecordingExchangeLog Log, ManualTimeProvider Time)> StartAsync(
        Http3TestQuicClient client,
        CancellationToken cancellationToken,
        string acceptedPath = "/echo",
        byte[]? clientSettings = null,
        bool openQpackStreams = true,
        bool openControlStream = true,
        ExchangeLimits? limits = null,
        CancellationToken exchangeToken = default)
    {
        var log = new RecordingExchangeLog();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var context = new ExchangeContext(
            1,
            new ListenUrl("webtransport", "127.0.0.1", 18050).WithBoundPort(18050),
            new IPEndPoint(IPAddress.Loopback, 18050),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            time,
            exchangeToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };
        _ = client.Serve(new WebTransportApplicationProtocol(acceptedPath, context), time, cancellationToken, WebTransportApplicationProtocol.MaxDatagramFrameSize);
        if (openControlStream)
        {
            await client.OpenAsync(cancellationToken, clientSettings ?? WebTransportSettings, openQpackStreams);
        }
        else
        {
            await client.CompleteHandshakeAsync(cancellationToken);
        }

        return (log, time);
    }

    /// <summary>Sends a CONNECT on stream 0 and waits for the server's answer.</summary>
    public static async Task<Http3TestResponse> ConnectAsync(Http3TestQuicClient client, CancellationToken cancellationToken, string path = "/echo", params (string Name, string Value)[] extra)
    {
        client.Send(0, Connect(client, path, extra));
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0 || client.ResetCode(0) is not null, cancellationToken);
        return client.Response(0);
    }

    /// <summary>Opens session 0 and checks it was accepted.</summary>
    public static async Task OpenSessionAsync(Http3TestQuicClient client, CancellationToken cancellationToken)
    {
        var response = await ConnectAsync(client, cancellationToken);
        Assert.AreEqual("200", response.Status);
    }

    /// <summary>The bytes the server sent on <paramref name="streamId"/>, as hex.</summary>
    public static string SentHex(Http3TestQuicClient client, ulong streamId) => Convert.ToHexString(client.StreamSent(streamId).Data);

    /// <summary>Sends a QUIC DATAGRAM frame carrying <paramref name="hex"/>.</summary>
    public static void SendDatagram(Http3TestQuicClient client, string hex) =>
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString(hex))));

    /// <summary>Sends STOP_SENDING with <paramref name="code"/> on <paramref name="streamId"/>.</summary>
    public static void StopSending(Http3TestQuicClient client, ulong streamId, ulong code) =>
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicStopSendingFrame(streamId, code)));

    /// <summary>Advances the clock a second at a time until <paramref name="condition"/> holds.</summary>
    public static Task AdvanceUntilAsync(Http3TestQuicClient client, ManualTimeProvider time, Func<bool> condition, CancellationToken cancellationToken) =>
        client.UntilAsync(
            () =>
            {
                if (condition())
                {
                    return true;
                }

                time.Advance(TimeSpan.FromSeconds(1));
                return false;
            },
            cancellationToken);

    private static Func<byte[], byte[]> ReplaceParameters(string toHex)
    {
        var fillerValueLength = (RecordedParametersAfterSourceConnectionId.Length / 2) - (toHex.Length / 2) - 2;
        var from = Convert.FromHexString(RecordedParametersAfterSourceConnectionId);
        var to = Convert.FromHexString(toHex + "1b" + fillerValueLength.ToString("x2", System.Globalization.CultureInfo.InvariantCulture) + new string('0', fillerValueLength * 2));
        return hello =>
        {
            var index = hello.AsSpan().IndexOf(from);
            Assert.IsGreaterThanOrEqualTo(0, index, "The ClientHello does not hold the recorded parameters.");
            var copy = hello.ToArray();
            to.CopyTo(copy, index);
            return copy;
        };
    }
}
