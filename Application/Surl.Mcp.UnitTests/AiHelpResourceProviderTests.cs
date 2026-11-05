using Surl.Cli;

namespace Surl.Mcp;

[TestClass]
public sealed class AiHelpResourceProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Resources_EveryAiHelpTopic_HasAResourceNamedForItsSurlAiHelpCommand()
    {
        var provider = new AiHelpResourceProvider();

        foreach (var topic in AiHelpTopics.All)
        {
            var resource = provider.Resources.SingleOrDefault(candidate => candidate.Uri == $"surl://aihelp/{topic.Name}");
            Assert.IsNotNull(resource, $"The --aihelp topic {topic.Name} has no surl://aihelp resource.");
            Assert.AreEqual($"surl --aihelp {topic.Name}", resource.Name);
            Assert.AreEqual(topic.Description, resource.Title);
            Assert.AreEqual(AiHelpResourceProvider.MimeType, resource.MimeType);
        }
    }

    [TestMethod]
    public void Resources_BesideTheTopics_HoldOnlyTheOverviewFirstAndAllLast()
    {
        var provider = new AiHelpResourceProvider();

        Assert.HasCount(AiHelpTopics.All.Count + 2, provider.Resources);
        Assert.AreEqual("surl://aihelp", provider.Resources[0].Uri);
        Assert.AreEqual("surl --aihelp", provider.Resources[0].Name);
        Assert.AreEqual("surl://aihelp/all", provider.Resources[^1].Uri);
        Assert.AreEqual("surl --aihelp all", provider.Resources[^1].Name);
    }

    [TestMethod]
    public async Task ReadTextAsync_TheOverviewUri_ReturnsTheAnswerForNoTopic()
    {
        var provider = new AiHelpResourceProvider();

        var text = await provider.ReadTextAsync("surl://aihelp", TestContext.CancellationToken);

        Assert.AreEqual(AiHelpText.Answer(null).Output, text);
    }

    [TestMethod]
    public async Task ReadTextAsync_AUriNoResourceHas_ReturnsNull()
    {
        var provider = new AiHelpResourceProvider();

        var text = await provider.ReadTextAsync("surl://aihelp/no-such-topic", TestContext.CancellationToken);

        Assert.IsNull(text);
    }
}
