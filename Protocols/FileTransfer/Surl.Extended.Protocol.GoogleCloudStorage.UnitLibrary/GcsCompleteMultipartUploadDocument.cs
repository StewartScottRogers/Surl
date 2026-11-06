using System.Globalization;
using System.Xml;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Reads a multipart completion body ("POST Object complete multipart", "Request body"): a
/// <c>CompleteMultipartUpload</c> element holding one <c>Part</c> per part, each with a
/// <c>PartNumber</c> and an <c>ETag</c>, in the order the object is assembled.
/// </summary>
/// <remarks>
/// Names are matched without their namespace, as the page's example sends one and SDKs may not.
/// Other elements inside a <c>Part</c> - the <c>Checksum*</c> elements - are skipped; any other
/// element in the root, text in it, a part without exactly one <c>PartNumber</c> and one
/// <c>ETag</c>, a <c>PartNumber</c> that is not a decimal integer, no part at all, or more than
/// <see cref="MaxParts"/> parts is <c>400 InvalidArgument</c>. DTDs are prohibited and nothing is
/// resolved.
/// </remarks>
internal static class GcsCompleteMultipartUploadDocument
{
    /// <summary>
    /// The most bytes a body holds (ADR-0110, decision 9); more is <c>400 InvalidArgument</c>.
    /// </summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>
    /// The most parts a body names (ADR-0110, decision 9); more is <c>400 InvalidArgument</c>.
    /// </summary>
    public const int MaxParts = 10000;

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
    };

    /// <summary>
    /// Reads the parts a body names.
    /// </summary>
    /// <param name="body">The body's bytes.</param>
    /// <returns>The parts in the order sent, or <see langword="null"/> when the body is malformed.</returns>
    public static List<GcsCompletedPart>? Read(byte[] body)
    {
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(body), Settings);

            return ReadRoot(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // The root, its parts, then nothing but the end of the document.
    private static List<GcsCompletedPart>? ReadRoot(XmlReader reader)
    {
        // Only an element has the name CompleteMultipartUpload; whatever else comes first has another, or none.
        _ = reader.MoveToContent();
        if (reader.LocalName != "CompleteMultipartUpload" || reader.IsEmptyElement)
        {
            return null;
        }

        reader.Read();
        var parts = ReadParts(reader);

        // The next read after the root either ends the document or throws on whatever follows it.
        _ = reader.Read();

        return parts is { Count: > 0 } ? parts : null;
    }

    private static List<GcsCompletedPart>? ReadParts(XmlReader reader)
    {
        var parts = new List<GcsCompletedPart>();
        while (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Part" && parts.Count < MaxParts)
        {
            if (ReadPart(reader) is not { } part)
            {
                return null;
            }

            parts.Add(part);
        }

        return reader.NodeType == XmlNodeType.EndElement ? parts : null;
    }

    // One Part: its PartNumber and ETag, each once, other elements skipped.
    private static GcsCompletedPart? ReadPart(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return null;
        }

        reader.Read();
        var isWellFormed = ReadPartElements(reader, out var partNumber, out var eTag);

        // The Part's end element; anything else inside it throws here or fails below.
        reader.ReadEndElement();

        return isWellFormed && PartNumberOf(partNumber) is { } number && eTag is not null ? new GcsCompletedPart(number, eTag) : null;
    }

    // The elements inside a Part; false when PartNumber or ETag comes twice.
    private static bool ReadPartElements(XmlReader reader, out string? partNumber, out string? eTag)
    {
        partNumber = null;
        eTag = null;
        var isWellFormed = true;
        while (reader.NodeType == XmlNodeType.Element)
        {
            var name = reader.LocalName;
            var text = reader.ReadElementContentAsString();
            isWellFormed &= name switch
            {
                "PartNumber" => Assign(ref partNumber, text),
                "ETag" => Assign(ref eTag, text),
                _ => true,
            };
        }

        return isWellFormed;
    }

    // Takes an element's text the first time; a second element of the name is malformed.
    private static bool Assign(ref string? slot, string text)
    {
        var isFirst = slot is null;
        slot = text;

        return isFirst;
    }

    private static int? PartNumberOf(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}
