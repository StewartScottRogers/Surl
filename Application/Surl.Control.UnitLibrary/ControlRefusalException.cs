namespace Surl.Control;

/// <summary>
/// Thrown by an <see cref="IControlTarget"/> that refuses a request; the endpoint answers its
/// <see cref="Kind"/>'s status with its message, which therefore never holds a token or a password.
/// </summary>
public sealed class ControlRefusalException : Exception
{
    /// <summary>Creates a refusal.</summary>
    /// <param name="kind">Why the request is refused.</param>
    /// <param name="message">What the client is told.</param>
    public ControlRefusalException(ControlRefusalKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Creates a bad-request refusal with a general message.</summary>
    public ControlRefusalException()
        : this(ControlRefusalKind.BadRequest, "The request was refused.")
    {
    }

    /// <summary>Creates a bad-request refusal.</summary>
    /// <param name="message">What the client is told.</param>
    public ControlRefusalException(string message)
        : this(ControlRefusalKind.BadRequest, message)
    {
    }

    /// <summary>Creates a bad-request refusal caused by another failure.</summary>
    /// <param name="message">What the client is told.</param>
    /// <param name="innerException">The failure behind the refusal.</param>
    public ControlRefusalException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = ControlRefusalKind.BadRequest;
    }

    /// <summary>Why the request is refused.</summary>
    public ControlRefusalKind Kind { get; }
}
