namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The reply codes of the AMQP 0-9-1 XML protocol definition's constants (ADR-0112 decision 7).
/// Codes from 500 up are <c>hard-error</c>s, connection exceptions; the 300s and 400s are
/// <c>soft-error</c>s, channel exceptions, except <see cref="AccessRefused"/> for a refused login.
/// </summary>
public enum Amqp0ReplyCode : ushort
{
    /// <summary>200 <c>reply-success</c>: the close was asked for, not forced.</summary>
    ReplySuccess = 200,

    /// <summary>311 <c>content-too-large</c>: a message body above the limit.</summary>
    ContentTooLarge = 311,

    /// <summary>312 <c>no-route</c>: a mandatory message no queue took.</summary>
    NoRoute = 312,

    /// <summary>313 <c>no-consumers</c>: an immediate message no consumer took.</summary>
    NoConsumers = 313,

    /// <summary>320 <c>connection-forced</c>: the broker closed the connection itself.</summary>
    ConnectionForced = 320,

    /// <summary>402 <c>invalid-path</c>: a virtual host path the broker does not accept.</summary>
    InvalidPath = 402,

    /// <summary>403 <c>access-refused</c>: the caller may not do this.</summary>
    AccessRefused = 403,

    /// <summary>404 <c>not-found</c>: the exchange or queue does not exist.</summary>
    NotFound = 404,

    /// <summary>405 <c>resource-locked</c>: another connection holds the queue exclusively.</summary>
    ResourceLocked = 405,

    /// <summary>406 <c>precondition-failed</c>: a field or state the method needs is wrong.</summary>
    PreconditionFailed = 406,

    /// <summary>501 <c>frame-error</c>: a frame that does not decode (section 4.2.3).</summary>
    FrameError = 501,

    /// <summary>502 <c>syntax-error</c>: a method whose arguments do not decode.</summary>
    SyntaxError = 502,

    /// <summary>503 <c>command-invalid</c>: a method the client may not send.</summary>
    CommandInvalid = 503,

    /// <summary>504 <c>channel-error</c>: a channel that is not open, or above the limit.</summary>
    ChannelError = 504,

    /// <summary>505 <c>unexpected-frame</c>: a frame out of the content sequence.</summary>
    UnexpectedFrame = 505,

    /// <summary>506 <c>resource-error</c>: the broker ran out of a bounded resource.</summary>
    ResourceError = 506,

    /// <summary>530 <c>not-allowed</c>: a request the broker's rules forbid.</summary>
    NotAllowed = 530,

    /// <summary>540 <c>not-implemented</c>: a class or method the broker does not serve.</summary>
    NotImplemented = 540,

    /// <summary>541 <c>internal-error</c>: a fault inside the broker.</summary>
    InternalError = 541,
}
