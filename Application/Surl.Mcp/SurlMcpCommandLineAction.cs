namespace Surl.Mcp;

/// <summary>What a <c>surl-mcp</c> command line asks for.</summary>
internal enum SurlMcpCommandLineAction
{
    /// <summary>Serve one MCP session on standard input and output.</summary>
    Serve,

    /// <summary><c>--help</c>: write the help to standard output and exit 0.</summary>
    ShowHelp,

    /// <summary><c>--version</c>: write the version to standard output and exit 0.</summary>
    ShowVersion,

    /// <summary>A word <c>surl-mcp</c> does not accept: write the refusal to standard error and exit 2.</summary>
    Refuse,
}
