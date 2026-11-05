namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A content descriptor as an image index lists it (Image "Descriptors"): what a manifest is,
/// where its bytes are, how long they are, and the annotations that name a tag.
/// </summary>
/// <param name="MediaType">The manifest's media type, sent as its <c>Content-Type</c>.</param>
/// <param name="Digest">The digest of its bytes, as the index writes it.</param>
/// <param name="Size">Its length in bytes.</param>
/// <param name="ArtifactType">Its artifact type, or <see langword="null"/>.</param>
/// <param name="Annotations">Its string annotations, in the order written.</param>
internal sealed record OciDescriptor(
    string MediaType,
    string Digest,
    long Size,
    string? ArtifactType,
    IReadOnlyList<KeyValuePair<string, string>> Annotations)
{
    /// <summary>
    /// The annotation an OCI Image Layout's <c>index.json</c> names a tag with (Image "OCI Image
    /// Layout Specification"; ADR-0115, decision 2.1).
    /// </summary>
    public const string RefNameAnnotation = "org.opencontainers.image.ref.name";

    /// <summary>
    /// The media type of an image index.
    /// </summary>
    public const string ImageIndexMediaType = "application/vnd.oci.image.index.v1+json";

    /// <summary>
    /// The media type of a Docker manifest list, which is an image index too.
    /// </summary>
    public const string DockerManifestListMediaType = "application/vnd.docker.distribution.manifest.list.v2+json";

    /// <summary>
    /// The tag this descriptor carries, or <see langword="null"/> for an untagged manifest.
    /// </summary>
    public string? Tag => Annotations.FirstOrDefault(annotation => annotation.Key == RefNameAnnotation).Value;

    /// <summary>
    /// Whether the descriptor names an image index, whose own <c>manifests</c> are manifests of
    /// the repository too.
    /// </summary>
    public bool IsImageIndex => MediaType is ImageIndexMediaType or DockerManifestListMediaType;
}
