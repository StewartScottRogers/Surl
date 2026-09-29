using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

[TestClass]
public sealed class ServerCertificateImportTests
{
    [TestMethod]
    [DataRow(true, X509KeyStorageFlags.EphemeralKeySet)]
    [DataRow(false, X509KeyStorageFlags.DefaultKeySet)]
    public void KeyStorageFlagsFor_ByPlatform_IsTheOneThePlatformCanServe(bool isLinux, X509KeyStorageFlags expected) =>
        Assert.AreEqual(expected, ServerCertificateImport.KeyStorageFlagsFor(isLinux));

    [TestMethod]
    public void ReimportForServing_KeepsTheCertificateAndKey()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();

        using var reimported = ServerCertificateImport.ReimportForServing(certificate, OperatingSystem.IsLinux());

        Assert.AreEqual(certificate.Thumbprint, reimported.Thumbprint);
        Assert.IsTrue(reimported.HasPrivateKey);
    }
}
