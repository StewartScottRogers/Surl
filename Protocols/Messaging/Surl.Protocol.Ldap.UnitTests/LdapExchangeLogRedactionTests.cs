using System.Buffers;
using System.Formats.Asn1;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapExchangeLogRedactionTests
{
    private static IReceivedBytesRedactor NewRedactor() =>
        LdapServerExchange.Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).CreateRedactor(new ExchangeRedactionContext(1, "ldap"));

    private static byte[] Logged(params byte[][] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.ObserveSent([1, 2, 3]);
        redactor.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static byte[] SaslBind(string mechanism, string? credentials, int messageId = 1) => Message(messageId, writer =>
    {
        using var bind = writer.PushSequence(LdapTags.BindRequest);
        writer.WriteInteger(3);
        Text(writer, "uid=alice");
        using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
        Text(writer, mechanism);
        if (credentials is not null)
        {
            Text(writer, credentials);
        }
    });

    // The message as it is logged: the secret and the length octets before it become 04 then ****.
    private static byte[] Replace(byte[] message, string secret)
    {
        var lengthOctets = secret.Length < 128 ? 1 : secret.Length < 256 ? 2 : 3;
        var text = Encoding.Latin1.GetString(message);
        var at = text.IndexOf(secret, StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(string.Concat(text.AsSpan(0, at - lengthOctets), "\u0004****", text.AsSpan(at + secret.Length)));
    }

    private static readonly byte[] Simple = Message(1, SimpleBind(3, "uid=alice,dc=example", "s3cret"));
    private static readonly byte[] SimpleMasked = Replace(Simple, "s3cret");

    private static void AssertLogged(byte[] expected, params byte[][] chunks) =>
        CollectionAssert.AreEqual(expected, Logged(chunks));

    [TestMethod]
    public void SimpleBind_Password_IsLoggedMasked() => AssertLogged(SimpleMasked, Simple);

    [TestMethod]
    public void SimpleBind_PasswordLength_IsLoggedAsFourBeforeTheMask()
    {
        var logged = Logged(Simple);
        var mask = Array.IndexOf(logged, (byte)'*');
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x04, (byte)'*', (byte)'*', (byte)'*', (byte)'*' }, logged[(mask - 2)..(mask + 4)]);
    }

    [TestMethod]
    public void SimpleBind_LongFormPasswordLength_IsLoggedAsFourBeforeTheMask()
    {
        var message = Message(1, SimpleBind(3, "uid=alice", new string('p', 200)));
        var logged = Logged(message);
        var mask = Array.IndexOf(logged, (byte)'*');
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x04 }, logged[(mask - 2)..mask]);
    }

    [TestMethod]
    public void SimpleBind_SplitAtEveryByte_IsMaskedTheSame()
    {
        for (var split = 1; split < Simple.Length; split++)
        {
            AssertLogged(SimpleMasked, Simple[..split], Simple[split..]);
        }
    }

    [TestMethod]
    public void SimpleBind_OneByteAtATime_IsMaskedTheSame() =>
        AssertLogged(SimpleMasked, [.. Simple.Select(value => new[] { value })]);

    [TestMethod]
    public void SimpleBind_EmptyPassword_IsLoggedAsReceived()
    {
        var anonymous = Message(1, SimpleBind(3, string.Empty, string.Empty));
        AssertLogged(anonymous, anonymous);
    }

    [TestMethod]
    public void SaslPlainBind_Credentials_AreLoggedMasked()
    {
        var message = SaslBind("plain", "\0alice\0s3cret");
        AssertLogged(Replace(message, "\0alice\0s3cret"), message);
    }

    [TestMethod]
    public void SaslGssSpnegoBind_Credentials_AreLoggedAsReceived()
    {
        var message = SaslBind("GSS-SPNEGO", "NTLMSSP-token");
        AssertLogged(message, message);
    }

    [TestMethod]
    public void SaslPlainBind_WithoutCredentials_IsLoggedAsReceived()
    {
        var message = SaslBind("PLAIN", null);
        AssertLogged(message, message);
    }

    [TestMethod]
    public void SaslPlainBind_WithEmptyCredentials_IsLoggedAsReceived()
    {
        var message = SaslBind("PLAIN", string.Empty);
        AssertLogged(message, message);
    }

    [TestMethod]
    public void SearchRequest_FilterHoldingThePasswordBytes_IsLoggedUnchanged()
    {
        var search = Message(2, Search(Equality("userPassword", "s3cret")));
        AssertLogged(search, search);
        AssertLogged(Concat(SimpleMasked, search), Concat(Simple, search));
    }

    [TestMethod]
    public void BindWithLongFormLengths_IsMasked()
    {
        var password = new string('p', 200);
        var message = Message(1, SimpleBind(3, "uid=alice", password));
        Assert.AreEqual(0x81, message[1]);
        AssertLogged(Replace(message, password), message[..40], message[40..]);
    }

    [TestMethod]
    public void BindLongerThanTheHoldLimit_IsLoggedAsOneMask()
    {
        var message = Message(1, SimpleBind(3, "uid=alice", new string('p', 70000)));
        AssertLogged(Concat("****"u8.ToArray(), SimpleMasked), message[..100], message[100..], Simple);
    }

    [TestMethod]
    public void BindCutOffByTheEnd_IsMaskedFromTheFieldItStoppedIn()
    {
        var logged = Encoding.Latin1.GetString(Logged(Simple[..^3]));
        Assert.IsFalse(logged.Contains("s3c", StringComparison.Ordinal));
        Assert.IsTrue(logged.EndsWith("****", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BindCutOffInEachEarlierField_IsMaskedFromThatField()
    {
        for (var length = 6; length < Simple.Length - 8; length++)
        {
            var logged = Encoding.Latin1.GetString(Logged(Simple[..length]));
            Assert.IsFalse(logged.Contains("s3cret", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void SaslBindCutOff_IsMaskedFromThatField()
    {
        var message = SaslBind("PLAIN", "\0alice\0s3cret");
        for (var length = 1; length < message.Length; length++)
        {
            Assert.IsFalse(Encoding.Latin1.GetString(Logged(message[..length])).Contains("s3cret", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void SaslBindWhoseMechanismOverrunsItsField_IsMaskedFromTheMechanism()
    {
        var message = Hex("30 11 02 01 01 60 0C 02 01 03 04 01 78 A3 04 04 05 50 4C");

        var logged = Logged(message);

        CollectionAssert.AreEqual(Concat(message[..17], "****"u8.ToArray()), logged);
    }

    [TestMethod]
    public void NonBerBytes_AreLoggedUnchanged()
    {
        var bytes = Encoding.Latin1.GetBytes("\x16\x03\x01 not ber");
        AssertLogged(Concat(bytes, Simple), bytes, Simple);
    }

    [TestMethod]
    public void IndefiniteLengthMessage_IsLoggedUnchanged()
    {
        byte[] bytes = [0x30, 0x80, 0x02, 0x01, 0x01];
        AssertLogged(bytes, bytes);
    }

    [TestMethod]
    public void MessageIdThatNeverEnds_IsLoggedUnchanged()
    {
        var bytes = Concat([0x30, 0x7F, 0x02, 0x84, 0xFF, 0xFF, 0xFF, 0xFF], new byte[40]);
        AssertLogged(bytes, bytes);
    }

    [TestMethod]
    public void MessageWithOnlyItsHeader_WaitsForMore()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Simple[..4], output);
        Assert.AreEqual(0, output.WrittenCount);
        redactor.Redact(Simple[4..], output);
        CollectionAssert.AreEqual(SimpleMasked, output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void BindWithUnknownAuthenticationChoice_IsLoggedAsReceived()
    {
        var message = Message(1, writer =>
        {
            using var bind = writer.PushSequence(LdapTags.BindRequest);
            writer.WriteInteger(3);
            Text(writer, "x");
            Text(writer, "y", LdapTags.Context(9));
        });
        AssertLogged(message, message);
    }

    [TestMethod]
    public void SaslBindWithNonStringCredentials_IsLoggedAsReceived()
    {
        var message = Message(1, writer =>
        {
            using var bind = writer.PushSequence(LdapTags.BindRequest);
            writer.WriteInteger(3);
            Text(writer, "x");
            using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
            Text(writer, "PLAIN");
            writer.WriteInteger(5);
        });
        AssertLogged(message, message);
    }

    [TestMethod]
    public void Redact_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NewRedactor().Redact([], null!));

    [TestMethod]
    public void Flush_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NewRedactor().Flush(null!));
}
