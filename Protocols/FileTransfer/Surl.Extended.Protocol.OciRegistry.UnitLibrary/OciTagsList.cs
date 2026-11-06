using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// end-8a and end-8b, the tag list (Dist "Listing Tags"; ADR-0115, decision 5.2):
/// <c>{"name":"&lt;name&gt;","tags":[…]}</c>, the tags in ordinal order, after <c>last</c> when
/// given, at most <c>n</c> of them, and a <c>Link</c> to the next page when tags remain.
/// </summary>
internal static class OciTagsList
{
    /// <summary>
    /// The largest <c>n</c> accepted.
    /// </summary>
    public const int MaxCount = 10000;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Answers a tag list request for a repository that exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="query">The request's query, still percent-encoded.</param>
    /// <param name="cancellationToken">Cancels the read of the index.</param>
    /// <returns>The answer: <c>200</c> with the list, or <c>400 UNSUPPORTED</c> for a bad <c>n</c>.</returns>
    public static async Task<HttpAnswer> AnswerAsync(OciReply reply, OciRepository repository, string query, CancellationToken cancellationToken)
    {
        var countText = OciQueryParameters.First(query, "n");
        int? count = countText is null ? null : ReadCount(countText);
        if (count == -1)
        {
            return reply.Error(OciError.UnsupportedParameter);
        }

        var last = OciQueryParameters.First(query, "last");
        var tags = (await repository.ReadIndexAsync(cancellationToken))
            .Select(descriptor => descriptor.Tag)
            .OfType<string>()
            .Where(OciNames.IsTag)
            .Where(tag => last is null || string.CompareOrdinal(tag, last) > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var page = count is { } n ? tags.Take(n).ToList() : tags;
        var fields = new List<KeyValuePair<string, string>>();
        if (page.Count > 0 && page.Count < tags.Count)
        {
            fields.Add(new("Link", $"</v2/{repository.Name}/tags/list?n={count}&last={page[^1]}>; rel=\"next\""));
        }

        return reply.Bytes(HttpStatus.Ok, "application/json", fields, Render(repository.Name, page));
    }

    // A decimal count from 0 to MaxCount, else -1.
    private static int ReadCount(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count <= MaxCount ? count : -1;

    private static byte[] Render(string name, IReadOnlyList<string> tags)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteStartArray("tags");
            foreach (var tag in tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
