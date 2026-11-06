namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Thrown when a frame's payload does not decode as AMQP 0-9-1's data types say (section
/// 4.2.5): a field runs past the payload, a field-table type octet is unknown, a short string is
/// not well-formed UTF-8, or tables nest deeper than <see cref="Amqp0WireReader.MaxNestingDepth"/>.
/// The broker answers it with <see cref="Amqp0ReplyCode.SyntaxError"/> (ADR-0112 decision 7).
/// </summary>
public sealed class Amqp0SyntaxException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public Amqp0SyntaxException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    /// <param name="message">What could not be read.</param>
    public Amqp0SyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and its cause.</summary>
    /// <param name="message">What could not be read.</param>
    /// <param name="innerException">The cause.</param>
    public Amqp0SyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The reply code the broker answers with: always <see cref="Amqp0ReplyCode.SyntaxError"/>.</summary>
    public Amqp0ReplyCode ReplyCode => Amqp0ReplyCode.SyntaxError;
}
