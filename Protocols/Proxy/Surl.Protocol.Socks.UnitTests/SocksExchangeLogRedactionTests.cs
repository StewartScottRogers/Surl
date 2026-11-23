using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksExchangeLogRedactionTests
{
    private static readonly byte[] Greeting = [5, 1, 2];
    private static readonly byte[] PasswordMethod = [5, 2];
    private static readonly byte[] Login = [1, 5, .. "alice"u8, 6, .. "s3cret"u8];

    private static IReceivedBytesRedactor NewRedactor() =>
        new SocksProtocolServer(new InMemoryOriginConnector()).CreateRedactor(new ExchangeRedactionContext(1, "socks5"));

    private static byte[] Logged(IEnumerable<(byte[] Sent, byte[][] Received)> turns)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var (sent, received) in turns)
        {
            redactor.ObserveSent(sent);
            foreach (var chunk in received)
            {
                redactor.Redact(chunk, output);
            }
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] Masked() => [1, 5, .. "alice"u8, 4, .. "****"u8];

    [TestMethod]
    public void Socks5Subnegotiation_PasswordsOfDifferentLengths_AreLoggedTheSame()
    {
        byte[] Loginwith(int length) => [1, 5, .. "alice"u8, (byte)length, .. new byte[length].Select(_ => (byte)'x')];

        var shortLogged = Logged([([], [Greeting]), (PasswordMethod, [Loginwith(1)])]);
        var longLogged = Logged([([], [Greeting]), (PasswordMethod, [Loginwith(255)])]);

        byte[] expectedTail = [4, 0x2A, 0x2A, 0x2A, 0x2A];
        CollectionAssert.AreEqual(expectedTail, shortLogged[^5..]);
        CollectionAssert.AreEqual(shortLogged, longLogged);
    }

    [TestMethod]
    public void Socks5Subnegotiation_IsLoggedWithPasswdMasked()
    {
        var logged = Logged([([], [Greeting]), (PasswordMethod, [Login])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], logged);
    }

    [TestMethod]
    public void Socks5Subnegotiation_SplitAtEveryByte_IsLoggedTheSame()
    {
        for (var split = 1; split < Login.Length; split++)
        {
            var redactor = NewRedactor();
            var output = new ArrayBufferWriter<byte>();
            redactor.Redact(Greeting, output);
            redactor.ObserveSent(PasswordMethod);
            redactor.Redact(Login.AsSpan(0, split), output);
            redactor.Redact(Login.AsSpan(split), output);
            redactor.Flush(output);

            CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray(), $"split {split}");
        }
    }

    [TestMethod]
    public void Socks5Subnegotiation_OneByteAtATime_IsLoggedTheSame()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var value in Greeting)
        {
            redactor.Redact([value], output);
        }

        redactor.ObserveSent(PasswordMethod);
        foreach (var value in Login)
        {
            redactor.Redact([value], output);
        }

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void TunnelledBytesAfterTheReply_ContainingThePassword_AreLoggedUnchanged()
    {
        byte[] request = [5, 1, 0, 3, 1, (byte)'h', 0, 80];
        byte[] tunnelled = [.. "s3cret"u8];
        var logged = Logged([([], [Greeting]), (PasswordMethod, [Login]), ([1, 0], [request]), ([5, 0, 0, 1, 0, 0, 0, 0, 0, 0], [tunnelled])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked(), .. request, .. tunnelled], logged);
    }

    [TestMethod]
    public void Socks4Request_IsLoggedAsBefore()
    {
        byte[] request = [4, 1, 0, 80, 127, 0, 0, 1, .. "s3cret"u8, 0];

        CollectionAssert.AreEqual(request, Logged([([], [request])]));
    }

    [TestMethod]
    public void NoAuthenticationMethod_LeavesALaterLookalikeUnchanged()
    {
        byte[] greeting = [5, 0];
        var logged = Logged([([], [greeting]), ([5, 0], [Login])]);

        CollectionAssert.AreEqual((byte[])[.. greeting, .. Login], logged);
    }

    [TestMethod]
    public void MethodSelectionRefusal_LeavesTheNextBytesUnchanged()
    {
        var logged = Logged([([], [Greeting]), ([5, 0xFF], [Login])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Login], logged);
    }

    [TestMethod]
    public void ShortMethodSelection_LeavesTheNextBytesUnchanged()
    {
        var logged = Logged([([], [Greeting]), ([5], [Login])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Login], logged);
    }

    [TestMethod]
    public void GreetingOfNoMethods_ThenSentBytes_AreFollowedWithoutMasking()
    {
        byte[] greeting = [5, 0];
        var logged = Logged([([], [greeting]), ([5, 0xFF], [[9]])]);

        CollectionAssert.AreEqual((byte[])[5, 0, 9], logged);
    }

    [TestMethod]
    public void EmptyPassword_IsLoggedAsFourAndTheMask()
    {
        byte[] login = [1, 0, 0];

        CollectionAssert.AreEqual((byte[])[.. Greeting, 1, 0, 4, .. "****"u8], Logged([([], [Greeting]), (PasswordMethod, [login])]));
    }

    [TestMethod]
    public void SentBytesAfterTheMethodWasChosen_AreIgnored()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Greeting, output);
        redactor.ObserveSent(PasswordMethod);
        redactor.ObserveSent([1, 0]);
        redactor.Redact(Login, output);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void NullOutput_IsRefused()
    {
        var redactor = NewRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
