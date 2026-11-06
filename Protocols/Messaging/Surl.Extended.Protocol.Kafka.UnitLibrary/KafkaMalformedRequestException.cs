namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Thrown when a request's bytes do not decode as the Kafka protocol guide's primitive types
/// say: a field runs past the request, a length is negative where no null is allowed, a
/// variable-length integer is too long, or a string is not well-formed UTF-8. The broker
/// closes the connection with no bytes when a request cannot be read (ADR-0105 decision 7).
/// </summary>
public sealed class KafkaMalformedRequestException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public KafkaMalformedRequestException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    /// <param name="message">What could not be read.</param>
    public KafkaMalformedRequestException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and its cause.</summary>
    /// <param name="message">What could not be read.</param>
    /// <param name="innerException">The cause.</param>
    public KafkaMalformedRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
