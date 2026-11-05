using System.Security.Cryptography;

namespace Surl.Tls13;

[TestClass]
public sealed class TlsTranscriptTests
{
    [TestMethod]
    public void CurrentHash_Rfc8448Section3ClientHelloAndServerHello_IsTheHelloTranscriptHash()
    {
        using var transcript = new TlsTranscript(HashAlgorithmName.SHA256);
        transcript.Append(Rfc8448.ClientHello);
        transcript.Append(Rfc8448.ServerHello);

        var hash = transcript.CurrentHash();

        CollectionAssert.AreEqual(Rfc8448.HelloTranscriptHash, hash);
    }

    [TestMethod]
    public void ReplaceWithMessageHash_Rfc8448Section5HelloRetryRequest_GivesItsHelloTranscriptHash()
    {
        using var transcript = new TlsTranscript(HashAlgorithmName.SHA256);
        transcript.Append(Rfc8448.Section5ClientHello1);

        transcript.ReplaceWithMessageHash();
        transcript.Append(Rfc8448.Section5HelloRetryRequest);
        transcript.Append(Rfc8448.Section5ClientHello2);
        transcript.Append(Rfc8448.Section5ServerHello);

        CollectionAssert.AreEqual(Rfc8448.Section5HelloTranscriptHash, transcript.CurrentHash());
    }
}
