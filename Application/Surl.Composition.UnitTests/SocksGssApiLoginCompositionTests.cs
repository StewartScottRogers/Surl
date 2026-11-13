using System.Buffers.Binary;
using System.Text;
using Surl.Cli;
using Surl.Protocol.Socks;

namespace Surl.Composition;

[TestClass]
public sealed class SocksGssApiLoginCompositionTests
{
    [TestMethod]
    public void ComposeSocksGssApiLogin_WithoutAKeytab_IsNone()
    {
        var seams = new StartTestSeams().Seams;

        var login = SurlServerComposition.ComposeSocksGssApiLogin(new SurlCommandLine(), seams);

        Assert.IsNull(login);
    }

    [TestMethod]
    public void ComposeSocksGssApiLogin_WithAKeytab_IsOverTheKeytabAndTheDefaultService()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => AesKeytab() };

        var login = SurlServerComposition.ComposeSocksGssApiLogin(new SurlCommandLine { KeytabFile = "socks.keytab" }, seams);

        Assert.IsNotNull(login);
        Assert.AreEqual(SocksGssApiLogin.DefaultService, login.Service);
    }

    private static byte[] AesKeytab()
    {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        List<byte> body = [];
        body.AddRange(U16(2));
        body.AddRange(Counted("EXAMPLE.COM"));
        body.AddRange(Counted("rcmd"));
        body.AddRange(Counted("host.example.com"));
        body.AddRange(U32(1));
        body.AddRange(U32(0));
        body.Add(1);
        body.AddRange(U16(17));
        body.AddRange(U16(key.Length));
        body.AddRange(key);
        return [0x05, 0x02, .. U32(body.Count), .. body];
    }

    private static byte[] Counted(string text) => [.. U16(text.Length), .. Encoding.ASCII.GetBytes(text)];

    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
        return bytes;
    }

    private static byte[] U32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
        return bytes;
    }
}
