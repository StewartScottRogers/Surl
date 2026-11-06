using System.Security.Cryptography;

namespace Surl.Tls13;

[TestClass]
public sealed class TlsKeyScheduleTests
{
    [TestMethod]
    public void EarlySecret_Rfc8448Section3_IsItsEarlySecret()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);

        CollectionAssert.AreEqual(Rfc8448.EarlySecret, schedule.EarlySecret);
    }

    [TestMethod]
    public void DeriveHandshakeTrafficSecrets_Rfc8448Section3_ProducesItsSecrets()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);

        var secrets = schedule.DeriveHandshakeTrafficSecrets(Rfc8448.SharedSecret, Rfc8448.HelloTranscriptHash);

        CollectionAssert.AreEqual(Rfc8448.HandshakeSecret, schedule.HandshakeSecret);
        CollectionAssert.AreEqual(Rfc8448.MasterSecret, schedule.MasterSecret);
        CollectionAssert.AreEqual(Rfc8448.ClientHandshakeTrafficSecret, secrets.Client);
        CollectionAssert.AreEqual(Rfc8448.ServerHandshakeTrafficSecret, secrets.Server);
    }

    [TestMethod]
    public void DeriveApplicationTrafficSecrets_Rfc8448Section3_ProducesItsSecrets()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);
        schedule.DeriveHandshakeTrafficSecrets(Rfc8448.SharedSecret, Rfc8448.HelloTranscriptHash);

        var secrets = schedule.DeriveApplicationTrafficSecrets(Rfc8448.ServerFinishedTranscriptHash);

        CollectionAssert.AreEqual(Rfc8448.ClientApplicationTrafficSecret, secrets.Client);
        CollectionAssert.AreEqual(Rfc8448.ServerApplicationTrafficSecret, secrets.Server);
    }

    [TestMethod]
    public void FinishedKey_Rfc8448Section3HandshakeTrafficSecrets_ProducesItsFinishedKeys()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);

        var serverKey = schedule.FinishedKey(Rfc8448.ServerHandshakeTrafficSecret);
        var clientKey = schedule.FinishedKey(Rfc8448.ClientHandshakeTrafficSecret);

        CollectionAssert.AreEqual(Rfc8448.ServerFinishedKey, serverKey);
        CollectionAssert.AreEqual(Rfc8448.ClientFinishedKey, clientKey);
    }

    [TestMethod]
    public void ComputeFinishedVerifyData_Rfc8448Section3Transcripts_ProducesBothFinishedMessages()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);
        using var transcript = new TlsTranscript(HashAlgorithmName.SHA256);
        foreach (var message in new[]
        {
            Rfc8448.ClientHello, Rfc8448.ServerHello, Rfc8448.EncryptedExtensions, Rfc8448.Certificate, Rfc8448.CertificateVerify,
        })
        {
            transcript.Append(message);
        }

        var serverVerifyData = schedule.ComputeFinishedVerifyData(Rfc8448.ServerHandshakeTrafficSecret, transcript.CurrentHash());
        transcript.Append(Rfc8448.ServerFinished);
        var clientVerifyData = schedule.ComputeFinishedVerifyData(Rfc8448.ClientHandshakeTrafficSecret, transcript.CurrentHash());

        CollectionAssert.AreEqual(Rfc8448.ServerVerifyData, serverVerifyData);
        CollectionAssert.AreEqual(Rfc8448.ClientVerifyData, clientVerifyData);
        CollectionAssert.AreEqual(Rfc8448.ClientFinished[4..], clientVerifyData);
    }

    [TestMethod]
    public void ExpandLabel_Rfc8448Section3ServerHandshakeTrafficSecret_ProducesItsKeyAndIv()
    {
        var key = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, Rfc8448.ServerHandshakeTrafficSecret, "key", [], 16);
        var iv = TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, Rfc8448.ServerHandshakeTrafficSecret, "iv", [], 12);

        CollectionAssert.AreEqual(Rfc8448.ServerHandshakeKey, key);
        CollectionAssert.AreEqual(Rfc8448.ServerHandshakeIv, iv);
    }

    [TestMethod]
    [DataRow(TlsCipherSuite.Aes128GcmSha256, "SHA256", 32)]
    [DataRow(TlsCipherSuite.ChaCha20Poly1305Sha256, "SHA256", 32)]
    [DataRow(TlsCipherSuite.Aes256GcmSha384, "SHA384", 48)]
    public void Constructor_CipherSuite_TakesItsHash(TlsCipherSuite cipherSuite, string hashName, int hashLength)
    {
        var schedule = new TlsKeySchedule(cipherSuite);

        Assert.AreEqual(hashName, schedule.HashAlgorithm.Name);
        Assert.AreEqual(hashLength, schedule.HashLength);
        Assert.HasCount(hashLength, schedule.EarlySecret);
    }

    [TestMethod]
    public void Constructor_NotATls13CipherSuite_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TlsKeySchedule((TlsCipherSuite)0x00FF));
    }

    [TestMethod]
    public void HandshakeSecret_BeforeTheHandshakeSecretsAreDerived_ThrowsInvalidOperation()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);

        Assert.ThrowsExactly<InvalidOperationException>(() => schedule.HandshakeSecret);
    }

    [TestMethod]
    public void DeriveApplicationTrafficSecrets_BeforeTheHandshakeSecretsAreDerived_ThrowsInvalidOperation()
    {
        var schedule = new TlsKeySchedule(TlsCipherSuite.Aes128GcmSha256);

        Assert.ThrowsExactly<InvalidOperationException>(() => schedule.DeriveApplicationTrafficSecrets(new byte[32]));
    }
}
