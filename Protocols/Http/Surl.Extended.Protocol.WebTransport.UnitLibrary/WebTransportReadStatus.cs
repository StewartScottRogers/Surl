namespace Surl.Extended.Protocol.WebTransport;

/// <summary>What reading a capsule or a stream header from the bytes received so far found.</summary>
public enum WebTransportReadStatus
{
    /// <summary>The bytes hold a whole, well-formed capsule or stream header.</summary>
    Complete,

    /// <summary>The bytes end inside it: read again once more arrive.</summary>
    Incomplete,

    /// <summary>It is whole but breaks its layout; more bytes will not help.</summary>
    Malformed,
}
