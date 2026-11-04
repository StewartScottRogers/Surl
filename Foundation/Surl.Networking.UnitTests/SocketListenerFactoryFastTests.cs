using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The parts of <see cref="SocketListenerFactory"/> that open no socket; the rest is in
/// <see cref="SocketListenerFactoryTests"/> (Integration).
/// </summary>
[TestClass]
public sealed class SocketListenerFactoryFastTests
{
    [TestMethod]
    public void Constructors_WithAndWithoutTlsSettings_MakeAListenerFactory()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));

        Assert.IsInstanceOfType<IListenerFactory>(new SocketListenerFactory());
        Assert.IsInstanceOfType<IListenerFactory>(new SocketListenerFactory(settings));
    }
}
