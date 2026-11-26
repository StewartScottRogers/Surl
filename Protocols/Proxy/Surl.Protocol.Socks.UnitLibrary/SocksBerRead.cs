namespace Surl.Protocol.Socks;

/// <summary>What <see cref="SocksBerHeader.TryRead"/> found.</summary>
internal enum SocksBerRead
{
    /// <summary>The header is whole.</summary>
    Read,

    /// <summary>More bytes are needed to read the header.</summary>
    CutShort,

    /// <summary>The length is indefinite or takes more than four octets.</summary>
    Unreadable,
}
