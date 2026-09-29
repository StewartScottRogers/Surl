namespace Surl.Content;

/// <summary>
/// The outcome of mapping a request path onto the served root: either a location inside the
/// root, or a refusal carrying its reason.
/// </summary>
/// <remarks>
/// A path the store's <see cref="ContentExposureOptions"/> hide - a dot-file, or a path through
/// a symbolic link that is not followed - is mapped with <see cref="EntryKind"/> set to
/// <see cref="ContentEntryKind.None"/>, exactly as a path that does not exist, and every later
/// look at it through the store keeps answering that nothing is there.
/// </remarks>
public sealed class ContentPathMapping
{
    private ContentPathMapping(string? location, ContentEntryKind entryKind, ContentPathRefusal refusal, bool isAnsweredAsAbsent)
    {
        Location = location;
        EntryKind = entryKind;
        Refusal = refusal;
        IsAnsweredAsAbsent = isAnsweredAsAbsent;
    }

    /// <summary>
    /// Whether the request path was mapped to a location inside the served root.
    /// </summary>
    public bool IsMapped => Refusal == ContentPathRefusal.None;

    /// <summary>
    /// The full path, inside the served root, that the request path maps to: with every
    /// symbolic link resolved, or as joined to the root when the path is hidden;
    /// <see langword="null"/> when the request path was refused.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// What exists at <see cref="Location"/>; <see cref="ContentEntryKind.None"/> when nothing
    /// does, the path is hidden, or the request path was refused.
    /// </summary>
    public ContentEntryKind EntryKind { get; }

    /// <summary>
    /// Why the request path was refused; <see cref="ContentPathRefusal.None"/> when it was mapped.
    /// </summary>
    public ContentPathRefusal Refusal { get; }

    /// <summary>
    /// Whether the store's exposure options hide the path, so the store answers it as absent
    /// whatever is there.
    /// </summary>
    internal bool IsAnsweredAsAbsent { get; }

    internal static ContentPathMapping Mapped(string location, ContentEntryKind entryKind) =>
        new(location, entryKind, ContentPathRefusal.None, isAnsweredAsAbsent: false);

    internal static ContentPathMapping AnsweredAsAbsent(string location) =>
        new(location, ContentEntryKind.None, ContentPathRefusal.None, isAnsweredAsAbsent: true);

    internal static ContentPathMapping Refused(ContentPathRefusal refusal) =>
        new(null, ContentEntryKind.None, refusal, isAnsweredAsAbsent: false);
}
