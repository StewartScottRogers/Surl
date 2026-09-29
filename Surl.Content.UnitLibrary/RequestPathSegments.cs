using System.Text;
using System.Text.Unicode;

namespace Surl.Content;

/// <summary>
/// Splits a request path into percent-decoded segments and refuses any segment that could
/// name something other than one entry inside the directory it is joined to.
/// </summary>
internal static class RequestPathSegments
{
    private const string HexDigits = "0123456789abcdef";

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$", "CLOCK$",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "LPT¹", "LPT²", "LPT³",
    };

    /// <summary>
    /// Splits <paramref name="requestPath"/> on <c>/</c> and decodes and checks each segment.
    /// A single trailing <c>/</c> is allowed and adds no segment.
    /// </summary>
    /// <param name="requestPath">The path component of the request, still percent-encoded.</param>
    /// <param name="segments">The decoded segments, in order; empty when refused.</param>
    /// <returns><see cref="ContentPathRefusal.None"/>, or why the path is refused.</returns>
    internal static ContentPathRefusal Split(string requestPath, out string[] segments)
    {
        segments = [];
        if (!requestPath.StartsWith('/'))
        {
            return ContentPathRefusal.NotRooted;
        }

        string[] rawSegments = requestPath.Length == 1 ? [] : requestPath[1..].Split('/');
        var decodedSegments = new List<string>(rawSegments.Length);
        for (int index = 0; index < rawSegments.Length; index++)
        {
            ContentPathRefusal refusal = DecodeSegment(rawSegments, index, decodedSegments);
            if (refusal != ContentPathRefusal.None)
            {
                return refusal;
            }
        }

        segments = [.. decodedSegments];
        return ContentPathRefusal.None;
    }

    private static ContentPathRefusal DecodeSegment(string[] rawSegments, int index, List<string> decodedSegments)
    {
        string rawSegment = rawSegments[index];
        if (rawSegment.Length == 0)
        {
            return index == rawSegments.Length - 1 ? ContentPathRefusal.None : ContentPathRefusal.EmptySegment;
        }

        byte[]? bytes = PercentDecode(Encoding.UTF8.GetBytes(rawSegment));
        if (bytes is null)
        {
            return ContentPathRefusal.InvalidPercentEncoding;
        }

        if (!Utf8.IsValid(bytes))
        {
            return ContentPathRefusal.InvalidUtf8;
        }

        string segment = Encoding.UTF8.GetString(bytes);
        decodedSegments.Add(segment);
        return CheckSegment(segment);
    }

    private static byte[]? PercentDecode(byte[] raw)
    {
        var decoded = new byte[raw.Length];
        int length = 0;
        for (int index = 0; index < raw.Length; index++)
        {
            int value = raw[index] == (byte)'%' ? HexPairValue(raw, index + 1) : raw[index];
            if (value < 0)
            {
                return null;
            }

            index += raw[index] == (byte)'%' ? 2 : 0;
            decoded[length++] = (byte)value;
        }

        return decoded[..length];
    }

    private static int HexPairValue(byte[] raw, int start)
    {
        if (start + 1 >= raw.Length)
        {
            return -1;
        }

        int high = HexDigitValue(raw[start]);
        int low = HexDigitValue(raw[start + 1]);
        return high < 0 || low < 0 ? -1 : (high << 4) | low;
    }

    private static int HexDigitValue(byte digit) => HexDigits.IndexOf(char.ToLowerInvariant((char)digit));

    private static ContentPathRefusal CheckSegment(string segment)
    {
        if (segment is "." or "..")
        {
            return ContentPathRefusal.DotSegment;
        }

        if (segment.AsSpan().IndexOfAny('/', '\\') >= 0)
        {
            return ContentPathRefusal.SeparatorInSegment;
        }

        return CheckCharacters(segment);
    }

    private static ContentPathRefusal CheckCharacters(string segment)
    {
        if (segment.Contains(':'))
        {
            return ContentPathRefusal.ColonInSegment;
        }

        if (segment.Any(char.IsControl))
        {
            return ContentPathRefusal.ControlCharacter;
        }

        return CheckWindowsName(segment);
    }

    private static ContentPathRefusal CheckWindowsName(string segment)
    {
        if (segment.EndsWith('.') || segment.EndsWith(' '))
        {
            return ContentPathRefusal.TrailingDotOrSpace;
        }

        string stem = segment.Split('.')[0].TrimEnd(' ');
        return ReservedDeviceNames.Contains(stem) ? ContentPathRefusal.ReservedDeviceName : ContentPathRefusal.None;
    }
}
