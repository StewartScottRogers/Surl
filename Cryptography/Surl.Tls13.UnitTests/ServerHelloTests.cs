namespace Surl.Tls13;

[TestClass]
public sealed class ServerHelloTests
{
    [TestMethod]
    public void Write_Rfc8448Section3ServerHello_WritesItByteForByte()
    {
        var serverHello = new ServerHello(
            Rfc8448.ServerHello[6..38],
            ReadOnlyMemory<byte>.Empty,
            TlsCipherSuite.Aes128GcmSha256,
            [
                TlsExtensions.KeyShareServer(new TlsKeyShareEntry(
                    TlsNamedGroup.X25519,
                    Convert.FromHexString("c9828876112095fe66762bdbf7c672e156d6cc253b833df1dd69b1b04e751f0f"))),
                TlsExtensions.SupportedVersionsSelected(TlsExtensions.Tls13Version),
            ]);

        var message = serverHello.Write();

        CollectionAssert.AreEqual(Rfc8448.ServerHello, message);
    }

    [TestMethod]
    public void HelloRetryRequest_Rfc8448Section5_WritesItByteForByte()
    {
        var cookie = Rfc8448.Section5HelloRetryRequest[56..(56 + 0x72)];
        var helloRetryRequest = ServerHello.HelloRetryRequest(
            ReadOnlyMemory<byte>.Empty,
            TlsCipherSuite.Aes128GcmSha256,
            [
                TlsExtensions.KeyShareHelloRetryRequest(TlsNamedGroup.Secp256r1),
                TlsExtensions.Cookie(cookie),
                TlsExtensions.SupportedVersionsSelected(TlsExtensions.Tls13Version),
            ]);

        var message = helloRetryRequest.Write();

        CollectionAssert.AreEqual(Rfc8448.Section5HelloRetryRequest, message);
    }

    [TestMethod]
    public void Write_RandomNot32Bytes_ThrowsArgumentOutOfRange()
    {
        var serverHello = new ServerHello(new byte[31], ReadOnlyMemory<byte>.Empty, TlsCipherSuite.Aes128GcmSha256, []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => serverHello.Write());
    }
}
