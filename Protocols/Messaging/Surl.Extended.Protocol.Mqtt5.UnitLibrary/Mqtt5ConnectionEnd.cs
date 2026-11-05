namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// What answering one packet left the connection in.
/// </summary>
internal enum Mqtt5ConnectionEnd
{
    /// <summary>The connection stays open for the next packet.</summary>
    StaysOpen,

    /// <summary>The client ended it with <c>DISCONNECT</c> <c>0x00</c>: its Will is discarded.</summary>
    ClosedNormally,

    /// <summary>It ended any other way: its Will is published (section 3.1.2.5).</summary>
    ClosedAbnormally,
}
