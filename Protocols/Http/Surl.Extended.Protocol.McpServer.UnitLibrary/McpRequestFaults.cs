using System.Globalization;
using System.Text.Json;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Asks the exchange's fault plan, for each JSON-RPC request in a session, whether to answer it with a
/// chosen JSON-RPC error instead (ADR-0124, decisions 4 and 9; ADR-0144): the point names the scheme
/// (<c>mcp</c> or <c>mcps</c>), the login as <c>account</c> (none for the anonymous caller), the endpoint's path as <c>path</c>, and as
/// <c>command</c> the tool's name for <c>tools/call</c> and the method for every other request.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule is applied, and so is an
/// <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="TryParse"/>); each publishes one
/// <see cref="FaultAppliedEvent"/> naming the path. A rule of any other kind, or a reply that does not
/// fit, is noted as not applied and the request is answered as without it.
/// </remarks>
internal static class McpRequestFaults
{
    private const string ToolsCallMethod = "tools/call";

    /// <summary>
    /// Offers a request to the fault plan and applies what it finds, publishing the
    /// <see cref="FaultAppliedEvent"/>: a <see cref="FaultKind.Delay"/> rule holds the answer for its
    /// <c>ms</c> on the exchange's clock with the idle clock paused, a <see cref="FaultKind.Drop"/> rule
    /// drops the exchange, and an <see cref="FaultKind.Error"/> rule gives its chosen JSON-RPC error.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="request">The request as parsed.</param>
    /// <param name="login">The session's login, or <see langword="null" /> for the anonymous caller.</param>
    /// <param name="endpointPath">The endpoint's path (<c>/mcp</c>).</param>
    /// <returns>What the request is answered with.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async Task<McpFaultOutcome> ApplyAsync(ExchangeContext context, JsonRpcRequest request, string? login, string endpointPath)
    {
        FaultPoint point = new(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = string.IsNullOrEmpty(login) ? null : login,
            Path = endpointPath,
            Command = CommandOf(request),
        };
        FaultRule? rule = await CommandStageFaults.FindAndApplyAsync(context, point, endpointPath).ConfigureAwait(false);

        return rule?.Kind switch
        {
            null or FaultKind.Delay => McpFaultOutcome.AnsweredAsUsual,
            FaultKind.Drop => McpFaultOutcome.Dropped,
            _ => ChooseError(context, rule, request, endpointPath),
        };
    }

    private static McpFaultOutcome ChooseError(ExchangeContext context, FaultRule rule, JsonRpcRequest request, string endpointPath)
    {
        if (rule.Kind != FaultKind.Error || !TryParse(rule.Reply, out JsonRpcError? error))
        {
            string why = rule.Kind == FaultKind.Error ? "its reply does not fit the mcp error shape" : $"a {rule.Kind} rule is not applied at a request";
            context.Log.Note($"Fault rule {rule.Id} not applied to the request: {why}.");
            return McpFaultOutcome.AnsweredAsUsual;
        }

        context.Log.Note($"The request was answered with JSON-RPC error {error.Code} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, endpointPath));
        return new McpFaultOutcome(JsonRpcResponseWriter.WriteError(request.Id, error), false);
    }

    /// <summary>
    /// The request as a fault rule's <c>command</c> names it: the tool's <c>name</c> for a <c>tools/call</c>
    /// that gives one as a string, the method otherwise.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The command.</returns>
    public static string CommandOf(JsonRpcRequest request)
    {
        if (request.Method == ToolsCallMethod
            && request.Params is { ValueKind: JsonValueKind.Object } parameters
            && parameters.TryGetProperty("name", out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString()!;
        }

        return request.Method;
    }

    /// <summary>
    /// Reads a reply in the <c>mcp</c> error shape: a JSON-RPC error code, a whole number with an optional
    /// leading <c>-</c>, then a space and the error's message.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null" /> when it gave none.</param>
    /// <param name="error">The error the reply names.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryParse(string? reply, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsonRpcError? error)
    {
        error = null;
        int space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        if (space <= 0 || space == reply!.Length - 1
            || !int.TryParse(reply.AsSpan(0, space), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int code))
        {
            return false;
        }

        error = new JsonRpcError(code, reply[(space + 1)..]);
        return true;
    }
}
