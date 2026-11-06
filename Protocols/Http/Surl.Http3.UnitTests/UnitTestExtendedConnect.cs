using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Http3;

/// <summary>An extended CONNECT handler that runs the test's script for each session and records each stream that named no open session.</summary>
internal sealed class UnitTestExtendedConnectHandler(string protocol, Func<Http3ExtendedConnectSession, CancellationToken, Task> serve) : IHttp3ExtendedConnectHandler
{
    /// <summary>WebTransport's signal and stream type (draft-ietf-webtrans-http3-16 section 4.3), the defaults.</summary>
    public const ulong WebTransportSignal = 0x41;

    public const ulong WebTransportStreamType = 0x54;

    public string Protocol => protocol;

    public ulong? BidirectionalStreamSignal { get; init; } = WebTransportSignal;

    public ulong? UnidirectionalStreamType { get; init; } = WebTransportStreamType;

    public TaskCompletionSource<Http3SessionStream> StreamWithoutSession { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ServeSessionAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken) => serve(session, cancellationToken);

    public Task ServeStreamWithoutSessionAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        StreamWithoutSession.TrySetResult(stream);
        return Task.CompletedTask;
    }
}

/// <summary>Starts an <see cref="Http3ConnectionServer"/> with extended CONNECT registrations on an <see cref="Http3TestQuicClient"/>.</summary>
internal static class UnitTestExtendedConnect
{
    /// <summary>The quic_transport_parameters pinned upstream curl sends after initial_source_connection_id (as Surl.Quic.UnitTests' UnitTestClientHelloEdits names them).</summary>
    private const string RecordedParametersAfterSourceConnectionId =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "110800000001" + "00000001";

    /// <summary>The recorded parameters up to initial_max_streams_uni, which tests put back with their own ending.</summary>
    private const string RecordedParametersBeforeStreamsUni =
        "0504800080000604800080000704be8000000404be800000080480040000";

    /// <summary>A ClientHello edit whose parameters also offer DATAGRAM frames of up to 1024 bytes (RFC 9221 section 3, max_datagram_frame_size 0x20).</summary>
    public static Func<byte[], byte[]> ClientOfferingDatagramFrames => ReplaceParameters(RecordedParametersBeforeStreamsUni + "090480040000" + "20024400");

    /// <summary>A ClientHello edit whose initial_max_streams_uni is 3: room for the server's control and QPACK streams and no more.</summary>
    public static Func<byte[], byte[]> ClientAllowingThreeUnidirectionalStreams => ReplaceParameters(RecordedParametersBeforeStreamsUni + "090103");

    /// <summary>A CONNECT for <paramref name="protocol"/> to /echo, with every pseudo-header RFC 9220 section 3 asks for.</summary>
    public static byte[] Connect(Http3TestQuicClient client, string protocol = "webtransport-h3") => client.Headers(
        (":method", "CONNECT"), (":protocol", protocol), (":scheme", "https"), (":authority", "127.0.0.1:18050"), (":path", "/echo"));

    public static async Task<RecordingExchangeLog> StartAsync(
        Http3TestQuicClient client,
        Http3ServerOptions options,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IHttp3RequestStreamHandler? requestHandler = null,
        bool openQpackStreams = true,
        byte[]? clientSettings = null)
    {
        var log = new RecordingExchangeLog();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        var context = new ExchangeContext(
            1,
            new ListenUrl("https", "127.0.0.1", 18050).WithBoundPort(18050),
            new IPEndPoint(IPAddress.Loopback, 18050),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            time,
            CancellationToken.None)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };
        requestHandler ??= new UnitTestRequestStreamHandler((_, _) => throw new AssertFailedException("The request must not reach the request-stream handler."));
        _ = client.Serve(new Http3ConnectionServer(requestHandler, options, context), time, cancellationToken, maxDatagramFrameSize: 1200);
        await client.OpenAsync(cancellationToken, clientSettings, openQpackStreams);
        return log;
    }

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
