using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class AzureStorageScopeTests
{
    [TestMethod]
    public void Constructor_AnyScope_CarriesWhatItWasGiven()
    {
        var scope = new AzureStorageScope("devstoreaccount1", "/photos/cat.jpg", "r", IPAddress.Loopback);

        Assert.AreEqual("devstoreaccount1", scope.AccountName);
        Assert.AreEqual("/photos/cat.jpg", scope.ResourcePath);
        Assert.AreEqual("r", scope.GrantingPermissions);
        Assert.AreSame(IPAddress.Loopback, scope.PeerAddress);
    }
}
