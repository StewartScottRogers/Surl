using System.Text;
using System.Text.Json;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The result shapes every tool shares (ADR-0103 decision 3): an object as <c>structuredContent</c> and the same
/// object as the one text block, a refusal, and a <c>surl</c> exit code with its member's name.
/// </summary>
internal static class ToolResults
{
    /// <summary>A result whose <c>structuredContent</c> is the object <paramref name="writeMembers"/> writes, and whose one text block is that object as JSON.</summary>
    /// <param name="writeMembers">Writes the object's members.</param>
    /// <param name="isError">Whether the tool failed.</param>
    /// <returns>The result.</returns>
    public static McpToolResult FromObject(Action<Utf8JsonWriter> writeMembers, bool isError = false)
    {
        var json = WriteObject(writeMembers);
        using var document = JsonDocument.Parse(json);
        return new McpToolResult([json], document.RootElement, isError);
    }

    /// <summary>A refusal by one of the host's rules: <c>refused: &lt;rule&gt;</c>, with <c>{"refused": "&lt;rule&gt;"}</c>.</summary>
    /// <param name="rule">The rule the call broke.</param>
    /// <returns>An <c>isError</c> result.</returns>
    public static McpToolResult Refused(string rule)
    {
        var json = WriteObject(writer => writer.WriteString("refused", rule));
        using var document = JsonDocument.Parse(json);
        return new McpToolResult([$"refused: {rule}"], document.RootElement, isError: true);
    }

    /// <summary>Writes <c>exit_code</c> and <c>exit_code_name</c>, the <see cref="SurlExitCode"/> member's name.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="exitCode">The exit code.</param>
    public static void WriteExitCode(Utf8JsonWriter writer, SurlExitCode exitCode)
    {
        writer.WriteNumber("exit_code", (int)exitCode);
        writer.WriteString("exit_code_name", exitCode.ToString());
    }

    /// <summary>Writes <paramref name="values"/> as an array of strings named <paramref name="name"/>.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="name">The member's name.</param>
    /// <param name="values">The strings.</param>
    public static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string WriteObject(Action<Utf8JsonWriter> writeMembers)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
