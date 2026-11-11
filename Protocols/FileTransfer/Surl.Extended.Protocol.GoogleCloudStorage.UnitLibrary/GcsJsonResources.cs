using System.Globalization;
using System.Text;
using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes the JSON API's resources (ADR-0148, decision 3) with <see cref="Utf8JsonWriter"/>, in the order the
/// decision gives: 64-bit numbers as strings, times as RFC 3339 with milliseconds, links in the request's scheme
/// and <c>Host</c>.
/// </summary>
/// <param name="origin">The request's scheme and <c>Host</c>, such as <c>http://127.0.0.1:4443</c>.</param>
internal sealed class GcsJsonResources(string origin)
{
    private const string ObjectKind = "storage#object";

    /// <summary>
    /// A <c>storage#object</c> as a document.
    /// </summary>
    /// <param name="bucket">The bucket's name.</param>
    /// <param name="name">The object's name.</param>
    /// <param name="properties">What the object answers with.</param>
    /// <param name="digests">The object's digests.</param>
    /// <returns>The document's bytes.</returns>
    public byte[] Object(string bucket, string name, ObjectProperties properties, GcsObjectDigests digests) =>
        GcsJson.Write(writer => WriteObject(writer, bucket, name, properties, digests));

    /// <summary>
    /// A finished <c>storage#rewriteResponse</c> as a document: <c>done</c> true, the size twice and the object written.
    /// </summary>
    /// <param name="bucket">The destination bucket's name.</param>
    /// <param name="name">The destination object's name.</param>
    /// <param name="properties">What the object written answers with.</param>
    /// <param name="digests">The object's digests.</param>
    /// <returns>The document's bytes.</returns>
    public byte[] Rewrite(string bucket, string name, ObjectProperties properties, GcsObjectDigests digests) => GcsJson.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "storage#rewriteResponse");
        writer.WriteString("totalBytesRewritten", Number(properties.Length));
        writer.WriteString("objectSize", Number(properties.Length));
        writer.WriteBoolean("done", true);
        writer.WritePropertyName("resource");
        WriteObject(writer, bucket, name, properties, digests);
        writer.WriteEndObject();
    });

    /// <summary>
    /// A <c>storage#bucket</c> as a document.
    /// </summary>
    /// <param name="name">The bucket's name.</param>
    /// <param name="time">The folder's last-write time, which stands for its creation too.</param>
    /// <returns>The document's bytes.</returns>
    public byte[] Bucket(string name, DateTimeOffset time) =>
        GcsJson.Write(writer => WriteBucket(writer, name, time));

    /// <summary>
    /// A <c>storage#buckets</c> page: every bucket the caller may read.
    /// </summary>
    /// <param name="buckets">The buckets, in order.</param>
    /// <returns>The document's bytes.</returns>
    public byte[] Buckets(IReadOnlyList<GcsBucketEntry> buckets) => GcsJson.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "storage#buckets");
        writer.WriteStartArray("items");
        foreach (var bucket in buckets)
        {
            WriteBucket(writer, bucket.Name, bucket.LastModifiedUtc);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>
    /// A <c>storage#objects</c> page.
    /// </summary>
    /// <param name="bucket">The bucket's name.</param>
    /// <param name="page">The page taken from the walk.</param>
    /// <param name="objects">Each listed object by name, with what it answers with and its digests.</param>
    /// <returns>The document's bytes.</returns>
    public byte[] Objects(string bucket, GcsListPage page, IReadOnlyList<(string Name, ObjectProperties Properties, GcsObjectDigests Digests)> objects) => GcsJson.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "storage#objects");
        if (page.LastName is { } lastName)
        {
            writer.WriteString("nextPageToken", GcsContinuationToken.Encode(lastName));
        }

        if (page.CommonPrefixes.Count > 0)
        {
            writer.WriteStartArray("prefixes");
            foreach (var prefix in page.CommonPrefixes)
            {
                writer.WriteStringValue(prefix);
            }

            writer.WriteEndArray();
        }

        writer.WriteStartArray("items");
        foreach (var (name, properties, digests) in objects)
        {
            WriteObject(writer, bucket, name, properties, digests);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Escaped(string text) => Uri.EscapeDataString(text);

    // The generation is the upload time in microseconds; one that is no time reads as the last write.
    private static DateTimeOffset TimeCreated(ObjectProperties properties) =>
        properties.Generation >= 0 && properties.Generation <= GcsGeneration.LastTimeInMicroseconds
            ? GcsGeneration.TimeOf(properties.Generation)
            : properties.LastWriteTimeUtc;

    private void WriteObject(Utf8JsonWriter writer, string bucket, string name, ObjectProperties properties, GcsObjectDigests digests)
    {
        var created = GcsXml.Time(TimeCreated(properties));
        var link = $"{origin}/storage/v1/b/{Escaped(bucket)}/o/{Escaped(name)}";
        writer.WriteStartObject();
        writer.WriteString("kind", ObjectKind);
        writer.WriteString("id", $"{bucket}/{name}/{Number(properties.Generation)}");
        writer.WriteString("selfLink", link);
        writer.WriteString("mediaLink", $"{origin}/download/storage/v1/b/{Escaped(bucket)}/o/{Escaped(name)}?generation={Number(properties.Generation)}&alt=media");
        writer.WriteString("name", name);
        writer.WriteString("bucket", bucket);
        writer.WriteString("generation", Number(properties.Generation));
        writer.WriteString("metageneration", Number(properties.Metageneration));
        writer.WriteString("contentType", properties.ContentType);
        writer.WriteString("storageClass", "STANDARD");
        writer.WriteString("size", Number(properties.Length));
        writer.WriteString("md5Hash", Convert.ToBase64String(digests.Md5));
        writer.WriteString("crc32c", Convert.ToBase64String(digests.Crc32c));
        writer.WriteString("etag", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Number(properties.Generation)}/{Number(properties.Metageneration)}")));
        writer.WriteString("timeCreated", created);
        writer.WriteString("updated", GcsXml.Time(properties.LastWriteTimeUtc));
        writer.WriteString("timeStorageClassUpdated", created);
        WriteWhenStored(writer, "contentEncoding", properties.ContentEncoding);
        WriteWhenStored(writer, "contentDisposition", properties.ContentDisposition);
        WriteWhenStored(writer, "cacheControl", properties.CacheControl);
        WriteWhenStored(writer, "contentLanguage", properties.ContentLanguage);
        WriteUserMetadata(writer, properties.UserMetadata);
        writer.WriteEndObject();
    }

    private void WriteBucket(Utf8JsonWriter writer, string name, DateTimeOffset time)
    {
        var text = GcsXml.Time(time);
        writer.WriteStartObject();
        writer.WriteString("kind", "storage#bucket");
        writer.WriteString("id", name);
        writer.WriteString("name", name);
        writer.WriteString("selfLink", $"{origin}/storage/v1/b/{Escaped(name)}");
        writer.WriteString("projectNumber", "0");
        writer.WriteString("metageneration", "1");
        writer.WriteString("location", "US");
        writer.WriteString("storageClass", "STANDARD");
        writer.WriteString("etag", "CAE=");
        writer.WriteString("timeCreated", text);
        writer.WriteString("updated", text);
        writer.WriteEndObject();
    }

    private static void WriteWhenStored(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteUserMetadata(Utf8JsonWriter writer, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        if (metadata.Count == 0)
        {
            return;
        }

        writer.WriteStartObject("metadata");
        foreach (var (name, value) in metadata)
        {
            writer.WriteString(name, value);
        }

        writer.WriteEndObject();
    }
}
