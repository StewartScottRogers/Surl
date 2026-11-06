namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The flow-control limits the server offers every session (ADR-0117 decisions 0 and 4; WTH3 5.5):
/// the stream credit each way and the data window, sent as the three <c>SETTINGS_WT_INITIAL_*</c>
/// and renewed as the session uses them.
/// </summary>
/// <param name="InitialMaxStreams">The client streams of each direction a session may open before the server raises the limit.</param>
/// <param name="DataWindow">The bytes of stream payload a session may send beyond what the server has read.</param>
internal sealed record WebTransportFlowControlSettings(ulong InitialMaxStreams, ulong DataWindow)
{
    /// <summary><c>SETTINGS_WT_INITIAL_MAX_DATA</c> (WTH3 9.2).</summary>
    public const ulong InitialMaxDataSetting = 0x2b61;

    /// <summary><c>SETTINGS_WT_INITIAL_MAX_STREAMS_UNI</c> (WTH3 9.2).</summary>
    public const ulong InitialMaxStreamsUnidirectionalSetting = 0x2b64;

    /// <summary><c>SETTINGS_WT_INITIAL_MAX_STREAMS_BIDI</c> (WTH3 9.2).</summary>
    public const ulong InitialMaxStreamsBidirectionalSetting = 0x2b65;

    /// <summary>ADR-0117 decision 4's limits: 100 streams each way and a 1048576-byte window.</summary>
    public static WebTransportFlowControlSettings Default { get; } = new(100, 1048576);

    /// <summary>The three <c>SETTINGS_WT_INITIAL_*</c> the server sends, all non-zero, so flow control is on whenever the client offers it too.</summary>
    /// <returns>The settings, by identifier.</returns>
    public KeyValuePair<ulong, ulong>[] ToHttp3Settings() =>
    [
        new(InitialMaxStreamsUnidirectionalSetting, InitialMaxStreams),
        new(InitialMaxStreamsBidirectionalSetting, InitialMaxStreams),
        new(InitialMaxDataSetting, DataWindow),
    ];

    /// <summary>
    /// Whether flow control is on for a connection whose client sent <paramref name="clientSettings"/>:
    /// WTH3 5.1 turns it on when the client sends any <c>SETTINGS_WT_INITIAL_*</c> non-zero, as the server always does.
    /// </summary>
    /// <param name="clientSettings">The client's <c>SETTINGS</c>.</param>
    /// <returns><see langword="true"/> when flow control is on.</returns>
    public static bool IsOnFor(IReadOnlyDictionary<ulong, ulong> clientSettings) =>
        clientSettings.GetValueOrDefault(InitialMaxDataSetting) != 0
        || clientSettings.GetValueOrDefault(InitialMaxStreamsUnidirectionalSetting) != 0
        || clientSettings.GetValueOrDefault(InitialMaxStreamsBidirectionalSetting) != 0;
}
