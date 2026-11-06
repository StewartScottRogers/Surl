using System.Text.Json;

namespace Surl.McpServing;

/// <summary>A tool as its registry holds it: the tool, its input schema as JSON to list, and the schema read to check arguments.</summary>
/// <param name="Tool">The tool.</param>
/// <param name="InputSchemaJson">The input schema as parsed JSON, written as the tool's <c>inputSchema</c>.</param>
/// <param name="InputSchema">The input schema read, to check a call's arguments.</param>
internal sealed record McpRegisteredTool(McpTool Tool, JsonElement InputSchemaJson, McpToolInputSchema InputSchema);
