using System.Text.Json;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reads the fields of an image index or image manifest that pulls and the referrers API need
/// (Image "Image Index", "Image Manifest", "Descriptors"), to a depth of 64 (ADR-0115,
/// decision 3.1). Nothing here throws on bad JSON: a document that does not parse reads as
/// <see langword="null"/>, and a descriptor missing a field is skipped.
/// </summary>
internal static class OciManifestJson
{
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 64 };

    /// <summary>
    /// Reads a JSON document whose root is an object.
    /// </summary>
    /// <param name="json">The document's bytes.</param>
    /// <returns>The facts read from it, or <see langword="null"/> when it is not a JSON object.</returns>
    public static OciManifestFacts? Read(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object ? FactsOf(root) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OciManifestFacts FactsOf(JsonElement root) => new(
        StringProperty(root, "artifactType"),
        ObjectProperty(root, "config") is { } config ? StringProperty(config, "mediaType") : null,
        ObjectProperty(root, "subject") is { } subject ? StringProperty(subject, "digest") : null,
        AnnotationsOf(root),
        DescriptorsOf(root));

    private static List<OciDescriptor> DescriptorsOf(JsonElement root)
    {
        var descriptors = new List<OciDescriptor>();
        if (root.TryGetProperty("manifests", out var manifests) && manifests.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in manifests.EnumerateArray())
            {
                AddDescriptor(descriptors, entry);
            }
        }

        return descriptors;
    }

    private static void AddDescriptor(List<OciDescriptor> descriptors, JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var mediaType = StringProperty(entry, "mediaType");
        var digest = StringProperty(entry, "digest");
        if (mediaType is not null && digest is not null && SizeOf(entry) is { } size)
        {
            descriptors.Add(new OciDescriptor(mediaType, digest, size, StringProperty(entry, "artifactType"), AnnotationsOf(entry)));
        }
    }

    private static long? SizeOf(JsonElement entry) =>
        entry.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var value) && value >= 0
            ? value
            : null;

    private static List<KeyValuePair<string, string>> AnnotationsOf(JsonElement element)
    {
        var annotations = new List<KeyValuePair<string, string>>();
        if (ObjectProperty(element, "annotations") is { } found)
        {
            foreach (var property in found.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.String))
            {
                annotations.Add(new(property.Name, property.Value.GetString()!));
            }
        }

        return annotations;
    }

    private static JsonElement? ObjectProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
