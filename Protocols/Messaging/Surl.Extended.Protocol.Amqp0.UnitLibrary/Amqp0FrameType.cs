namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The frame types of AMQP 0-9-1 section 4.2.3 and the XML's <c>frame-*</c> constants.
/// </summary>
public enum Amqp0FrameType : byte
{
    /// <summary>1 <c>frame-method</c>: a class ID, a method ID and the method's arguments.</summary>
    Method = 1,

    /// <summary>2 <c>frame-header</c>: a content header (section 4.2.6).</summary>
    ContentHeader = 2,

    /// <summary>3 <c>frame-body</c>: a piece of a content body (section 4.2.6).</summary>
    ContentBody = 3,

    /// <summary>8 <c>frame-heartbeat</c>: an empty frame on channel 0 only (section 4.2.7).</summary>
    Heartbeat = 8,
}
