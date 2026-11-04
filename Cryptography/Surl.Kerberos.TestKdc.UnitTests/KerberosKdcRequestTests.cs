using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosKdcRequestTests
{
    [TestMethod]
    public void Read_AsRequest_ReadsItsPaDataClientServerTillNonceAndEnctypesInOrder()
    {
        byte[] bytes = KdcClient.AsRequest([23, 18, 17], [(2, [0x01]), (149, [])], till: KdcClient.FarTill);

        KerberosKdcRequest request = KerberosKdcRequest.Read(bytes);

        Assert.IsFalse(request.IsTicketGrantingRequest);
        CollectionAssert.AreEqual(new[] { 2, 149 }, request.PreAuthenticationData.Select(paData => paData.Type).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x01 }, request.PreAuthenticationData[0].Value);
        Assert.AreEqual(new KerberosPrincipalName("SURL.TEST", ["tester"]), request.ClientName);
        Assert.AreEqual(new KerberosPrincipalName("SURL.TEST", ["krbtgt", "SURL.TEST"]), request.ServerName);
        Assert.AreEqual(KdcClient.FarTill, request.Till);
        Assert.AreEqual(KdcClient.Nonce, request.Nonce);
        CollectionAssert.AreEqual(new[] { 23, 18, 17 }, request.EncryptionTypeNumbers.ToArray());
    }

    [TestMethod]
    public void Read_TgsRequestWithoutAClientName_HasNoClientName()
    {
        byte[] bytes = KdcClient.TgsRequest([0x6e, 0x00], ["HTTP", "web.surl.test"], [18]);

        KerberosKdcRequest request = KerberosKdcRequest.Read(bytes);

        Assert.IsTrue(request.IsTicketGrantingRequest);
        Assert.IsNull(request.ClientName);
        Assert.AreEqual((1, (byte)0x6e), (request.PreAuthenticationData[0].Type, request.PreAuthenticationData[0].Value[0]));
    }

    [TestMethod]
    public void Read_AsRequestWithoutPaData_HasNoPaData()
    {
        KerberosKdcRequest request = KerberosKdcRequest.Read(KdcClient.AsRequest([18]));

        Assert.IsEmpty(request.PreAuthenticationData);
    }

    [TestMethod]
    public void Read_AnotherApplicationTag_Throws()
    {
        byte[] bytes = KdcClient.KdcRequest(11, 11, null, ["tester"], ["krbtgt", "SURL.TEST"], [18], KdcClient.FarTill);

        Assert.ThrowsExactly<AsnContentException>(() => KerberosKdcRequest.Read(bytes));
    }
}
