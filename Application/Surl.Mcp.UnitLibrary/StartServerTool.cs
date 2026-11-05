using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>start_server</c> tool (ADR-0103 decisions 3 and 4): checks the listen URLs and options against the
/// host's rules, then starts a Surl server through <c>Surl.Api</c> with in-memory files, recording its exchanges,
/// and returns its ID, its bound URLs and the log lines written while it started.
/// </summary>
public static class StartServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "start_server";

    /// <summary>The most exchanges each server records.</summary>
    internal const int MaxRecordedExchanges = 100;

    /// <summary>The most bytes each recorded exchange keeps, so eight servers hold at most 200 MiB.</summary>
    internal const int MaxRecordedBytesPerExchange = 262_144;

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Start a Surl protocol server that listens on the given URLs and answers clients as `surl` with the same "
        + "options would. Returns its ID and the URLs it bound. Use port 0 to let the system choose a free port. "
        + "Read the `surl://aihelp` resources for every scheme and option.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "listen_urls": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 16,
              "description": "What to listen on, as surl's listen URLs, e.g. http://127.0.0.1:0" },
            "arguments": { "type": "array", "items": { "type": "string" }, "maxItems": 256,
              "description": "Any other surl options, as surl's own words, e.g. [\"--user\", \"alice:secret\"]" },
            "files": { "type": "object",
              "description": "In-memory content: '/'-separated relative path -> UTF-8 text" },
            "files_base64": { "type": "object",
              "description": "In-memory content: '/'-separated relative path -> base64 bytes" }
          },
          "required": ["listen_urls"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, starting servers into <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        var safetyCheck = new StartServerSafetyCheck(servers.Settings, new PathConfinement(servers.Settings.Root));
        return Create(servers, safetyCheck);
    }

    /// <summary>Creates the tool with the given safety check.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <param name="safetyCheck">The checks made before a start.</param>
    /// <returns>The tool.</returns>
    internal static McpTool Create(RunningSurlServers servers, StartServerSafetyCheck safetyCheck) =>
        new(Name, "Start a Surl server", Description, InputSchema,
            (arguments, cancellationToken) => StartAsync(servers, safetyCheck, arguments, cancellationToken));

    private static async Task<McpToolResult> StartAsync(
        RunningSurlServers servers, StartServerSafetyCheck safetyCheck, JsonElement arguments, CancellationToken cancellationToken)
    {
        var listenUrls = ReadStrings(arguments, "listen_urls");
        var words = ReadStrings(arguments, "arguments");
        var refusal = safetyCheck.FindRefusal([.. listenUrls, .. words]);
        if (refusal is not null)
        {
            return ToolResults.Refused(refusal);
        }

        var log = new StartLogWriter(servers.ErrorOutput);
        var options = new SurlServerOptions
        {
            RecordExchanges = true,
            MaxRecordedExchanges = MaxRecordedExchanges,
            MaxRecordedBytesPerExchange = MaxRecordedBytesPerExchange,
            Log = log,
            LogLevel = servers.Settings.LogLevel,
            TimeProvider = servers.TimeProvider,
        };
        var fileFailure = AddFiles(options, arguments);
        return fileFailure is not null
            ? McpToolResult.FromFailure(fileFailure)
            : await StartAsync(servers, listenUrls, words, options, log, cancellationToken);
    }

    private static async Task<McpToolResult> StartAsync(
        RunningSurlServers servers,
        IReadOnlyList<string> listenUrls,
        IReadOnlyList<string> words,
        SurlServerOptions options,
        StartLogWriter log,
        CancellationToken cancellationToken)
    {
        foreach (var listenUrl in listenUrls)
        {
            options.ListenUrls.Add(listenUrl);
        }

        foreach (var word in words)
        {
            options.AdditionalArguments.Add(word);
        }

        try
        {
            var running = await servers.StartAsync(listenUrls, options, cancellationToken);
            return running is null
                ? ToolResults.Refused($"at most {servers.Settings.MaxServers} servers run at once; stop one with stop_server first")
                : Started(running, log.StopKeeping());
        }
        catch (SurlServerStartException exception)
        {
            return StartRefused(exception);
        }
        catch (ArgumentException exception)
        {
            return McpToolResult.FromFailure(exception.Message);
        }
    }

    private static McpToolResult Started(RunningSurlServer running, IReadOnlyList<string> logLines) =>
        ToolResults.FromObject(writer =>
        {
            writer.WriteString("server_id", running.ServerId);
            ToolResults.WriteStrings(writer, "listen_urls", running.ListenUrls);
            ToolResults.WriteStrings(writer, "bound_urls", running.Server.BoundUrls.Select(boundUrl => boundUrl.ToString()));
            ToolResults.WriteStrings(writer, "log_lines", logLines);
        });

    private static McpToolResult StartRefused(SurlServerStartException exception)
    {
        var structured = ToolResults.FromObject(writer =>
        {
            ToolResults.WriteExitCode(writer, exception.ExitCode);
            ToolResults.WriteStrings(writer, "lines", exception.Lines);
        });
        var text = string.Join("\n", exception.Lines.Select(line => "surl: " + line));
        return new McpToolResult([text], structured.StructuredContent, isError: true);
    }

    /// <summary>Reads the array of strings named <paramref name="name"/>; empty when absent. The schema has checked its shape.</summary>
    private static IReadOnlyList<string> ReadStrings(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var array)
            ? [.. array.EnumerateArray().Select(item => item.GetString()!)]
            : [];

    /// <summary>Adds <c>files</c> and <c>files_base64</c> to <paramref name="options"/>; returns why they are refused, or null.</summary>
    private static string? AddFiles(SurlServerOptions options, JsonElement arguments) =>
        AddFiles(options, arguments, "files", text => Encoding.UTF8.GetBytes(text))
        ?? AddFiles(options, arguments, "files_base64", DecodeBase64);

    private static string? AddFiles(SurlServerOptions options, JsonElement arguments, string name, Func<string, byte[]?> decode)
    {
        if (!arguments.TryGetProperty(name, out var files))
        {
            return null;
        }

        return files.EnumerateObject()
            .Select(file => AddFile(options, name, file, decode))
            .FirstOrDefault(failure => failure is not null);
    }

    private static string? AddFile(SurlServerOptions options, string name, JsonProperty file, Func<string, byte[]?> decode)
    {
        var contents = file.Value.ValueKind == JsonValueKind.String ? decode(file.Value.GetString()!) : null;
        if (contents is null)
        {
            return $"{name}.{file.Name}: expected {(name == "files" ? "a string" : "a base64 string")}";
        }

        return options.Files.TryAdd(file.Name, contents)
            ? null
            : $"{name}.{file.Name}: the path is given more than once across files and files_base64";
    }

    private static byte[]? DecodeBase64(string text)
    {
        var bytes = new byte[text.Length];
        return Convert.TryFromBase64String(text, bytes, out var written) ? bytes[..written] : null;
    }
}
