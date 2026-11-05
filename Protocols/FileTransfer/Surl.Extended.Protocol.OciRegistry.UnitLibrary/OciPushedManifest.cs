using System.Text.Json;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A manifest push's body read and checked (Image "Media Types", "Image Manifest", "Image Index",
/// "Descriptors"; ADR-0115, decision 3.1): a JSON object with <c>"schemaVersion":2</c>, a
/// <c>mediaType</c>, when present, equal to the <c>Content-Type</c>, and for an image manifest a
/// <c>config</c> and <c>layers</c>, for an image index <c>manifests</c>, each descriptor with a
/// <c>mediaType</c>, a valid digest and a non-negative <c>size</c>.
/// </summary>
/// <param name="ArtifactType">The top-level <c>artifactType</c>, or <see langword="null"/>.</param>
/// <param name="SubjectDigest">The <c>subject.digest</c>, or <see langword="null"/>.</param>
/// <param name="RequiredBlobs">The blobs that must be in the repository: an image manifest's config and its layers without <c>urls</c>.</param>
/// <param name="RequiredManifests">The manifests that must be in the repository: an image index's entries.</param>
internal sealed record OciPushedManifest(
    string? ArtifactType,
    string? SubjectDigest,
    IReadOnlyList<OciDigest> RequiredBlobs,
    IReadOnlyList<OciDigest> RequiredManifests)
{
    /// <summary>
    /// The media types a manifest may be pushed as: OCI's image manifest and index, and Docker's
    /// schema 2 manifest and manifest list.
    /// </summary>
    public static readonly IReadOnlySet<string> PushableMediaTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "application/vnd.oci.image.manifest.v1+json",
        OciDescriptor.ImageIndexMediaType,
        "application/vnd.docker.distribution.manifest.v2+json",
        OciDescriptor.DockerManifestListMediaType,
    };

    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 64 };

    /// <summary>
    /// Reads and checks a pushed manifest.
    /// </summary>
    /// <param name="body">The body as pushed.</param>
    /// <param name="mediaType">The <c>Content-Type</c> without parameters, one of <see cref="PushableMediaTypes"/>.</param>
    /// <returns>The manifest, or <see langword="null"/> for <c>MANIFEST_INVALID</c>.</returns>
    public static OciPushedManifest? Read(byte[] body, string mediaType)
    {
        try
        {
            using var document = JsonDocument.Parse(body, Options);

            return IsSchemaVersion2Of(document.RootElement, mediaType) ? ReadChecked(document.RootElement, mediaType) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsSchemaVersion2Of(JsonElement root, string mediaType) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number == 2
        && (!root.TryGetProperty("mediaType", out var declared) || (declared.ValueKind == JsonValueKind.String && declared.GetString() == mediaType));

    private static OciPushedManifest? ReadChecked(JsonElement root, string mediaType)
    {
        var artifactType = StringProperty(root, "artifactType");
        if (!TryReadSubject(root, out var subject))
        {
            return null;
        }

        if (mediaType is OciDescriptor.ImageIndexMediaType or OciDescriptor.DockerManifestListMediaType)
        {
            return DescriptorDigests(root, "manifests", includesExternal: true) is { } manifests
                ? new OciPushedManifest(artifactType, subject?.ToString(), [], manifests)
                : null;
        }

        var config = root.TryGetProperty("config", out var configElement) ? DigestOf(configElement) : null;
        var layers = DescriptorDigests(root, "layers", includesExternal: false);

        return config is not null && layers is not null
            ? new OciPushedManifest(artifactType, subject?.ToString(), [config, .. layers], [])
            : null;
    }

    // A subject, when present, must be a valid descriptor.
    private static bool TryReadSubject(JsonElement root, out OciDigest? subject)
    {
        subject = root.TryGetProperty("subject", out var element) ? DigestOf(element) : null;

        return subject is not null || !root.TryGetProperty("subject", out _);
    }

    // The digests of an array of descriptors; null when it is missing or any entry is invalid. A
    // layer with urls is non-distributable, so it is checked but not required unless includesExternal.
    private static List<OciDigest>? DescriptorDigests(JsonElement root, string name, bool includesExternal)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var digests = new List<OciDigest>();
        foreach (var entry in array.EnumerateArray())
        {
            if (DigestOf(entry) is not { } digest)
            {
                return null;
            }

            if (includesExternal || !entry.TryGetProperty("urls", out _))
            {
                digests.Add(digest);
            }
        }

        return digests;
    }

    // A descriptor's digest when it is an object with a mediaType, a valid digest and a non-negative size.
    private static OciDigest? DigestOf(JsonElement descriptor)
    {
        if (descriptor.ValueKind != JsonValueKind.Object || StringProperty(descriptor, "mediaType") is null)
        {
            return null;
        }

        var hasSize = descriptor.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var length) && length >= 0;

        return hasSize && StringProperty(descriptor, "digest") is { } digest ? OciDigest.Parse(digest) : null;
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
