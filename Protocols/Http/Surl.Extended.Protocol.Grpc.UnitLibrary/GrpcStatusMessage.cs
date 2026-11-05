using System.Text;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Encodes the <c>grpc-message</c> value of PROTOCOL-HTTP2.md "Responses": the text as UTF-8, with every byte
/// outside <c>%x20-%x7E</c>, and <c>%</c> itself, written as <c>%</c> and two upper-case hexadecimal digits.
/// </summary>
public static class GrpcStatusMessage
{
    /// <summary>
    /// Encodes <paramref name="message" />.
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <returns>The header value.</returns>
    public static string Encode(string message)
    {
        var encoded = new StringBuilder(message.Length);
        foreach (var value in Encoding.UTF8.GetBytes(message))
        {
            if (value is >= 0x20 and <= 0x7E and not (byte)'%')
            {
                encoded.Append((char)value);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }
}
