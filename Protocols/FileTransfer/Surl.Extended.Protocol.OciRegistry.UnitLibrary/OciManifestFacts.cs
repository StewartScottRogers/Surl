namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What <see cref="OciManifestJson"/> read from an image index or image manifest.
/// </summary>
/// <param name="ArtifactType">The top-level <c>artifactType</c>, or <see langword="null"/>.</param>
/// <param name="ConfigMediaType">An image manifest's <c>config.mediaType</c>, or <see langword="null"/>.</param>
/// <param name="SubjectDigest">The <c>subject.digest</c>, or <see langword="null"/> when there is no subject.</param>
/// <param name="Annotations">The top-level string annotations.</param>
/// <param name="Manifests">An image index's <c>manifests</c>; empty for an image manifest.</param>
internal sealed record OciManifestFacts(
    string? ArtifactType,
    string? ConfigMediaType,
    string? SubjectDigest,
    IReadOnlyList<KeyValuePair<string, string>> Annotations,
    IReadOnlyList<OciDescriptor> Manifests);
