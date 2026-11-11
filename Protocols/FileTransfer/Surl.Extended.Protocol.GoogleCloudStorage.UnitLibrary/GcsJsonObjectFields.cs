using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The writable fields of an object resource a JSON API request carries (ADR-0148, decision 3): <c>name</c>,
/// <c>contentType</c>, <c>contentEncoding</c>, <c>contentDisposition</c>, <c>cacheControl</c>, <c>contentLanguage</c>,
/// custom <c>metadata</c>, and the <c>md5Hash</c> and <c>crc32c</c> an upload is checked against. Every other member is ignored.
/// A member sent as <c>null</c> is present and empty, which a patch reads as "remove".
/// </summary>
internal sealed class GcsJsonObjectFields
{
    private static readonly string[] StringMembers =
        ["name", "contentType", "contentEncoding", "contentDisposition", "cacheControl", "contentLanguage", "md5Hash", "crc32c"];

    private readonly Dictionary<string, string?> members = new(StringComparer.Ordinal);
    private bool hasMetadata;
    private Dictionary<string, string?>? metadata;

    private GcsJsonObjectFields()
    {
    }

    /// <summary>
    /// The object's name as the body names it, or <see langword="null"/> when it names none.
    /// </summary>
    public string? Name => Member("name");

    /// <summary>
    /// The digests the body says the bytes have.
    /// </summary>
    /// <returns>The digests, or <see langword="null"/> when <c>md5Hash</c> or <c>crc32c</c> is not the base64 of a digest.</returns>
    public GcsSentDigests? SentDigests() => GcsSentDigests.FromBase64(Member("md5Hash"), Member("crc32c"));

    /// <summary>
    /// Reads a body.
    /// </summary>
    /// <param name="json">The body's UTF-8 bytes.</param>
    /// <returns>The fields, or <see langword="null"/> when the body is not a JSON object, a field named above is not a string
    /// or <c>null</c>, or <c>metadata</c> is not an object of strings and <c>null</c>s.</returns>
    public static GcsJsonObjectFields? Read(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadObject(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The metadata an object is stored with.
    /// </summary>
    /// <param name="current">The object a patch merges into, or <see langword="null"/> for an insert or an update, which take
    /// the body's fields alone.</param>
    /// <returns>The metadata, without ETag, generation or metageneration.</returns>
    public ObjectMetadata Compose(ObjectProperties? current) => new()
    {
        ContentType = Member("contentType", current?.ContentType),
        ContentEncoding = Member("contentEncoding", current?.ContentEncoding),
        ContentDisposition = Member("contentDisposition", current?.ContentDisposition),
        CacheControl = Member("cacheControl", current?.CacheControl),
        ContentLanguage = Member("contentLanguage", current?.ContentLanguage),
        UserMetadata = UserMetadataOf(current),
    };

    private static GcsJsonObjectFields? ReadObject(JsonElement root)
    {
        var fields = new GcsJsonObjectFields();
        foreach (var member in root.EnumerateObject())
        {
            if (!fields.TryTake(member))
            {
                return null;
            }
        }

        return fields;
    }

    private bool TryTake(JsonProperty member)
    {
        if (member.Name == "metadata")
        {
            hasMetadata = true;

            return TryTakeMetadata(member.Value);
        }

        if (!StringMembers.Contains(member.Name))
        {
            return true;
        }

        if (member.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            return false;
        }

        members[member.Name] = member.Value.GetString();

        return true;
    }

    private bool TryTakeMetadata(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            metadata = null;

            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        metadata = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateObject())
        {
            if (entry.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                return false;
            }

            metadata[entry.Name] = entry.Value.GetString();
        }

        return true;
    }

    private string? Member(string name, string? whenAbsent = null) =>
        members.TryGetValue(name, out var value) ? value : whenAbsent;

    // No metadata member keeps what there is; a null one clears it; an object merges by key, a null value removing the key.
    private Dictionary<string, string> UserMetadataOf(ObjectProperties? current)
    {
        var existing = (current?.UserMetadata ?? []).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (!hasMetadata)
        {
            return existing;
        }

        var result = metadata is null || current is null ? new Dictionary<string, string>(StringComparer.Ordinal) : existing;
        foreach (var (name, value) in metadata ?? new Dictionary<string, string?>())
        {
            if (value is null)
            {
                result.Remove(name);
            }
            else
            {
                result[name] = value;
            }
        }

        return result;
    }
}
