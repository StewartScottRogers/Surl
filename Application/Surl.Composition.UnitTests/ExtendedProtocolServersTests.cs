using Surl.Content;
using Surl.Extended.Protocol.S3;

namespace Surl.Composition;

[TestClass]
public sealed class ExtendedProtocolServersTests
{
    [TestMethod]
    public void AreTurnedOn_SwitchUnset_IsTrue()
    {
        Assert.IsTrue(ExtendedProtocolServers.AreTurnedOn(FakeEnvironmentVariableReader.Empty));
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("1")]
    [DataRow("false", DisplayName = "Only 0 turns them off")]
    [DataRow(" 0", DisplayName = "0 with a space is another value")]
    public void AreTurnedOn_SwitchAtAnotherValue_IsTrue(string value)
    {
        var environment = new FakeEnvironmentVariableReader(new Dictionary<string, string> { ["SURL_EXTENSIONS"] = value });

        Assert.IsTrue(ExtendedProtocolServers.AreTurnedOn(environment));
    }

    [TestMethod]
    public void AreTurnedOn_SwitchAtZero_IsFalse()
    {
        var environment = new FakeEnvironmentVariableReader(new Dictionary<string, string> { ["SURL_EXTENSIONS"] = "0" });

        Assert.IsFalse(ExtendedProtocolServers.AreTurnedOn(environment));
    }

    [TestMethod]
    public void Compose_ThisBuild_IsTheS3ServerForS3Alone()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var servers = ExtendedProtocolServers.Compose(services);

        Assert.IsInstanceOfType<S3ProtocolServer>(servers.Single());
        CollectionAssert.AreEqual(new[] { "s3" }, servers.SelectMany(server => server.Schemes).ToArray());
        CollectionAssert.AreEqual(new[] { "s3" }, ExtendedProtocolServers.Registrations.SelectMany(registration => registration.Schemes).ToArray());
    }

    [TestMethod]
    public void Compose_ThisBuild_BuildsTheS3ServerOverTheServicesContentStoreAndIndex()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var server = ExtendedProtocolServers.Compose(services).Single();

        Assert.AreSame(services.ComposedServices.ContentStore, ComposedServerFields.Single<ContentStore>(server));
        Assert.AreSame(services.ComposedServices.ObjectMetadataIndex, ComposedServerFields.Single<ObjectMetadataIndex>(server));
    }

    [TestMethod]
    public void Compose_NullServices_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtendedProtocolServers.Compose(null!));
    }
}
