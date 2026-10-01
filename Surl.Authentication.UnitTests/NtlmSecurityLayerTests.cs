using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// <see cref="NtlmSecurityLayer"/> against [MS-NLMP] section 3.4 with extended session security:
/// the specification's own section 4.2.4.4 example, and the server's layer against the client's
/// mirror (<see cref="NtlmSecurityLayer.ForInitiator"/>) in both directions (ADR-0072, decision 4).
/// </summary>
[TestClass]
public sealed class NtlmSecurityLayerTests
{
    // [MS-NLMP] section 4.2.4's flags: KEY_EXCH, 56, 128, VERSION, TARGET_INFO, ESS,
    // TARGET_TYPE_SERVER, ALWAYS_SIGN, NTLM, SEAL, SIGN, OEM, UNICODE.
    private const NtlmNegotiateFlags SpecificationFlags = (NtlmNegotiateFlags)0xE28A8233;

    private const NtlmNegotiateFlags Sealed =
        NtlmNegotiateFlags.Seal | NtlmNegotiateFlags.Sign | NtlmNegotiateFlags.ExtendedSessionSecurity
        | NtlmNegotiateFlags.KeyExchange | NtlmNegotiateFlags.Negotiate128;

    private static readonly byte[] SpecificationSessionKey = Convert.FromHexString("55555555555555555555555555555555");

    private static readonly byte[] Message = Encoding.ASCII.GetBytes("a message to protect");

    private static (NtlmSecurityLayer Server, NtlmSecurityLayer Client) Layers(NtlmNegotiateFlags flags)
    {
        var sessionKey = new NtlmSessionKey(SpecificationSessionKey, flags);

        return (NtlmSecurityLayer.ForAcceptor(sessionKey), NtlmSecurityLayer.ForInitiator(sessionKey));
    }

