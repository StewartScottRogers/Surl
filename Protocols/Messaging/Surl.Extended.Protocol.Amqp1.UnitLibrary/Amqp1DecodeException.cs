namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Thrown when bytes do not decode as AMQP 1.0 says. <see cref="Condition"/> is the error
/// condition the container closes the connection with (ADR-0107 decision 3):
/// <see cref="Amqp1ErrorCondition.DecodeError"/> for a value that does not decode or a field that
/// is missing or of the wrong type, <see cref="Amqp1ErrorCondition.FramingError"/> for a frame
/// body that is not a performative the container knows.
/// </summary>
public sealed class Amqp1DecodeException : Exception
{
    /// <summary>Creates the exception with a general message and <see cref="Amqp1ErrorCondition.DecodeError"/>.</summary>
    public Amqp1DecodeException()
        : this("The bytes do not decode as AMQP 1.0.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and <see cref="Amqp1ErrorCondition.DecodeError"/>.</summary>
    /// <param name="message">What could not be read.</param>
    public Amqp1DecodeException(string message)
        : this(message, Amqp1ErrorCondition.DecodeError)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>, its cause and <see cref="Amqp1ErrorCondition.DecodeError"/>.</summary>
    /// <param name="message">What could not be read.</param>
    /// <param name="innerException">The cause.</param>
    public Amqp1DecodeException(string message, Exception innerException)
        : base(message, innerException)
    {
        Condition = Amqp1ErrorCondition.DecodeError;
    }

    /// <summary>Creates the exception with <paramref name="message"/> and <paramref name="condition"/>.</summary>
    /// <param name="message">What could not be read.</param>
    /// <param name="condition">The error condition to close the connection with.</param>
    public Amqp1DecodeException(string message, Amqp1Symbol condition)
        : base(message)
    {
        Condition = condition;
    }

    /// <summary>The error condition the container closes the connection with.</summary>
    public Amqp1Symbol Condition { get; }
}
