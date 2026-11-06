using System.Buffers;
using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The three shapes every RESP type is written in: a line (prefix, text, CR LF), a blob (prefix,
/// length line, bytes, CR LF) and an aggregate (prefix, count line, elements).
/// </summary>
internal static class RespEncoding
{
    /// <summary>Writes <paramref name="prefix"/>, <paramref name="text"/> and CR LF.</summary>
    public static void WriteLine(IBufferWriter<byte> output, byte prefix, ReadOnlySpan<byte> text)
    {
        output.Write([prefix]);
        output.Write(text);
        output.Write("\r\n"u8);
    }

    /// <summary>Writes <paramref name="prefix"/>, <paramref name="number"/> in decimal and CR LF.</summary>
    public static void WriteLine(IBufferWriter<byte> output, byte prefix, long number) =>
        WriteLine(output, prefix, Encoding.ASCII.GetBytes(number.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Writes <paramref name="prefix"/>, the length line, <paramref name="bytes"/> and CR LF.</summary>
    public static void WriteBlob(IBufferWriter<byte> output, byte prefix, ReadOnlySpan<byte> bytes)
    {
        WriteLine(output, prefix, bytes.Length);
        output.Write(bytes);
        output.Write("\r\n"u8);
    }

    /// <summary>Writes <paramref name="prefix"/>, the element count line and each element.</summary>
    public static void WriteAggregate(IBufferWriter<byte> output, byte prefix, IReadOnlyList<RespValue> elements)
    {
        WriteLine(output, prefix, elements.Count);

        foreach (var element in elements)
        {
            element.EncodeTo(output);
        }
    }

    /// <summary>Writes <paramref name="prefix"/>, the pair count line and each key then its value.</summary>
    public static void WritePairs(
        IBufferWriter<byte> output,
        byte prefix,
        IReadOnlyList<KeyValuePair<RespValue, RespValue>> pairs)
    {
        WriteLine(output, prefix, pairs.Count);

        foreach (var pair in pairs)
        {
            pair.Key.EncodeTo(output);
            pair.Value.EncodeTo(output);
        }
    }

    /// <summary>
    /// Copies <paramref name="text"/>, refusing CR and LF, which a simple string or simple error
    /// cannot hold (the RESP page).
    /// </summary>
    public static byte[] RequireNoLineBreak(ReadOnlySpan<byte> text, string parameterName)
    {
        if (text.IndexOfAny((byte)'\r', (byte)'\n') >= 0)
        {
            throw new ArgumentException("A simple string or simple error cannot hold CR or LF.", parameterName);
        }

        return text.ToArray();
    }

    /// <summary>The UTF-8 bytes of <paramref name="text"/>, which must not be null.</summary>
    public static byte[] Utf8(string text, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(text, parameterName);
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>A copy of <paramref name="elements"/>, refusing a null list or element.</summary>
    public static RespValue[] RequireElements(IReadOnlyList<RespValue> elements, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(elements, parameterName);
        var copy = elements.ToArray();

        if (Array.Exists(copy, element => element is null))
        {
            throw new ArgumentException("An element is null.", parameterName);
        }

        return copy;
    }

    /// <summary>A copy of <paramref name="pairs"/>, refusing a null list, key or value.</summary>
    public static KeyValuePair<RespValue, RespValue>[] RequirePairs(
        IReadOnlyList<KeyValuePair<RespValue, RespValue>> pairs,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(pairs, parameterName);
        var copy = pairs.ToArray();

        if (Array.Exists(copy, pair => pair.Key is null || pair.Value is null))
        {
            throw new ArgumentException("A key or a value is null.", parameterName);
        }

        return copy;
    }
}
