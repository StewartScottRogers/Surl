namespace Surl.Content;

/// <summary>
/// The outcome of mapping a request path onto the served root: either a location inside the
/// root, or a refusal carrying its reason.
/// </summary>
public sealed class ContentPathMapping
{
    private ContentPathMapping(string? location, ContentEntryKind entryKind, ContentPathRefusal refusal)
    {
        Location = location;
        EntryKind = entryKind;
        Refusal = refusal;
    }

    /// <summary>
    /// Whether the request path was mapped to a location inside the served root.
    /// </summary>
    public bool IsMapped => Refusal == ContentPathRefusal.None;

    /// <summary>
    /// The full path, inside the served root and with every symbolic link resolved, that the
    /// request path maps to; <see langword="null"/> when the request path was refused.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// What exists at <see cref="Location"/>; <see cref="ContentEntryKind.None"/> when nothing
    /// does or the request path was refused.
    /// </summary>
    public ContentEntryKind EntryKind { get; }

    /// <summary>
    /// Why the request path was refused; <see cref="ContentPathRefusal.None"/> when it was mapped.
    /// </summary>
    public ContentPathRefusal Refusal { get; }

    internal static ContentPathMapping Mapped(string location, ContentEntryKind entryKind) =>
        new(location, entryKind, ContentPathRefusal.None);

    internal static ContentPathMapping Refused(ContentPathRefusal refusal) =>
        new(null, ContentEntryKind.None, refusal);
}
