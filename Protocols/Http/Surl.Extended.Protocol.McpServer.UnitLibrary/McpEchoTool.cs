using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>The <c>echo</c> tool (ADR-0111 decision 2): returns the text it is given, unchanged, and touches nothing.</summary>
public static class McpEchoTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "echo";

    internal const string InputSchema =
        """{"type":"object","properties":{"text":{"type":"string","description":"The text to return."}},"required":["text"]}""";

    /// <summary>Creates the tool.</summary>
    /// <returns>The tool.</returns>
    public static McpTool Create() =>
        new(Name, "Echo", "Return the text it is given, unchanged.", InputSchema,
            (arguments, _) => Task.FromResult(McpToolResult.FromText(arguments.GetProperty("text").GetString()!)));
}
