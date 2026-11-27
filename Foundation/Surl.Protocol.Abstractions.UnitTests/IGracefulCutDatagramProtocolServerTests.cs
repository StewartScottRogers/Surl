namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class IGracefulCutDatagramProtocolServerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ClosesCutFlowsGracefully_NotOverridden_IsTrueForEveryListenUrl(bool isImplicitTls)
    {
        IGracefulCutDatagramProtocolServer server = new ServerOfOneKindOfFlow();

        Assert.IsTrue(server.ClosesCutFlowsGracefully(new ListenUrl("https", "127.0.0.1", 443) { IsImplicitTls = isImplicitTls }));
    }

    private sealed class ServerOfOneKindOfFlow : IGracefulCutDatagramProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["https"]);

        public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) => Task.CompletedTask;
    }
}
