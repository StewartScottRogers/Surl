using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// What the exchange log holds of a Kafka client's SASL PLAIN login (ADR-0182, BL-1462): the password is
/// <c>****</c>, the names and every other byte are as received, however the bytes are chunked.
/// </summary>
[TestClass]
public sealed class KafkaExchangeLogRedactionTests
{
    private const string Plain = "\0alice\0s3cret";
    private const string PlainLogged = "\0alice\0****";

    private static readonly byte[] PlainHandshakeV1 = Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN"));
    private static readonly byte[] PlainHandshakeV0 = Request(KafkaApiKey.SaslHandshake, 0, String("PLAIN"));
    private static readonly byte[] ScramHandshake = Request(KafkaApiKey.SaslHandshake, 1, String("SCRAM-SHA-256"));
    private static readonly byte[] Metadata = Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]);

    private static IReceivedBytesRedactor NewRedactor() =>
        new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(false, "PLAIN"), []).CreateRedactor(new ExchangeRedactionContext(1, "kafka"));

    private static byte[] Logged(params byte[][] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    // The request as the client sent it, but its auth_bytes replaced by what the log holds.
    private static byte[] LoggedAuthenticate(short version, string authBytesLogged)
    {
        var original = Request(KafkaApiKey.SaslAuthenticate, version, Bytes(Plain));
        var withoutAuthBytes = original[..^Plain.Length];
        return [.. withoutAuthBytes, .. Encoding.UTF8.GetBytes(authBytesLogged)];
    }

    private static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static void AssertSameAtEverySplit(byte[] expected, byte[] sent)
    {
        for (var split = 0; split <= sent.Length; split++)
        {
            CollectionAssert.AreEqual(expected, Logged(sent[..split], sent[split..]), $"split at {split}");
        }

        CollectionAssert.AreEqual(expected, Logged([.. sent.Select(next => new[] { next })]), "one byte at a time");
    }

    [TestMethod]
    public void PlainOverV1_MasksOnlyThePassword_WhateverTheChunks()
    {
        var sent = Join(PlainHandshakeV1, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain)), Metadata);
        var expected = Join(PlainHandshakeV1, LoggedAuthenticate(1, PlainLogged), Metadata);

        CollectionAssert.AreEqual(expected, Logged(sent));
        AssertSameAtEverySplit(expected, sent);
    }

    [TestMethod]
    public void PlainOverV0_MasksTheRawTokenAndThenLogsAsReceived()
    {
        byte[] token = [.. Int32(Plain.Length), .. Encoding.UTF8.GetBytes(Plain)];
        var sent = Join(PlainHandshakeV0, token, Metadata);
        var expected = Join(PlainHandshakeV0, Int32(Plain.Length), Encoding.UTF8.GetBytes(PlainLogged), Metadata);

        CollectionAssert.AreEqual(expected, Logged(sent));
        AssertSameAtEverySplit(expected, sent);
    }

    [TestMethod]
    public void Scram_IsLoggedAsReceived()
    {
        var sent = Join(ScramHandshake, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes("n,,n=alice,r=0")), Metadata);

        AssertSameAtEverySplit(sent, sent);
    }

    [TestMethod]
    public void AuthenticateBeforeAnyPlainHandshake_IsLoggedAsReceived()
    {
        var sent = Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain));

        AssertSameAtEverySplit(sent, sent);
    }

    [TestMethod]
    public void ASecondHandshakeNamingAnotherMechanism_EndsTheMasking()
    {
        var sent = Join(PlainHandshakeV1, ScramHandshake, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain)));

        CollectionAssert.AreEqual(sent, Logged(sent));
    }

    [TestMethod]
    public void AHandshakeTheBrokerCannotRead_StartsNoLogin()
    {
        var unreadable = Request(KafkaApiKey.SaslHandshake, 1);
        var sent = Join(unreadable, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain)));

        CollectionAssert.AreEqual(sent, Logged(sent));
    }

    [TestMethod]
    public void ARequestOfNoBodyBytes_AndOneShorterThanItsHeader_AreLoggedAsReceived()
    {
        byte[] sent = [0, 0, 0, 0, .. Int32(2), 0x00, 0x11, .. Int32(4), 0x00, 0x11, 0x00, 0x01];

        AssertSameAtEverySplit(sent, sent);
    }

    [TestMethod]
    public void ARequestWhoseBodyEndsAtItsApiKeyAndVersion_IsDecidedAndCompleted()
    {
        byte[] handshake = [.. Int32(4), 0x00, 0x11, 0x00, 0x01];
        byte[] authenticate = [.. Int32(4), 0x00, 0x24, 0x00, 0x01];

        CollectionAssert.AreEqual(handshake, Logged(handshake));
        CollectionAssert.AreEqual(Join(PlainHandshakeV1, Int32(4), "****"u8.ToArray()), Logged(PlainHandshakeV1, authenticate));
    }

    [TestMethod]
    public void APlainMessageWithoutTwoNuls_IsMaskedWhole()
    {
        var sent = Join(PlainHandshakeV1, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes("only-a-password")));
        var expected = Join(PlainHandshakeV1, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes("only-a-password"))[..^"only-a-password".Length], "****"u8.ToArray());

        CollectionAssert.AreEqual(expected, Logged(sent));
    }

    [TestMethod]
    public void AnAuthenticateWhoseAuthBytesAreNull_IsMaskedFromItsHeader()
    {
        var authenticate = Request(KafkaApiKey.SaslAuthenticate, 1, Int32(-1));
        var expected = Join(PlainHandshakeV1, authenticate[..4], "****"u8.ToArray());

        CollectionAssert.AreEqual(expected, Logged(PlainHandshakeV1, authenticate));
    }

    [TestMethod]
    public void APlainRequestOverTheLimit_IsMaskedAndDroppedAndTheNextRequestIsAsReceived()
    {
        var huge = Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(new string('p', KafkaSaslPlainBytesRedactor.MaxHeldBytes + 10)));
        var sent = Join(PlainHandshakeV1, huge, Metadata);
        var expected = Join(PlainHandshakeV1, "****"u8.ToArray(), Metadata);

        CollectionAssert.AreEqual(expected, Logged(sent));
        CollectionAssert.AreEqual(expected, Logged(sent[..(PlainHandshakeV1.Length + 100)], sent[(PlainHandshakeV1.Length + 100)..]));
    }

    [TestMethod]
    public void AHandshakeOverTheLimit_IsLoggedAsReceived()
    {
        var huge = Request(KafkaApiKey.SaslHandshake, 1, new byte[KafkaSaslPlainBytesRedactor.MaxHeldBytes + 10]);
        var sent = Join(huge, Metadata);

        CollectionAssert.AreEqual(sent, Logged(sent));
    }

    [TestMethod]
    public void ARequestCutOffAtFlush_IsMaskedWhenSecretAndAsReceivedWhenNot()
    {
        var authenticate = Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain));
        var cutAuthenticate = Join(PlainHandshakeV1, authenticate[..20]);
        var cutHandshake = PlainHandshakeV1[..10];
        var cutToken = Join(PlainHandshakeV0, Int32(Plain.Length), "\0al"u8.ToArray());

        CollectionAssert.AreEqual(Join(PlainHandshakeV1, "****"u8.ToArray()), Logged(cutAuthenticate));
        CollectionAssert.AreEqual(cutHandshake, Logged(cutHandshake));
        CollectionAssert.AreEqual(Join(PlainHandshakeV0, "****"u8.ToArray()), Logged(cutToken));
        CollectionAssert.AreEqual(Array.Empty<byte>(), Logged());
    }

    [TestMethod]
    public void FlushInTheMiddleOfADroppedRequest_ForgetsIt()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(PlainHandshakeV1, output);
        redactor.Redact(Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(new string('p', KafkaSaslPlainBytesRedactor.MaxHeldBytes + 10)))[..(KafkaSaslPlainBytesRedactor.MaxHeldBytes + 5)], output);
        redactor.Flush(output);
        redactor.Redact(Metadata, output);

        CollectionAssert.AreEqual(Join(PlainHandshakeV1, "****"u8.ToArray(), Metadata), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void ObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        redactor.ObserveSent(Response([0, 0, 0, 0, 0, 0]));
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Join(PlainHandshakeV1, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain))), output);

        CollectionAssert.AreEqual(Join(PlainHandshakeV1, LoggedAuthenticate(1, PlainLogged)), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void NullOutput_Throws()
    {
        var redactor = NewRedactor();
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("x"u8, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    [TestMethod]
    public void TheBrokerRawBytesStillReachTheServer_OnlyTheLogIsRedacted()
    {
        var redactor = NewRedactor();
        var sent = Join(PlainHandshakeV1, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes(Plain)));
        var input = sent.ToArray();
        redactor.Redact(input, new ArrayBufferWriter<byte>());

        CollectionAssert.AreEqual(sent, input);
        Assert.IsTrue(Encoding.UTF8.GetString(Logged(sent)).Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(Encoding.UTF8.GetString(Logged(sent)).Contains("s3cret", StringComparison.Ordinal));
    }
}
