namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The error conditions of AMQP 1.0 Part 2 sections 2.8.15 to 2.8.18 and the transaction errors of
/// Part 4 section 4.5.8 the container sends, as the symbols an
/// <see cref="Amqp1Error"/> carries.
/// </summary>
public static class Amqp1ErrorCondition
{
    /// <summary><c>amqp:internal-error</c>.</summary>
    public static readonly Amqp1Symbol InternalError = new("amqp:internal-error");

    /// <summary><c>amqp:not-found</c>.</summary>
    public static readonly Amqp1Symbol NotFound = new("amqp:not-found");

    /// <summary><c>amqp:unauthorized-access</c>.</summary>
    public static readonly Amqp1Symbol UnauthorizedAccess = new("amqp:unauthorized-access");

    /// <summary><c>amqp:decode-error</c>: data could not be decoded.</summary>
    public static readonly Amqp1Symbol DecodeError = new("amqp:decode-error");

    /// <summary><c>amqp:resource-limit-exceeded</c>.</summary>
    public static readonly Amqp1Symbol ResourceLimitExceeded = new("amqp:resource-limit-exceeded");

    /// <summary><c>amqp:not-allowed</c>.</summary>
    public static readonly Amqp1Symbol NotAllowed = new("amqp:not-allowed");

    /// <summary><c>amqp:invalid-field</c>.</summary>
    public static readonly Amqp1Symbol InvalidField = new("amqp:invalid-field");

    /// <summary><c>amqp:not-implemented</c>.</summary>
    public static readonly Amqp1Symbol NotImplemented = new("amqp:not-implemented");

    /// <summary><c>amqp:resource-locked</c>.</summary>
    public static readonly Amqp1Symbol ResourceLocked = new("amqp:resource-locked");

    /// <summary><c>amqp:precondition-failed</c>.</summary>
    public static readonly Amqp1Symbol PreconditionFailed = new("amqp:precondition-failed");

    /// <summary><c>amqp:resource-deleted</c>.</summary>
    public static readonly Amqp1Symbol ResourceDeleted = new("amqp:resource-deleted");

    /// <summary><c>amqp:illegal-state</c>.</summary>
    public static readonly Amqp1Symbol IllegalState = new("amqp:illegal-state");

    /// <summary><c>amqp:frame-size-too-small</c>.</summary>
    public static readonly Amqp1Symbol FrameSizeTooSmall = new("amqp:frame-size-too-small");

    /// <summary><c>amqp:connection:forced</c>.</summary>
    public static readonly Amqp1Symbol ConnectionForced = new("amqp:connection:forced");

    /// <summary><c>amqp:connection:framing-error</c>: a frame that is not valid.</summary>
    public static readonly Amqp1Symbol FramingError = new("amqp:connection:framing-error");

    /// <summary><c>amqp:connection:redirect</c>.</summary>
    public static readonly Amqp1Symbol ConnectionRedirect = new("amqp:connection:redirect");

    /// <summary><c>amqp:session:window-violation</c>.</summary>
    public static readonly Amqp1Symbol WindowViolation = new("amqp:session:window-violation");

    /// <summary><c>amqp:session:errant-link</c>.</summary>
    public static readonly Amqp1Symbol ErrantLink = new("amqp:session:errant-link");

    /// <summary><c>amqp:session:handle-in-use</c>.</summary>
    public static readonly Amqp1Symbol HandleInUse = new("amqp:session:handle-in-use");

    /// <summary><c>amqp:session:unattached-handle</c>.</summary>
    public static readonly Amqp1Symbol UnattachedHandle = new("amqp:session:unattached-handle");

    /// <summary><c>amqp:link:detach-forced</c>.</summary>
    public static readonly Amqp1Symbol DetachForced = new("amqp:link:detach-forced");

    /// <summary><c>amqp:link:transfer-limit-exceeded</c>.</summary>
    public static readonly Amqp1Symbol TransferLimitExceeded = new("amqp:link:transfer-limit-exceeded");

    /// <summary><c>amqp:link:message-size-exceeded</c>.</summary>
    public static readonly Amqp1Symbol MessageSizeExceeded = new("amqp:link:message-size-exceeded");

    /// <summary><c>amqp:link:redirect</c>.</summary>
    public static readonly Amqp1Symbol LinkRedirect = new("amqp:link:redirect");

    /// <summary><c>amqp:link:stolen</c>.</summary>
    public static readonly Amqp1Symbol Stolen = new("amqp:link:stolen");

    /// <summary><c>amqp:transaction:unknown-id</c> (Part 4 section 4.5.8): a transaction id that names no open transaction.</summary>
    public static readonly Amqp1Symbol TransactionUnknownId = new("amqp:transaction:unknown-id");

    /// <summary><c>amqp:transaction:rollback</c> (Part 4 section 4.5.8): the transaction was rolled back instead of committed.</summary>
    public static readonly Amqp1Symbol TransactionRollback = new("amqp:transaction:rollback");
}
