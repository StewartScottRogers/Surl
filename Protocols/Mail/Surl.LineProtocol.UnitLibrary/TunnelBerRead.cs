namespace Surl.LineProtocol;

/// <summary>What <see cref="TunnelBerHeader.TryRead"/> found.</summary>
internal enum TunnelBerRead
{
    /// <summary>The header is whole.</summary>
    Read,

    /// <summary>More bytes are needed to read the header.</summary>
    CutShort,

    /// <summary>The length is indefinite or takes more than four octets.</summary>
    Unreadable,
}
