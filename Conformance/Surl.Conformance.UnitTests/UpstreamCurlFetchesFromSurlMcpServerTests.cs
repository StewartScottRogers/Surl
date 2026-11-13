using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Surl.Conformance;

/// <summary>
/// BL-632's proof: the built <c>surl-mcp</c>, started as a child process and spoken to over standard input and
/// output, starts an HTTP server on loopback port 0, pinned upstream curl fetches a file from the URL the start
/// tool returned, and <c>get_exchanges</c> shows the request curl sent. Inconclusive where no pinned build is
/// installed. The MCP client is a small newline-delimited JSON-RPC client written for this class.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlMcpServerTests
{
    private static readonly byte[] Hello = "hello from surl mcp\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartServer_UpstreamCurlGet_ExitsZeroWithTheFilesBytesAndTheExchangeShowsTheRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), "surl-mcp-" + Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDirectory);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dataDirectory, "hello.txt"), Hello, TestContext.CancellationToken);
            await RunSessionAsync(root, dataDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task RunSessionAsync(string root, string dataDirectory)
    {
        using var mcp = McpStdioProcess.Start(root);
        var cancellationToken = TestContext.CancellationToken;

        var initialized = await mcp.CallAsync("initialize", """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}""", cancellationToken);
        Assert.AreEqual("surl-mcp", initialized.GetProperty("serverInfo").GetProperty("name").GetString());
        await mcp.NotifyAsync("notifications/initialized", cancellationToken);

        var arguments = JsonSerializer.Serialize(new
        {
            listen_urls = new[] { "http://127.0.0.1:0" },
            arguments = new[] { "--directory", dataDirectory },
        });
        var started = await mcp.CallToolAsync("start_server", arguments, cancellationToken);
        var serverId = started.GetProperty("server_id").GetString()!;
        var boundUrl = new Uri(started.GetProperty("bound_urls")[0].GetString()!);
        Assert.AreNotEqual(0, boundUrl.Port);

        var curl = await PinnedUpstreamCurl.RunAsync(TestContext, "-s", new Uri(boundUrl, "/hello.txt").ToString());
        Assert.AreEqual(0, curl.ExitCode, curl.StandardError);
        CollectionAssert.AreEqual(Hello, curl.StandardOutput);

        var exchanges = await mcp.CallToolAsync("get_exchanges", JsonSerializer.Serialize(new { server_id = serverId }), cancellationToken);
        var firstReceived = exchanges.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
            .First(chunk => chunk.GetProperty("direction").GetString() == "received");
        StringAssert.StartsWith(DecodeChunk(firstReceived), "GET /hello.txt HTTP/1.1");

        var stopped = await mcp.CallToolAsync("stop_server", JsonSerializer.Serialize(new { server_id = serverId }), cancellationToken);
        Assert.AreEqual(serverId, stopped.GetProperty("server_id").GetString());

        var exitCode = await mcp.CloseAsync(cancellationToken);
        Assert.AreEqual(0, exitCode, mcp.StandardError);
        Assert.IsGreaterThan(0, mcp.LinesRead);
    }

    private static string DecodeChunk(JsonElement chunk) =>
        chunk.GetProperty("encoding").GetString() == "base64"
            ? Encoding.UTF8.GetString(Convert.FromBase64String(chunk.GetProperty("data").GetString()!))
            : chunk.GetProperty("data").GetString()!;

    /// <summary>The built <c>surl-mcp</c> as a child process, driven with newline-delimited JSON-RPC.</summary>
    private sealed class McpStdioProcess : IDisposable
    {
        private readonly Process process;
        private readonly StringBuilder standardError = new();
        private int nextId = 1;

        private McpStdioProcess(Process process) => this.process = process;

        public int LinesRead { get; private set; }

        public string StandardError
        {
            get
            {
                lock (standardError)
                {
                    return standardError.ToString();
                }
            }
        }

        public static McpStdioProcess Start(string root)
        {
            var baseDirectory = AppContext.BaseDirectory;
            var testAssembly = typeof(McpStdioProcess).Assembly.GetName().Name!;
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = root,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
            };
            foreach (var argument in new[]
            {
                "exec",
                "--runtimeconfig", Path.Combine(baseDirectory, testAssembly + ".runtimeconfig.json"),
                "--depsfile", Path.Combine(baseDirectory, testAssembly + ".deps.json"),
                Path.Combine(baseDirectory, "surl-mcp.dll"),
                "--root", root,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            var child = Process.Start(startInfo) ?? throw new InvalidOperationException("surl-mcp did not start.");
            var client = new McpStdioProcess(child);
            child.ErrorDataReceived += (_, data) =>
            {
                lock (client.standardError)
                {
                    client.standardError.AppendLine(data.Data);
                }
            };
            child.BeginErrorReadLine();
            return client;
        }

        public async Task<JsonElement> CallAsync(string method, string parametersJson, CancellationToken cancellationToken)
        {
            var id = nextId++;
            await WriteLineAsync($$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}","params":{{parametersJson}}}""", cancellationToken);
            var line = await ReadLineAsync(cancellationToken);
            using var message = JsonDocument.Parse(line);
            var root = message.RootElement;
            Assert.AreEqual("2.0", root.GetProperty("jsonrpc").GetString());
            Assert.AreEqual(id, root.GetProperty("id").GetInt32());
            Assert.IsTrue(root.TryGetProperty("result", out var result), line);
            return result.Clone();
        }

        public async Task<JsonElement> CallToolAsync(string tool, string argumentsJson, CancellationToken cancellationToken)
        {
            var result = await CallAsync("tools/call", $$"""{"name":"{{tool}}","arguments":{{argumentsJson}}}""", cancellationToken);
            Assert.IsFalse(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(), result.ToString());
            return result.GetProperty("structuredContent");
        }

        public Task NotifyAsync(string method, CancellationToken cancellationToken) =>
            WriteLineAsync($$"""{"jsonrpc":"2.0","method":"{{method}}"}""", cancellationToken);

        public async Task<int> CloseAsync(CancellationToken cancellationToken)
        {
            process.StandardInput.Close();
            Assert.IsEmpty(await process.StandardOutput.ReadToEndAsync(cancellationToken), "standard output held more than the replies read");
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
        }

        private async Task WriteLineAsync(string json, CancellationToken cancellationToken)
        {
            await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }

        /// <summary>Reads one line, which must be a JSON-RPC message: nothing else may reach standard output.</summary>
        private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("surl-mcp closed its standard output. stderr: " + StandardError);
            LinesRead++;
            return line;
        }
    }
}
