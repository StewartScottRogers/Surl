using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>Drives the SOCKS server's redactor past a granted request and reads what it logs of the tunnel.</summary>
internal static class SocksTunnelRedactionTestKit
{
    /// <summary>A redactor whose SOCKS5 <c>CONNECT</c> the server has granted.</summary>
    public static IReceivedBytesRedactor GrantedSocks5Tunnel()
    {
        var redactor = new SocksProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "socks5"));
        var handshake = new ArrayBufferWriter<byte>();
        redactor.Redact([5, 1, 0], handshake);
        redactor.ObserveSent([5, 0]);
        redactor.Redact(SocksTestKit.Socks5DomainRequest("localhost", 1883), handshake);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);

        return redactor;
    }

    /// <summary>What a granted tunnel logs of <paramref name="chunks"/>, with the bytes held at the end.</summary>
    public static byte[] Logged(params byte[][] chunks)
    {
        var redactor = GrantedSocks5Tunnel();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
            redactor.ObserveSent([0x20, 0x02, 0x00, 0x00]);
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    /// <summary>Asserts the log of <paramref name="sent"/> split in two at every byte, and sent one byte at a time.</summary>
    public static void AssertLoggedHoweverSplit(byte[] expected, byte[] sent)
    {
        for (var split = 0; split < sent.Length; split++)
        {
            CollectionAssert.AreEqual(expected, Logged(sent[..split], sent[split..]), $"split at {split}");
        }

        CollectionAssert.AreEqual(expected, Logged([.. sent.SelectMany(value => new byte[][] { [], [value] })]));
    }
}
