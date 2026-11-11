using Surl.Networking;

namespace Surl.Api;

[TestClass]
public sealed class SurlServerHostSeamsTests
{
    [TestMethod]
    public void Production_CreateListenerFactory_IsTheSocketListenerFactory()
    {
        var factory = SurlServerHostSeams.Production.CreateListenerFactory(null, TimeProvider.System);

        Assert.IsInstanceOfType<SocketListenerFactory>(factory);
        Assert.IsNull(SurlServerHostSeams.Production.DataDirectoryFileSystem);
    }
}
