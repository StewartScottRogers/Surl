using System.Text;

namespace Surl.Http2;

/// <summary>Text to bytes and back for the HTTP/2 tests: one byte per character.</summary>
internal static class Http2TestBytes
{
    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
