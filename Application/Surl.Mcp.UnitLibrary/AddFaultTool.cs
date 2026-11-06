using System.Text.Json;
using Surl.Api;
using Surl.Cli;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_fault</c> tool (ADR-0124 decisions 4 and 8): adds one <c>--fault</c> item to a running server's fault
/// plan, after its other rules, through <c>Surl.Api</c>'s <c>SurlServer.Faults.Add</c>. The item is read by
/// <c>surl</c>'s own <c>--fault</c> reader, so a bad item is an <c>isError</c> result with <c>surl</c>'s text
/// (<c>option --fault: ...</c>), and a 257th rule is refused as the plan refuses it. Result <c>{"fault_id"}</c>.
/// </summary>
public static class AddFaultTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_fault";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Make a running Surl server misbehave on purpose from its next connection, login or command on: add one fault "
        + "rule, written as surl's --fault item, e.g. drop;scheme=smtp;times=1, refuse-login;account=alice, "
        + "delay;ms=500;stage=command;path=/slow or error;scheme=http;reply=503 Busy. Returns the rule's ID for "
        + "remove_fault. Read the surl://aihelp/testing resource for every kind and key.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "fault": { "type": "string", "minLength": 1, "description": "One surl --fault item, e.g. drop;scheme=http;times=1" }
          },
          "required": ["server_id", "fault"],
          "additionalProperties": false
        }
        """;

    // The one listen URL the item is parsed beside: the reader checks the item alone, never the listen URLs.
    private const string ParsedBesideListenUrl = "http://127.0.0.1:0";

    /// <summary>Creates the tool, adding rules to servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Add a fault rule to a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Add(running, arguments)));

    /// <summary>Spells <paramref name="configured"/>, as <c>surl</c>'s reader read it, as the <see cref="SurlFault"/> <c>Surl.Api</c> takes.</summary>
    /// <param name="configured">The item as read.</param>
    /// <returns>The same rule.</returns>
    internal static SurlFault ToSurlFault(ConfiguredFault configured) =>
        KindOf(configured) with
        {
            Listener = configured.Listener,
            Scheme = configured.Scheme,
            Account = configured.Account,
            Path = configured.Path,
            Command = configured.Command,
            Stage = configured.Stage,
            AfterBytes = configured.AfterBytes,
            Times = configured.Times,
        };

    private static SurlFault KindOf(ConfiguredFault configured) => configured.Kind switch
    {
        FaultKind.Delay => SurlFault.Delay(TimeSpan.FromMilliseconds(configured.DelayMilliseconds!.Value)),
        FaultKind.Drop => SurlFault.DropConnection(),
        FaultKind.RefuseLogin => SurlFault.RefuseLogin(),
        FaultKind.Error => SurlFault.ProtocolError(configured.Reply!),
        _ => SurlFault.OutOfSpace(),
    };

    private static McpToolResult Add(RunningSurlServer running, JsonElement arguments)
    {
        var parsed = CommandLineParser.Parse([ParsedBesideListenUrl, "--fault", arguments.GetProperty("fault").GetString()!]);
        if (parsed.Outcome != CommandLineOutcome.Serve)
        {
            return McpToolResult.FromFailure(parsed.Failure!.Message);
        }

        var faultId = running.Server.Faults.Add(ToSurlFault(parsed.CommandLine!.Faults[0]));
        return ToolResults.FromObject(writer => writer.WriteString("fault_id", faultId));
    }
}
