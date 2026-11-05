using System.Text.Json;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The <c>list_directory</c> tool (ADR-0111 decision 2): the entries of one folder, as text lines and as
/// <c>structuredContent</c>, at most <see cref="MaximumEntries" />. Registered only under <c>--list-directories</c>.
/// </summary>
public static class McpListDirectoryTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_directory";

    /// <summary>The most entries one call returns.</summary>
    public const int MaximumEntries = 1000;

    internal const string InputSchema =
        """{"type":"object","properties":{"path":{"type":"string","description":"The folder, '/'-separated and relative to the served directory; empty for the served directory itself.","default":""}}}""";

    /// <summary>Creates the tool for one caller.</summary>
    /// <param name="store">The content store.</param>
    /// <param name="caller">Who the session's calls run as.</param>
    /// <returns>The tool.</returns>
    public static McpTool Create(ContentStore store, ContentCaller caller) =>
        new(Name, "List a folder", "List the files and folders in a folder Surl serves.", InputSchema,
            (arguments, cancellationToken) => Task.FromResult(List(store, caller, arguments, cancellationToken)));

    private static McpToolResult List(ContentStore store, ContentCaller caller, JsonElement arguments, CancellationToken cancellationToken)
    {
        ContentPathMapping mapping = McpToolPath.Map(store, caller, McpToolPath.ReadRelativePath(arguments));
        if (!mapping.IsMapped)
        {
            return McpToolResult.FromFailure(McpToolFailureText.PathRefused);
        }

        ContentDirectoryListing listing = store.ListDirectory(mapping, cancellationToken);
        if (!listing.IsListed)
        {
            return McpToolResult.FromFailure(
                listing.LocationKind == ContentEntryKind.File ? McpToolFailureText.IsAFile : McpToolFailureText.NoSuchFileOrFolder);
        }

        return Describe(listing.Entries);
    }

    private static McpToolResult Describe(IReadOnlyList<ContentDirectoryEntry> entries)
    {
        bool isTruncated = entries.Count > MaximumEntries;
        ContentDirectoryEntry[] shown = [.. entries.Take(MaximumEntries)];
        IEnumerable<string> lines = shown.Select(entry => entry.Kind == ContentEntryKind.Directory ? entry.Name + "/" : entry.Name);
        string text = string.Join('\n', isTruncated ? lines.Append("...") : lines);
        JsonElement structured = McpStructuredContent.Write(writer =>
        {
            writer.WriteStartArray("entries");
            foreach (ContentDirectoryEntry entry in shown)
            {
                WriteEntry(writer, entry);
            }

            writer.WriteEndArray();
            if (isTruncated)
            {
                writer.WriteBoolean("is_truncated", true);
            }
        });
        return new McpToolResult([text], structured);
    }

    private static void WriteEntry(Utf8JsonWriter writer, ContentDirectoryEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("name", entry.Name);
        if (entry.Kind == ContentEntryKind.Directory)
        {
            writer.WriteString("kind", "directory");
        }
        else
        {
            writer.WriteString("kind", "file");
            writer.WriteNumber("size", entry.Length.GetValueOrDefault());
        }

        writer.WriteEndObject();
    }
}
