namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The fixed <c>isError</c> texts of the MCP server's tools (ADR-0111 decision 2's table, and the cases it leaves to
/// BL-465). None echoes how a path resolved.
/// </summary>
public static class McpToolFailureText
{
    /// <summary>No such file or folder, or one the caller's share access hides.</summary>
    public const string NoSuchFileOrFolder = "no such file or folder";

    /// <summary>A path the content store refuses.</summary>
    public const string PathRefused = "the path is refused";

    /// <summary>A write the content store does not permit.</summary>
    public const string WritingNotPermitted = "writing is not permitted";

    /// <summary>A write past <c>--max-filesize</c>.</summary>
    public const string LargerThanMaxFilesize = "the file is larger than --max-filesize";

    /// <summary>A read whose answer would pass the message limit.</summary>
    public const string LargerThanOneMessage = "the file is larger than this server returns in one message";

    /// <summary>A <c>write_file</c> call with both or neither of <c>text</c> and <c>base64</c>.</summary>
    public const string ExactlyOneOfTextAndBase64 = "give exactly one of text and base64";

    /// <summary>A <c>write_file</c> call whose <c>base64</c> does not decode.</summary>
    public const string Base64NotValid = "base64 is not valid base64";

    /// <summary>A <c>read_file</c> call naming a folder.</summary>
    public const string IsAFolder = "the path is a folder, not a file";

    /// <summary>A <c>list_directory</c> call naming a file.</summary>
    public const string IsAFile = "the path is a file, not a folder";
}
