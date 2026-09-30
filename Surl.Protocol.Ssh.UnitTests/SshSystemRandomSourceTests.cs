namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshSystemRandomSourceTests
{
    [TestMethod]
    public void Fill_Destination_IsFilledWithRandomBytes()
    {
        var destination = new byte[64];

        new SshSystemRandomSource().Fill(destination);

        Assert.IsTrue(destination.Any(value => value != 0));
    }
}
