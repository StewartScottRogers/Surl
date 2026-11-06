namespace Surl.Content;

/// <summary>
/// Thrown by <see cref="ObjectMetadataIndex.LoadAsync"/> when the persisted object metadata index
/// cannot be loaded (ADR-0100, decision 2.3): it cannot be read, or it does not parse. Nothing is
/// loaded, and nothing is overwritten. The composition root answers it with
/// <c>CouldNotReadFile</c> (37) and <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>.
/// </summary>
public sealed class ObjectMetadataIndexLoadException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="filePath">The full path of the index.</param>
    /// <param name="reason">Why it cannot be loaded, the exception's message.</param>
    /// <param name="innerException">The exception behind it, or <see langword="null"/>.</param>
    public ObjectMetadataIndexLoadException(string filePath, string reason, Exception? innerException = null)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// The full path of the index.
    /// </summary>
    public string FilePath { get; }
}
