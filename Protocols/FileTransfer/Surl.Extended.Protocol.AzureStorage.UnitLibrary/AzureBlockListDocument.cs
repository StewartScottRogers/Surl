using System.Xml;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Reads a Put Block List body ("Put Block List", "Request body"): a <c>BlockList</c> element whose
/// children are <c>Committed</c>, <c>Uncommitted</c> and <c>Latest</c> elements, each holding one
/// block ID, in the order the blob's blocks are to be committed.
/// </summary>
internal static class AzureBlockListDocument
{
    /// <summary>
    /// The most bytes a Put Block List body holds (ADR-0109, decision 7); more is
    /// <c>400 InvalidXmlDocument</c>.
    /// </summary>
    public const int MaxBodyBytes = 4 * 1024 * 1024;

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
    };

    /// <summary>
    /// Reads the entries of a body.
    /// </summary>
    /// <param name="body">The body's bytes.</param>
    /// <param name="entries">The entries, in the order sent.</param>
    /// <returns><c>InvalidXmlDocument</c> for a body that is not a <c>BlockList</c> of those
    /// elements, <c>BlockListTooLong</c> for one past 50000 entries, else null.</returns>
    public static AzureError? Read(byte[] body, out List<AzureBlockListEntry> entries)
    {
        entries = [];
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(body), Settings);

            return ReadBlockList(reader, entries);
        }
        catch (XmlException)
        {
            return AzureError.InvalidXmlDocument;
        }
    }

    // The root, each entry, then nothing but the end of the document.
    private static AzureError? ReadBlockList(XmlReader reader, List<AzureBlockListEntry> entries)
    {
        // Only an element has the name BlockList; whatever else comes first has another, or none.
        _ = reader.MoveToContent();
        if (reader.LocalName != "BlockList")
        {
            return AzureError.InvalidXmlDocument;
        }

        if (!reader.IsEmptyElement)
        {
            reader.Read();
            if (ReadEntries(reader, entries) is { } refusal)
            {
                return refusal;
            }
        }

        // Whitespace, comments and processing instructions are skipped, so the next read after the
        // root either ends the document or throws on whatever follows it.
        _ = reader.Read();

        return null;
    }

    private static AzureError? ReadEntries(XmlReader reader, List<AzureBlockListEntry> entries)
    {
        while (reader.NodeType == XmlNodeType.Element)
        {
            if (KindOf(reader.LocalName) is not { } kind)
            {
                return AzureError.InvalidXmlDocument;
            }

            if (entries.Count == AzureStagedBlocks.MaxCommittedBlocks)
            {
                return AzureError.BlockListTooLong;
            }

            entries.Add(new AzureBlockListEntry(kind, reader.ReadElementContentAsString()));
        }

        return reader.NodeType == XmlNodeType.EndElement ? null : AzureError.InvalidXmlDocument;
    }

    private static AzureBlockListKind? KindOf(string name) => name switch
    {
        "Committed" => AzureBlockListKind.Committed,
        "Uncommitted" => AzureBlockListKind.Uncommitted,
        "Latest" => AzureBlockListKind.Latest,
        _ => null,
    };
}
