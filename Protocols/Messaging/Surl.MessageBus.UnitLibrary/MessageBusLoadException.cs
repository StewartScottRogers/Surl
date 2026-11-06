namespace Surl.MessageBus;

/// <summary>
/// Thrown by <see cref="MessageBus.LoadAsync"/> when a persisted message bus file cannot be
/// loaded (ADR-0100, decision 3.7): it cannot be read, does not parse, or holds more than the
/// bus's bounds allow. Nothing is overwritten. The composition root answers it with
/// <c>CouldNotReadFile</c> (37) and <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>.
/// </summary>
public sealed class MessageBusLoadException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="filePath">The full path of the file at fault.</param>
    /// <param name="reason">Why it cannot be loaded, the exception's message.</param>
    /// <param name="innerException">The exception behind it, or <see langword="null"/>.</param>
    public MessageBusLoadException(string filePath, string reason, Exception? innerException = null)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// The full path of the file at fault.
    /// </summary>
    public string FilePath { get; }
}
