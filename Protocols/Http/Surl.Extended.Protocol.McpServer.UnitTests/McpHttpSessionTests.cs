using System.Text;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpHttpSessionTests
{
    private const string WaitCall = """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"wait","arguments":{}}}""";

    [TestMethod]
    public async Task AcceptAsync_CancelledNotificationNamingARunningRequest_CancelsItAndItsAnswerIsNull()
    {
        (McpHttpSession session, TaskCompletionSource started) = await NewSessionAsync();
        Task<byte[]?> running = AnswerAsync(session, WaitCall);
        await started.Task;

        await AcceptAsync(session, """{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":5}}""");

        Assert.IsNull(await running);
    }

    [TestMethod]
    public async Task End_WithARequestRunning_CancelsIt()
    {
        (McpHttpSession session, TaskCompletionSource started) = await NewSessionAsync();
        Task<byte[]?> running = AnswerAsync(session, WaitCall);
        await started.Task;

        session.End();

        Assert.IsNull(await running);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":true}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":"5"}}""")]
    public async Task AcceptAsync_CancelledNotificationNotNamingTheRunningRequest_LeavesItRunning(string notification)
    {
        (McpHttpSession session, TaskCompletionSource started) = await NewSessionAsync();
        Task<byte[]?> running = AnswerAsync(session, WaitCall);
        await started.Task;

        await AcceptAsync(session, notification);

        Assert.IsFalse(running.IsCompleted);
        session.End();
        Assert.IsNull(await running);
    }

    [TestMethod]
    public async Task AnswerRequestAsync_Ping_IsAnswered()
    {
        (McpHttpSession session, _) = await NewSessionAsync();

        byte[]? response = await AnswerAsync(session, """{"jsonrpc":"2.0","id":"p","method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"p","result":{}}""", Encoding.UTF8.GetString(response!));
    }

    private static async Task<(McpHttpSession Session, TaskCompletionSource Started)> NewSessionAsync()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool(
            "wait", "Wait", "Wait until cancelled.", """{"type":"object"}""",
            async (_, cancellationToken) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return McpToolResult.FromText("never");
            }));
        McpServerSession core = new(registry, McpServerSession.CreateOptions("1.0", McpSessionOptions.DefaultMaximumMessageBytes));
        await core.AnswerAsync(Encoding.UTF8.GetBytes(McpHttpHarness.InitializeMessage), CancellationToken.None);
        return (new McpHttpSession("id", null, core, McpHttpHarness.Now), started);
    }

    private static Task<byte[]?> AnswerAsync(McpHttpSession session, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        return session.AnswerRequestAsync((JsonRpcRequest)JsonRpcMessageParser.Parse(bytes), bytes, CancellationToken.None);
    }

    private static Task AcceptAsync(McpHttpSession session, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        return session.AcceptAsync(JsonRpcMessageParser.Parse(bytes), bytes, CancellationToken.None);
    }
}
