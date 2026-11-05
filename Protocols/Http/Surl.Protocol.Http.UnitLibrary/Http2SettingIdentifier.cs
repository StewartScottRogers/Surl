namespace Surl.Protocol.Http;

/// <summary>
/// The setting identifiers RFC 9113 section 6.5.2 defines. A <c>SETTINGS</c> entry with any other
/// identifier is kept as its number and ignored by whoever applies the settings.
/// </summary>
internal enum Http2SettingIdentifier : ushort
{
    /// <summary><c>SETTINGS_HEADER_TABLE_SIZE</c>: the largest HPACK dynamic table the sender's decoder allows.</summary>
    HeaderTableSize = 0x1,

    /// <summary><c>SETTINGS_ENABLE_PUSH</c>: 0 or 1, whether the server may push.</summary>
    EnablePush = 0x2,

    /// <summary><c>SETTINGS_MAX_CONCURRENT_STREAMS</c>: how many streams the sender allows the peer to open.</summary>
    MaxConcurrentStreams = 0x3,

    /// <summary><c>SETTINGS_INITIAL_WINDOW_SIZE</c>: each stream's initial flow-control window, at most 2^31-1.</summary>
    InitialWindowSize = 0x4,

    /// <summary><c>SETTINGS_MAX_FRAME_SIZE</c>: the largest frame payload the sender accepts, 16384 to 16777215.</summary>
    MaxFrameSize = 0x5,

    /// <summary><c>SETTINGS_MAX_HEADER_LIST_SIZE</c>: advisory, the largest field section the sender accepts.</summary>
    MaxHeaderListSize = 0x6,
}
