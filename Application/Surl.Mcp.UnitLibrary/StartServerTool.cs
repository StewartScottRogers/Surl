using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>start_server</c> tool (ADR-0103 decisions 3 and 4): checks the listen URLs and options against the
/// host's rules, then starts a Surl server through <c>Surl.Api</c> with in-memory files and any start-time seeds for
/// its Redis keyspace, LDAP directory and DNS zones (ADR-0158), recording its exchanges, and returns its ID, its bound
/// URLs and the log lines written while it started. A refused seed is an <c>isError</c> result and nothing is left
/// running.
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
        + "Read the `surl://aihelp` resources for every scheme and option. "
        + "Seed Redis keys with redis_keys, LDAP entries with directory_ldif and DNS zones with dns_zones; they are "
        + "held before any listener binds and `reset_server` returns the server to them. A seed a store refuses, "
        + "or one for a store no listen URL serves, starts nothing and is answered with the store's words, which for "
        + "directory_ldif or dns_zones may quote part of the text; a Redis value is never shown back by this tool. "
        + "The whole `tools/call` message is at most 4 MiB (4194304 bytes); seed larger files one at a "
        + "time with `put_file`.";

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
              "description": "In-memory content: '/'-separated relative path -> base64 bytes" },
            "cookies": { "type": "array", "items": { "type": "string" }, "maxItems": 64,
              "description": "surl --set-cookie values, one per cookie, e.g. session=t1;Path=/;HttpOnly" },
            "redis_keys": { "type": "array",
              "description": "Redis string keys held at start, each as set_key takes it",
              "items": { "type": "object",
                "properties": {
                  "key": { "type": "string", "description": "The key as UTF-8 text" },
                  "key_base64": { "type": "string", "description": "The key as base64 bytes" },
                  "value": { "type": "string", "description": "The value as UTF-8 text" },
                  "value_base64": { "type": "string", "description": "The value as base64 bytes" },
                  "database": { "type": "integer", "minimum": 0, "maximum": 15, "description": "The database (default 0)" },
                  "expire_ms": { "type": "integer", "minimum": 1, "maximum": 31536000000, "description": "Milliseconds until the key expires (default: never)" }
                },
                "additionalProperties": false } },
            "directory_ldif": { "type": "string", "minLength": 1,
              "description": "LDAP entries held at start, as add_ldap_entries' ldif" },
            "dns_zones": { "type": "array",
              "description": "DNS zones held at start, each as add_dns_zone takes it",
              "items": { "type": "object",
                "properties": {
                  "apex": { "type": "string", "minLength": 1, "description": "The zone's apex, e.g. example.com" },
                  "zone_text": { "type": "string", "minLength": 1, "description": "The zone file's text" }
                },
                "required": ["apex", "zone_text"],
                "additionalProperties": false } }
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
        var refusal = safetyCheck.FindRefusal(listenUrls, words);
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
        AddAll(options.Cookies, ReadStrings(arguments, "cookies"));
        var contentFailure = AddFiles(options, arguments) ?? StartTimeSeedArguments.AddTo(options, arguments);
        return contentFailure is not null
            ? McpToolResult.FromFailure(contentFailure)
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
        AddAll(options.ListenUrls, listenUrls);
        AddAll(options.AdditionalArguments, words);

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
        catch (SurlSeedRefusedException exception)
        {
            return McpToolResult.FromFailure(exception.Message);
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

    private static void AddAll(IList<string> list, IReadOnlyList<string> items)
    {
        foreach (var item in items)
        {
            list.Add(item);
        }
    }

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
