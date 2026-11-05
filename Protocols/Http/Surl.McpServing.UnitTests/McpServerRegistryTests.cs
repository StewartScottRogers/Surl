namespace Surl.McpServing;

[TestClass]
public sealed class McpServerRegistryTests
{
    [TestMethod]
    public void AddTool_DuplicateName_IsRefusedAndTheFirstStays()
    {
        McpServerRegistry registry = new();
        McpTool first = Tool("start_server", """{"type":"object"}""");
        registry.AddTool(first);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => registry.AddTool(Tool("start_server", """{"type":"object"}""")));

        StringAssert.StartsWith(exception.Message, "A tool named start_server is already registered.");
        Assert.AreSame(first, registry.Tools.Single());
    }

    [TestMethod]
    [DataRow("""{"type":"array"}""")]
    [DataRow("""{"properties":{}}""")]
    [DataRow("""[]""")]
    [DataRow("""not json""")]
    [DataRow("""{"type":"object","anyOf":[]}""")]
    public void AddTool_SchemaThatIsNotACheckableObjectSchema_IsRefusedAndNothingIsAdded(string schema)
    {
        McpServerRegistry registry = new();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => registry.AddTool(Tool("t", schema)));

        StringAssert.StartsWith(exception.Message, "The tool t has an input schema the core cannot check: ");
        Assert.AreEqual("tool", exception.ParamName);
        Assert.IsEmpty(registry.Tools);
    }

    [TestMethod]
    public void Tools_SeveralAdded_AreInTheOrderAdded()
    {
        McpServerRegistry registry = new();

        registry.AddTool(Tool("b", """{"type":"object"}"""));
        registry.AddTool(Tool("a", """{"type":"object"}"""));

        CollectionAssert.AreEqual(new[] { "b", "a" }, registry.Tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void AddTool_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpServerRegistry().AddTool(null!));
    }

    private static McpTool Tool(string name, string schema) =>
        new(name, null, "d", schema, (_, _) => Task.FromResult(McpToolResult.FromText(name)));
}
