using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// A parsed read or write request (RFC 1350 section 5, RFC 2347): which of the two it is,
/// the file name, the transfer mode and the options, in the order the client sent them.
/// </summary>
/// <param name="IsWrite"><see langword="true"/> for a write request (<c>WRQ</c>), <see langword="false"/> for a read request (<c>RRQ</c>).</param>
/// <param name="FileName">The file name's bytes, exactly as sent.</param>
/// <param name="Mode">The transfer mode, in lower case: <c>octet</c> or <c>netascii</c>.</param>
/// <param name="Options">The options, with names and values as sent.</param>
internal sealed record TftpRequest(bool IsWrite, byte[] FileName, string Mode, IReadOnlyList<TftpOption> Options)
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// The request's kind as the verbose log names it: <c>Read</c> or <c>Write</c>.
    /// </summary>
    public string Kind => IsWrite ? "Write" : "Read";

    /// <summary>
    /// Parses a read or write request: opcode 1 or 2, then the file name, the mode, and any
    /// number of option names and values, each followed by a zero byte.
    /// </summary>
    /// <param name="datagram">The whole first datagram of the flow.</param>
    /// <param name="request">The request, when it parses.</param>
    /// <returns>
    /// <see langword="true"/> for a read or write request whose last byte is zero, with a mode
    /// of <c>octet</c> or <c>netascii</c> (in any case) and a value after every option name;
    /// <see langword="false"/> for anything else.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> datagram, out TftpRequest? request)
    {
        request = IsZeroTerminatedRequest(datagram)
            ? FromFields(TftpPacket.ReadOpcode(datagram) == TftpPacket.WriteRequest, SplitFields(datagram[2..^1]))
            : null;

        return request is not null;
    }

    private static bool IsZeroTerminatedRequest(ReadOnlySpan<byte> datagram) =>
        TftpPacket.ReadOpcode(datagram) is TftpPacket.ReadRequest or TftpPacket.WriteRequest && datagram.Length > 2 && datagram[^1] == 0;

    private static TftpRequest? FromFields(bool isWrite, List<byte[]> fields)
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

        return new TftpRequest(isWrite, fields[0], mode, OptionsFrom(fields));
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
