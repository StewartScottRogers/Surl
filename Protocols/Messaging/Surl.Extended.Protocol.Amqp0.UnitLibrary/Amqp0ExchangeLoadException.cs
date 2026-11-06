namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Thrown by <see cref="Amqp0Broker.LoadExchangesAsync"/> when the broker's exchange file cannot be
/// loaded (ADR-0112 decision 6, with ADR-0031 decision 6's and ADR-0050 decision 7's rules): it
/// cannot be read, does not parse, or holds more than the broker's bounds allow. Nothing is
/// overwritten. The composition answers it with <c>CouldNotReadFile</c> (37) and
/// <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>, before any listener binds.
/// </summary>
public sealed class Amqp0ExchangeLoadException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="filePath">The full path of the file at fault.</param>
    /// <param name="reason">Why it cannot be loaded, the exception's message.</param>
    /// <param name="innerException">The exception behind it, or <see langword="null"/>.</param>
    public Amqp0ExchangeLoadException(string filePath, string reason, Exception? innerException = null)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>The full path of the file at fault.</summary>
    public string FilePath { get; }
}
