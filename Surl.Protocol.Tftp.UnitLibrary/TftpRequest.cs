using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// A parsed read request (RFC 1350 section 5, RFC 2347): the file name, the transfer mode
/// and the options, in the order the client sent them.
/// </summary>
/// <param name="FileName">The file name's bytes, exactly as sent.</param>
/// <param name="Mode">The transfer mode, in lower case: <c>octet</c> or <c>netascii</c>.</param>
/// <param name="Options">The options, with names and values as sent.</param>
internal sealed record TftpReadRequest(byte[] FileName, string Mode, IReadOnlyList<TftpOption> Options)
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Parses a read request: opcode 1, then the file name, the mode, and any number of
    /// option names and values, each followed by a zero byte.
    /// </summary>
    /// <param name="datagram">The whole first datagram of the flow.</param>
    /// <param name="request">The request, when it parses.</param>
    /// <returns>
    /// <see langword="true"/> for a read request whose last byte is zero, with a mode of
    /// <c>octet</c> or <c>netascii</c> (in any case) and a value after every option name;
    /// <see langword="false"/> for anything else.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> datagram, out TftpReadRequest? request)
    {
        request = IsZeroTerminatedReadRequest(datagram) ? FromFields(SplitFields(datagram[2..^1])) : null;

        return request is not null;
    }

    private static bool IsZeroTerminatedReadRequest(ReadOnlySpan<byte> datagram) =>
        TftpPacket.ReadOpcode(datagram) == TftpPacket.ReadRequest && datagram.Length > 2 && datagram[^1] == 0;

    private static TftpReadRequest? FromFields(List<byte[]> fields)
    {
        if (fields.Count < 2 || fields.Count % 2 != 0)
        {
            return null;
        }

        var mode = Latin1.GetString(fields[1]).ToLowerInvariant();
        if (mode is not ("octet" or "netascii"))
        {
            return null;
        }

        return new TftpReadRequest(fields[0], mode, OptionsFrom(fields));
    }

    private static IReadOnlyList<TftpOption> OptionsFrom(List<byte[]> fields)
    {
        var options = new List<TftpOption>();
        for (var index = 2; index < fields.Count; index += 2)
        {
            options.Add(new TftpOption(Latin1.GetString(fields[index]), Latin1.GetString(fields[index + 1])));
        }

        return options.AsReadOnly();
    }

    private static List<byte[]> SplitFields(ReadOnlySpan<byte> body)
    {
        var fields = new List<byte[]>();
        foreach (var range in body.Split((byte)0))
        {
            fields.Add(body[range].ToArray());
        }

        return fields;
    }
}
