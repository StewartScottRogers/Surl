using Surl.Content;

namespace Surl.Protocol.Dict;

/// <summary>
/// The one DICT database Surl serves: the files directly in the content store's served root.
/// A file's name is a headword and its bytes are the headword's definition (ADR-0011).
/// </summary>
/// <remarks>
/// Headwords are compared ordinally, case and all, the way the content store names files on
/// every platform. A word that starts with <c>.</c>, a directory, and a name the content
/// store refuses (one holding <c>/</c>, <c>\</c> or <c>:</c>, for instance) are never
/// headwords, so they are answered as having no definition (ADR-0006, section 2).
/// </remarks>
internal sealed class DictContentDictionary
{
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates the dictionary of the files in <paramref name="contentStore"/>'s served root.
    /// </summary>
    /// <param name="contentStore">The content store whose root holds the definitions.</param>
    public DictContentDictionary(ContentStore contentStore)
    {
        this.contentStore = contentStore;
    }

    /// <summary>
    /// Finds the file that defines <paramref name="word"/>.
    /// </summary>
    /// <param name="word">The headword asked for.</param>
    /// <returns>The file's mapping and status, or <see langword="null"/> when the word has no definition.</returns>
    public DictDefinitionFile? FindDefinition(string word)
    {
        if (!IsHeadwordName(word))
        {
            return null;
        }

        var mapping = contentStore.MapRequestPath("/" + Uri.EscapeDataString(word));
        if (!mapping.IsMapped)
        {
            return null;
        }

        return contentStore.GetFileStatus(mapping) is { } status ? new DictDefinitionFile(mapping, status) : null;
    }

    /// <summary>
    /// Lists the headwords <paramref name="strategy"/> matches with <paramref name="word"/>.
    /// </summary>
    /// <param name="strategy">How a headword is compared with the word.</param>
    /// <param name="word">The word asked for.</param>
    /// <param name="cancellationToken">Cuts the listing off.</param>
    /// <returns>The matching headwords, in ordinal order.</returns>
    public IReadOnlyList<string> MatchHeadwords(DictMatchStrategy strategy, string word, CancellationToken cancellationToken)
    {
        var listing = contentStore.ListDirectory(contentStore.MapRequestPath("/"), cancellationToken);

        return listing.Entries
            .Where(entry => entry.Kind == ContentEntryKind.File && IsHeadwordName(entry.Name))
            .Select(entry => entry.Name)
            .Where(name => strategy.Matches(name, word))
            .ToList();
    }

    /// <summary>
    /// Copies the definition in <paramref name="file"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="file">A file <see cref="FindDefinition"/> returned.</param>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the copy off.</param>
    /// <returns>A task that completes when the file has been copied, or as much of it as is left if it shrank.</returns>
    public Task CopyDefinitionAsync(DictDefinitionFile file, Stream destination, CancellationToken cancellationToken) =>
        contentStore.CopyFileBytesAsync(file.Mapping, ContentByteRange.WholeFile(file.Status.Length), destination, cancellationToken);

    private static bool IsHeadwordName(string word) => word.Length > 0 && word[0] != '.';
}