    private static uint SequenceNumberOf(byte[] buffer) => BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12));

    [TestMethod]
    public void Protect_SpecificationExample_IsSection4244sSealedMessageAndSignature()
    {
        var client = NtlmSecurityLayer.ForInitiator(new NtlmSessionKey(SpecificationSessionKey, SpecificationFlags));

        var buffer = client.Protect(Encoding.Unicode.GetBytes("Plaintext"));

        CollectionAssert.AreEqual(
            Convert.FromHexString("010000007FB38EC5C55D497600000000" + "54E50165BF1936DC996020C1811B0F06FB5F"), buffer);
    }

    [TestMethod]
    public void ProtectThenTryUnprotect_Sealed_RoundTripsBothDirectionsWithSequenceNumbersFromZero()
    {
        var (server, client) = Layers(Sealed);

        for (uint sequenceNumber = 0; sequenceNumber < 3; sequenceNumber++)
        {
            var toClient = server.Protect(Message);
            var toServer = client.Protect(Message);

            Assert.AreEqual(sequenceNumber, SequenceNumberOf(toClient));
            Assert.AreEqual(sequenceNumber, SequenceNumberOf(toServer));
            CollectionAssert.AreNotEqual(Message, toClient[NtlmSecurityLayer.SignatureLength..]);
            Assert.IsTrue(client.TryUnprotect(toClient, out var atClient));
            CollectionAssert.AreEqual(Message, atClient);
            Assert.IsTrue(server.TryUnprotect(toServer, out var atServer));
            CollectionAssert.AreEqual(Message, atServer);
        }
    }

    [TestMethod]
    public void Protect_EachDirection_UsesItsOwnKeys()
    {
        var (server, client) = Layers(Sealed);

        CollectionAssert.AreNotEqual(server.Protect(Message), client.Protect(Message));
    }

    [TestMethod]
    public void ProtectThenTryUnprotect_SigningOnly_SendsTheMessageInClear()
    {
        var (server, client) = Layers(NtlmNegotiateFlags.Sign | NtlmNegotiateFlags.ExtendedSessionSecurity | NtlmNegotiateFlags.KeyExchange);

        var toClient = server.Protect(Message);

        CollectionAssert.AreEqual(Message, toClient[NtlmSecurityLayer.SignatureLength..]);
        Assert.IsTrue(client.TryUnprotect(toClient, out var message));
        CollectionAssert.AreEqual(Message, message);
    }

    [TestMethod]
    public void Protect_WithoutKeyExchange_SendsTheChecksumUnencrypted()
    {
        var flags = NtlmNegotiateFlags.Sign | NtlmNegotiateFlags.ExtendedSessionSecurity;
        var (server, client) = Layers(flags);

        var toClient = server.Protect(Message);

        var signingKey = MD5.HashData([.. SpecificationSessionKey, .. Encoding.ASCII.GetBytes("session key to server-to-client signing key magic constant\0")]);
        byte[] signed = [0, 0, 0, 0, .. Message];
        var checksum = HMACMD5.HashData(signingKey, signed).AsSpan(0, 8).ToArray();
        CollectionAssert.AreEqual(checksum, toClient[4..12]);
        Assert.IsTrue(client.TryUnprotect(toClient, out _));
    }

    [TestMethod]
    [DataRow(0x20000000u)]
    [DataRow(0x80000000u)]
    [DataRow(0u)]
    public void ProtectThenTryUnprotect_EachSealingKeyLength_RoundTrips(uint keyLength)
    {
        var (server, client) = Layers(NtlmNegotiateFlags.Seal | NtlmNegotiateFlags.ExtendedSessionSecurity | (NtlmNegotiateFlags)keyLength);

        Assert.IsTrue(client.TryUnprotect(server.Protect(Message), out var message));
        CollectionAssert.AreEqual(Message, message);
    }

    [TestMethod]
    public void Protect_ShorterSealingKeys_SealDifferently()
    {
        var sealedWith = new[] { NtlmNegotiateFlags.Negotiate128, NtlmNegotiateFlags.Negotiate56, NtlmNegotiateFlags.None }
            .Select(keyLength => Convert.ToHexString(Layers(NtlmNegotiateFlags.Seal | NtlmNegotiateFlags.ExtendedSessionSecurity | keyLength).Server.Protect(Message)))
            .ToList();

        Assert.HasCount(3, sealedWith.Distinct());
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(12)]
    [DataRow(20)]
    public void TryUnprotect_TamperedByte_IsRefused(int index)
    {
        var (server, client) = Layers(Sealed);
        var buffer = client.Protect(Message);
        buffer[index] ^= 1;

        Assert.IsFalse(server.TryUnprotect(buffer, out var message));
        Assert.IsEmpty(message);
    }

    [TestMethod]
    public void TryUnprotect_ReplayedSequenceNumber_IsRefused()
    {
        var (server, client) = Layers(NtlmNegotiateFlags.Sign | NtlmNegotiateFlags.ExtendedSessionSecurity);
        var buffer = client.Protect(Message);
        Assert.IsTrue(server.TryUnprotect(buffer, out _));

        Assert.IsFalse(server.TryUnprotect(buffer, out _));
    }

    [TestMethod]
    public void TryUnprotect_ShorterThanASignature_IsRefused()
    {
        var (server, _) = Layers(Sealed);

        Assert.IsFalse(server.TryUnprotect(new byte[NtlmSecurityLayer.SignatureLength - 1], out var message));
        Assert.IsEmpty(message);
    }

    [TestMethod]
    public void MaximumProtectedBytes_IsUnboundedByNtlm()
    {
        Assert.AreEqual(int.MaxValue, Layers(Sealed).Server.MaximumProtectedBytes);
    }

    [TestMethod]
    [DataRow(0x00000020u, true)]
    [DataRow(0x00000010u, true)]
    [DataRow(0x40080000u, false)]
    public void IsNegotiated_IsSealingOrSigning(uint flags, bool expected)
    {
        Assert.AreEqual(expected, NtlmSecurityLayer.IsNegotiated((NtlmNegotiateFlags)flags));
    }
}
