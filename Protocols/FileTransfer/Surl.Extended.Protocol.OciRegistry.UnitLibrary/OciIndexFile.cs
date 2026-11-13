using System.Text.Encodings.Web;
using System.Text.Json;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Edits and renders a repository's <c>index.json</c> (Image "OCI Image Layout Specification";
/// ADR-0115, decisions 2.1 and 3.1): one descriptor per tag, annotated
/// <c>org.opencontainers.image.ref.name</c>, and one untagged descriptor for each manifest no tag
/// names, so every manifest pushed stays listed.
/// </summary>
internal static class OciIndexFile
{
    /// <summary>
    /// The most descriptors a repository's <c>index.json</c> holds; a push past it is
    /// <c>403 DENIED</c>, <c>repository is full</c> (ADR-0115, decision 7).
    /// </summary>
    public const int MaxDescriptors = 10000;

    // Media types hold a '+', which the default encoder would write as +.
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The index with <paramref name="manifest"/> tagged <paramref name="tag"/>: the tag moves to
    /// it, an untagged descriptor of it is dropped, and the manifest the tag named before stays
    /// listed untagged if nothing else lists it.
    /// </summary>
    /// <param name="index">The descriptors listed now.</param>
    /// <param name="manifest">The manifest pushed, without annotations.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The new descriptors.</returns>
    public static List<OciDescriptor> WithTag(IReadOnlyList<OciDescriptor> index, OciDescriptor manifest, string tag)
    {
        var kept = index.Where(descriptor => descriptor.Tag != tag && !(descriptor.Tag is null && descriptor.Digest == manifest.Digest)).ToList();
        var tagged = manifest with { Annotations = [new(OciDescriptor.RefNameAnnotation, tag)] };

        return KeepListed([.. kept, tagged], index);
    }

    /// <summary>
    /// The index with <paramref name="manifest"/> listed untagged, unless a descriptor already
    /// lists its digest.
    /// </summary>
    /// <param name="index">The descriptors listed now.</param>
    /// <param name="manifest">The manifest pushed, without annotations.</param>
    /// <returns>The new descriptors.</returns>
    public static List<OciDescriptor> WithManifest(IReadOnlyList<OciDescriptor> index, OciDescriptor manifest) =>
        index.Any(descriptor => descriptor.Digest == manifest.Digest) ? [.. index] : [.. index, manifest];

    /// <summary>
    /// The index without the descriptor of <paramref name="tag"/>; its manifest stays listed
    /// untagged if nothing else lists it.
    /// </summary>
    /// <param name="index">The descriptors listed now.</param>
    /// <param name="tag">The tag deleted.</param>
    /// <returns>The new descriptors.</returns>
    public static List<OciDescriptor> WithoutTag(IReadOnlyList<OciDescriptor> index, string tag) =>
        KeepListed(index.Where(descriptor => descriptor.Tag != tag).ToList(), index);

    /// <summary>
    /// The index without any descriptor of <paramref name="digest"/>.
    /// </summary>
    /// <param name="index">The descriptors listed now.</param>
    /// <param name="digest">The manifest deleted.</param>
    /// <returns>The new descriptors.</returns>
    public static List<OciDescriptor> WithoutDigest(IReadOnlyList<OciDescriptor> index, string digest) =>
        index.Where(descriptor => descriptor.Digest != digest).ToList();

    /// <summary>
    /// Renders an image index listing <paramref name="descriptors"/>.
    /// </summary>
    /// <param name="descriptors">The descriptors, in order.</param>
    /// <returns>The <c>index.json</c> bytes.</returns>
    public static byte[] Render(IReadOnlyList<OciDescriptor> descriptors)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 2);
            writer.WriteString("mediaType", OciDescriptor.ImageIndexMediaType);
            writer.WriteStartArray("manifests");
            foreach (var descriptor in descriptors)
            {
                WriteDescriptor(writer, descriptor);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    // Each manifest a removed descriptor listed, still listed untagged when nothing kept lists it.
    private static List<OciDescriptor> KeepListed(List<OciDescriptor> kept, IReadOnlyList<OciDescriptor> before)
    {
        foreach (var removed in before.Where(descriptor => !kept.Any(listed => listed.Digest == descriptor.Digest)).DistinctBy(descriptor => descriptor.Digest).ToList())
        {
            kept.Add(removed with { Annotations = removed.Annotations.Where(annotation => annotation.Key != OciDescriptor.RefNameAnnotation).ToList() });
        }

        return kept;
    }

    private static void WriteDescriptor(Utf8JsonWriter writer, OciDescriptor descriptor)
    {
        writer.WriteStartObject();
        writer.WriteString("mediaType", descriptor.MediaType);
        writer.WriteString("digest", descriptor.Digest);
        writer.WriteNumber("size", descriptor.Size);
        if (descriptor.ArtifactType is { } artifactType)
        {
            writer.WriteString("artifactType", artifactType);
        }

        if (descriptor.Annotations.Count > 0)
        {
            writer.WriteStartObject("annotations");
            foreach (var (name, value) in descriptor.Annotations)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        foreach (var (name, rawValue) in descriptor.ExtraFields)
        {
            writer.WritePropertyName(name);
            writer.WriteRawValue(rawValue, skipInputValidation: true);
        }

        writer.WriteEndObject();
    }
}
