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

    [TestMethod]
    public void AddResources_UriAlreadyRegistered_IsRefusedAndNoneOfThatProvidersResourcesIsAdded()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://a")));

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => registry.AddResources(new UnitTestResourceProvider(Resource("surl://b"), Resource("surl://a"))));

        StringAssert.StartsWith(exception.Message, "A resource with the URI surl://a is already registered.");
        Assert.AreEqual("provider", exception.ParamName);
        CollectionAssert.AreEqual(new[] { "surl://a" }, registry.Resources.Select(resource => resource.Uri).ToArray());
    }

    [TestMethod]
    public void AddResources_UriListedTwiceByOneProvider_IsRefusedAndNothingIsAdded()
    {
        McpServerRegistry registry = new();

        Assert.ThrowsExactly<ArgumentException>(
            () => registry.AddResources(new UnitTestResourceProvider(Resource("surl://a"), Resource("surl://a"))));

        Assert.IsEmpty(registry.Resources);
    }

    [TestMethod]
    public void AddResources_SeveralProviders_ListsInTheOrderAddedAndReadsEachProvidersListOnce()
    {
        McpServerRegistry registry = new();
        UnitTestResourceProvider first = new(Resource("surl://b"), Resource("surl://a"));

        registry.AddResources(first);
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://c")));

        CollectionAssert.AreEqual(new[] { "surl://b", "surl://a", "surl://c" }, registry.Resources.Select(resource => resource.Uri).ToArray());
        Assert.AreEqual(1, first.ResourcesReadCount);
    }

    [TestMethod]
    public void AddResources_NullProviderOrNullResource_Throws()
    {
        McpServerRegistry registry = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => registry.AddResources(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => registry.AddResources(new UnitTestResourceProvider(Resource("surl://a"), null!)));
        Assert.IsEmpty(registry.Resources);
    }

    private static McpResource Resource(string uri) => new(uri, uri, null, null, null);

    private static McpTool Tool(string name, string schema) =>
        new(name, null, "d", schema, (_, _) => Task.FromResult(McpToolResult.FromText(name)));
}
