using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The WebTransport server on one QUIC connection (ADR-0117 decision 0): what it offers, and its arguments.</summary>
[TestClass]
public sealed class WebTransportApplicationProtocolTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeAsync_Connection_SendsSettingsWithExtendedConnectDatagramsWtEnabledAndTheInitialFlowControlLimits()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        await client.UntilAsync(() => client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data.Length > 1, Token);

        var settings = Http3TestQuicClient.ReadFrames(client.StreamSent(Http3TestQuicClient.ServerControlStreamId).Data.AsSpan(1))[0];
        Assert.AreEqual(Http3FrameType.Settings, settings.Type);
        Assert.EndsWith(
            "0801" + "3301" + "AC7CF00001" + "6B644064" + "6B654064" + "6B6180100000",
            Convert.ToHexString(settings.Payload),
            "ENABLE_CONNECT_PROTOCOL, H3_DATAGRAM, SETTINGS_WT_ENABLED (WTH3 3.1), then WT_INITIAL_MAX_STREAMS_UNI and _BIDI = 100 and WT_INITIAL_MAX_DATA = 1048576 (WTH3 5.5, ADR-0117 decision 4).");
    }

    [TestMethod]
    public async Task ConstructorAndServeAsync_NullArguments_Throw()
    {
        var context = new ExchangeContext(1, new ListenUrl("webtransport", "127.0.0.1", 1), new IPEndPoint(0, 1), new IPEndPoint(0, 2), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);

        var policy = new AnonymousAuthenticationPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportApplicationProtocol(null!, policy, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportApplicationProtocol("/", null!, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportApplicationProtocol("/", policy, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new WebTransportApplicationProtocol("/", policy, context).ServeAsync(null!, Token));
    }
}
