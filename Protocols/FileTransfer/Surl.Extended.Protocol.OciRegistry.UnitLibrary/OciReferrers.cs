using System.Text.Encodings.Web;
using System.Text.Json;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// end-12a and end-12b, the referrers API (Dist "Listing Referrers"; ADR-0115, decision 5.3): an
/// image index of every manifest in the repository whose <c>subject.digest</c> is the digest
/// asked about, plus the entries of an image index tagged with the referrers tag schema
/// (<c>&lt;alg&gt;-&lt;hex&gt;</c>), optionally filtered by <c>artifactType</c>, ordered by digest
/// and never paged. An unknown subject is an empty index, never <c>404</c>.
/// </summary>
internal static class OciReferrers
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Answers a referrers request for a repository that exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="subject">The digest asked about.</param>
    /// <param name="query">The request's query, still percent-encoded.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The answer: <c>200</c> with the image index.</returns>
    public static async Task<HttpAnswer> AnswerAsync(OciReply reply, OciRepository repository, OciDigest subject, string query, CancellationToken cancellationToken)
    {
        var referrers = await FromSubjectsAsync(repository, subject.ToString(), cancellationToken);
        referrers.AddRange(await FromReferrersTagAsync(repository, subject, cancellationToken));
        var artifactType = OciQueryParameters.First(query, "artifactType");
        var selected = referrers
            .Where(referrer => artifactType is null || referrer.ArtifactType == artifactType)
            .DistinctBy(referrer => referrer.Digest, StringComparer.Ordinal)
            .OrderBy(referrer => referrer.Digest, StringComparer.Ordinal)
            .ToList();
        KeyValuePair<string, string>[] fields = artifactType is null ? [] : [new("OCI-Filters-Applied", "artifactType")];

        return reply.Bytes(HttpStatus.Ok, OciDescriptor.ImageIndexMediaType, fields, Render(selected));
    }

    // Each manifest whose subject is the digest, described as Dist's rule says: its own
    // artifactType, else an image manifest's config.mediaType, and its annotations.
    private static async Task<List<OciDescriptor>> FromSubjectsAsync(OciRepository repository, string subject, CancellationToken cancellationToken)
    {
        var referrers = new List<OciDescriptor>();
        foreach (var descriptor in await repository.ReadManifestDescriptorsAsync(cancellationToken))
        {
            if (await repository.ReadManifestAsync(descriptor, cancellationToken) is { } manifest && ReferrerOf(manifest, subject) is { } referrer)
            {
                referrers.Add(referrer);
            }
        }

        return referrers;
    }

    private static OciDescriptor? ReferrerOf(OciFoundManifest manifest, string subject) =>
        OciManifestJson.Read(manifest.Bytes) is { } facts && facts.SubjectDigest == subject
            ? new OciDescriptor(manifest.Descriptor.MediaType, manifest.Descriptor.Digest, manifest.Bytes.Length, facts.ArtifactType ?? facts.ConfigMediaType, facts.Annotations)
            : null;

    // The entries of an image index tagged <alg>-<hex> for the digest (Dist "Enabling the
    // Referrers API"), as that index lists them.
    private static async Task<IReadOnlyList<OciDescriptor>> FromReferrersTagAsync(OciRepository repository, OciDigest subject, CancellationToken cancellationToken)
    {
        var tag = $"{subject.Algorithm}-{subject.Encoded}";
        var tagged = (await repository.ReadIndexAsync(cancellationToken)).FirstOrDefault(descriptor => descriptor.Tag == tag && descriptor.IsImageIndex);
        var manifest = tagged is null ? null : await repository.ReadManifestAsync(tagged, cancellationToken);

        return manifest is not null && OciManifestJson.Read(manifest.Bytes) is { } facts ? facts.Manifests : [];
    }

    private static byte[] Render(IReadOnlyList<OciDescriptor> referrers)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 2);
            writer.WriteString("mediaType", OciDescriptor.ImageIndexMediaType);
            writer.WriteStartArray("manifests");
            foreach (var referrer in referrers)
            {
                WriteDescriptor(writer, referrer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteDescriptor(Utf8JsonWriter writer, OciDescriptor referrer)
    {
        writer.WriteStartObject();
        writer.WriteString("mediaType", referrer.MediaType);
        writer.WriteString("digest", referrer.Digest);
        writer.WriteNumber("size", referrer.Size);
        if (referrer.ArtifactType is not null)
        {
            writer.WriteString("artifactType", referrer.ArtifactType);
        }

        if (referrer.Annotations.Count > 0)
        {
            writer.WriteStartObject("annotations");
            foreach (var (name, value) in referrer.Annotations)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
