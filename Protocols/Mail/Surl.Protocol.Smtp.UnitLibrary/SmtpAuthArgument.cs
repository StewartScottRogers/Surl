using System.Text;
using Surl.LineProtocol;

namespace Surl.Protocol.Smtp;

/// <summary>
/// <c>AUTH</c>'s argument (RFC 4954, section 4): the mechanism, then optionally one space and
/// the initial response in base64, <c>=</c> standing for an empty one.
/// </summary>
/// <param name="Mechanism">The mechanism as the client named it; the policy matches it.</param>
/// <param name="InitialResponse">The initial response, decoded; <see langword="null"/> when none was sent.</param>
internal sealed record SmtpAuthArgument(string Mechanism, ReadOnlyMemory<byte>? InitialResponse)
{
    /// <summary>
    /// Splits and decodes <paramref name="argument"/>, the bytes after <c>AUTH </c>.
    /// </summary>
    /// <param name="argument">The argument's bytes.</param>
    /// <param name="read">The mechanism and initial response, when the initial response decodes.</param>
    /// <returns><see langword="false"/> when an initial response was sent and is not base64
    /// (whitespace in it, or <c>*</c>, included).</returns>
    public static bool TryRead(byte[] argument, out SmtpAuthArgument? read)
    {
        var space = Array.IndexOf(argument, (byte)' ');
        var mechanism = Encoding.ASCII.GetString(space < 0 ? argument : argument[..space]);
        var initialResponse = space < 0 || space == argument.Length - 1 ? null : argument[(space + 1)..];
        if (initialResponse is null)
        {
            read = new SmtpAuthArgument(mechanism, null);
            return true;
        }

        var decoded = Decode(initialResponse);
        read = decoded is null ? null : new SmtpAuthArgument(mechanism, decoded);
        return read is not null;
    }

    private static byte[]? Decode(byte[] initialResponse) =>
        initialResponse.AsSpan().SequenceEqual("="u8) ? [] : SaslContinuationLine.Classify(initialResponse).Response;
}
