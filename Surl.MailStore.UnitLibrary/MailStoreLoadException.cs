namespace Surl.MailStore;

/// <summary>
/// Thrown by <see cref="MailboxStore.LoadAsync"/> when the persisted mail store cannot be
/// loaded (ADR-0050, decision 7): its index cannot be read or does not parse, or a message
/// file the index names is missing, unreadable or not the size the index gives. Nothing is
/// loaded, and nothing is overwritten. The composition root answers it with
/// <c>CouldNotReadFile</c> (37) and <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>.
/// </summary>
public sealed class MailStoreLoadException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="filePath">The full path of the file at fault.</param>
    /// <param name="reason">Why it cannot be loaded, the exception's message.</param>
    /// <param name="innerException">The exception behind it, or <see langword="null"/>.</param>
    public MailStoreLoadException(string filePath, string reason, Exception? innerException = null)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// The full path of the file at fault: the index, or one message file.
    /// </summary>
    public string FilePath { get; }
}
