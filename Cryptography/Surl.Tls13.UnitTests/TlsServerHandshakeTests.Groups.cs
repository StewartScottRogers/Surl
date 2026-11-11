using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>
/// The groups a pinned upstream curl offers only when <c>--curves</c> names one alone (ADR-0147),
/// served after ADR-0082 decision 3's five: ffdhe2048 and ffdhe3072. The recorded ClientHellos are
/// BL-545's, from the static OpenSSL builds on Windows and Linux.
/// </summary>
public sealed partial class TlsServerHandshakeTests
{
    [TestMethod]
    [DataRow(0x0100)]
    [DataRow(0x0101)]
    public void Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello(int group)
    {
        using var handshake = EcdsaHandshake();
        var client = ClientFor((ushort)group);
        var clientHello1 = new UnitTestClientHelloBuilder { SupportedGroups = [(ushort)group], KeyShares = [] }.Write();
        var clientHello2 = new UnitTestClientHelloBuilder
        {
            SupportedGroups = [(ushort)group],
            KeyShares = [((ushort)group, client.PublicShare)],
        }.Write();

        var retry = handshake.Receive(TlsEncryptionLevel.Initial, clientHello1);
        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello2);
        var verified = client.VerifyServerFlight(
            [UnitTestTlsClient.MessageHash(HashAlgorithmName.SHA256, clientHello1), retry.Initial, clientHello2],
            output.Initial,
            output.Handshake);
        handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        CollectionAssert.AreEqual(Convert.FromHexString($"00330002{group:X4}"), retry.Initial[44..50]);
        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual((TlsNamedGroup)group, handshake.KeyExchangeGroup);
        CollectionAssert.AreEqual(verified.ApplicationSecrets.Client, output.ApplicationSecrets!.Client);
    }

    [TestMethod]
    [DataRow("static-8.21.0-win-curves-ffdhe2048.bin", 0x0100, 256, 256)]
    [DataRow("static-8.21.0-win-curves-ffdhe3072.bin", 0x0101, 384, 384)]
    [DataRow("static-8.21.0-linux-curves-ffdhe2048.bin", 0x0100, 256, 256)]
    [DataRow("static-8.21.0-linux-curves-ffdhe3072.bin", 0x0101, 384, 384)]
    public void Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry(
        string fixture, int group, int clientShareLength, int serverShareLength)
    {
        using var handshake = EcdsaHandshake();
        var recorded = UnitTestFixtures.Read(fixture);
        var recordedHello = ClientHello.Read(recorded);
        var recordedShare = recordedHello.KeyShares!.Single();
        var client = ClientFor((ushort)group);
        var clientHello = client.WithShareReplaced(recorded, recordedShare.KeyExchange.Span);

        var output = handshake.Receive(TlsEncryptionLevel.Initial, clientHello);
        var verified = client.VerifyServerFlight([clientHello], output.Initial, output.Handshake);
        handshake.Receive(TlsEncryptionLevel.Handshake, verified.ClientFinished);

        CollectionAssert.AreEqual(new[] { (TlsNamedGroup)group }, recordedHello.SupportedGroups!.ToArray());
        Assert.AreEqual((TlsNamedGroup)group, recordedShare.Group);
        Assert.AreEqual(clientShareLength, recordedShare.KeyExchange.Length);
        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual((TlsNamedGroup)group, handshake.KeyExchangeGroup);
        Assert.HasCount(serverShareLength, client.ServerShare);
    }

    [TestMethod]
    [DataRow(0x0100, 255)]
    [DataRow(0x0101, 385)]
    public void Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter(int group, int length)
    {
        AssertShareRefused((ushort)group, new byte[length]);
    }

    [TestMethod]
    [DataRow(0x0100, 0)]
    [DataRow(0x0100, 1)]
    [DataRow(0x0100, -1)]
    [DataRow(0x0100, -2)]
    [DataRow(0x0101, 1)]
    [DataRow(0x0101, -1)]
    [DataRow(0x0101, -2)]
    public void Receive_FiniteFieldShareOutsideOneToPMinusOne_FailsWithIllegalParameter(int group, int offsetFromZeroOrP)
    {
        var prime = UnitTestFiniteFieldPrimes.Of((ushort)group);
        var value = offsetFromZeroOrP >= 0 ? new BigInteger(offsetFromZeroOrP) : prime + offsetFromZeroOrP + 1;
        var length = (int)((prime.GetBitLength() + 7) / 8);
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        AssertShareRefused((ushort)group, [.. new byte[length - bytes.Length], .. bytes]);
    }

    private static UnitTestTlsClient ClientFor(ushort group) => UnitTestTlsClient.WithFiniteFieldShare(group);

    private static void AssertShareRefused(ushort group, byte[] share)
    {
        using var handshake = EcdsaHandshake();
        var clientHello = new UnitTestClientHelloBuilder { SupportedGroups = [group], KeyShares = [(group, share)] }.Write();

        AssertAlert(TlsAlert.IllegalParameter, () => handshake.Receive(TlsEncryptionLevel.Initial, clientHello));
    }
}
