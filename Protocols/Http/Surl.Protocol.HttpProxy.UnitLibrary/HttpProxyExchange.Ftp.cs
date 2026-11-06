using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Forwarding an <c>ftp://</c> absolute-form <c>GET</c> (ADR-0088 decision 3 and Amendment 4):
/// the proxy logs in to the origin over FTP, fetches the file or listing through a passive data
/// connection, and answers curl <c>200</c> with its bytes.
/// </summary>
internal sealed partial class HttpProxyExchange
{
    private const int FtpCopyBufferBytes = 16384;

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    // The port of a 229 reply: "(|||port|)" (RFC 2428 section 3).
    private static int? ExtendedPassivePort(string line)
    {
        var start = line.IndexOf("(|||", StringComparison.Ordinal) + 4;
        var end = start < 4 ? -1 : line.IndexOf("|)", start, StringComparison.Ordinal);

        return end < 0 ? null : DataPort(line[start..end]);
    }

    // The port of a 227 reply: the six numbers h1,h2,h3,h4,p1,p2 after its code (RFC 959
    // section 4.1.2, RFC 1123 section 4.1.2.6); the host numbers are not used, since the data
    // connection goes to the origin's own host.
    private static int? PassivePort(string line)
    {
        var text = line.AsSpan(3);
        var start = text.IndexOfAnyInRange('0', '9');
        var numbers = start < 0 ? text[..0] : text[start..];
        var end = numbers.IndexOfAnyExcept("0123456789,");
        var parts = (end < 0 ? numbers : numbers[..end]).ToString().Split(',');

        return parts.Length == 6
            && byte.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var high)
            && byte.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var low)
                ? DataPort(((high << 8) | low).ToString(CultureInfo.InvariantCulture))
                : null;
    }

    private static int? DataPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535 ? port : null;

    private async Task<bool> AnswerFtpAsync(HttpRequestHead head, ProxyTarget target, bool closes)
    {
        if (FtpFetch.Of(target, head) is not { } fetch)
        {
            return await AnswerAsync(HttpStatus.BadRequest, "the ftp:// path or credentials hold a control character, or the Basic credentials are not Base64", closes);
        }

        FtpControlConnection control;
        try
        {
            var connection = await originConnector.ConnectAsync(target.Host, target.Port, context.Limits.HeadTimeout, Cancellation);
            control = new FtpControlConnection(connection, context.TimeProvider, TimerDelay(context.Limits.HeadTimeout), Cancellation);
        }
        catch (OriginConnectionException exception)
        {
            return await AnswerOriginFailureAsync(exception.Failure, target, closes);
        }

        await using (control)
        {
            return await FetchOverFtpAsync(control, fetch, target, closes);
        }
    }

    private async Task<bool> FetchOverFtpAsync(FtpControlConnection control, FtpFetch fetch, ProxyTarget target, bool closes)
    {
        IConnection data;
        try
        {
            data = await OpenFtpDataConnectionAsync(control, fetch, target);
        }
        catch (FtpOriginException exception)
        {
            return await AnswerFtpFailureAsync(exception, target, closes);
        }

        await using (data)
        {
            try
            {
                await control.ExpectAsync(fetch.TransferCommandLine, 125, 150);
            }
            catch (FtpOriginException exception)
            {
                return await AnswerFtpFailureAsync(exception, target, closes);
            }

            return await RelayFtpTransferAsync(control, data, fetch, target, closes);
        }
    }

    private Task<bool> AnswerFtpFailureAsync(FtpOriginException exception, ProxyTarget target, bool closes) =>
        AnswerAsync(exception.TimedOut ? HttpProxyStatus.GatewayTimeout : HttpProxyStatus.BadGateway, $"FTP origin {target.Host}:{target.Port} {exception.Message}", closes);

    // Logs in, sets the type and opens the passive data connection, which RFC 959 section 3.2
    // has the client open before it sends the transfer command.
    private async Task<IConnection> OpenFtpDataConnectionAsync(FtpControlConnection control, FtpFetch fetch, ProxyTarget target)
    {
        await control.ExpectAsync(null, 220);
        if (await control.ExpectAsync($"USER {fetch.User}", 230, 331) == 331)
        {
            await control.ExpectAsync($"PASS {fetch.Password}", 230);
        }

        await control.ExpectAsync($"TYPE {fetch.TransferType}", 200);
        return await ConnectFtpDataAsync(target.Host, await PassiveDataPortAsync(control));
    }

    // EPSV first, as upstream curl asks an FTP server; PASV when EPSV is not answered 229 with a port.
    private static async Task<int> PassiveDataPortAsync(FtpControlConnection control)
    {
        var (code, line) = await control.CommandAsync("EPSV");
        if (code == 229 && ExtendedPassivePort(line) is { } extendedPort)
        {
            return extendedPort;
        }

        (code, line) = await control.CommandAsync("PASV");

        return code == 227 && PassivePort(line) is { } port
            ? port
            : throw new FtpOriginException(false, $"answered EPSV and PASV with no data port ({line})");
    }

    private async Task<IConnection> ConnectFtpDataAsync(string host, int port)
    {
        try
        {
            return await originConnector.ConnectAsync(host, port, context.Limits.HeadTimeout, Cancellation);
        }
        catch (OriginConnectionException exception)
        {
            throw new FtpOriginException(exception.Failure == OriginConnectionFailure.TimedOut, $"could not be reached on its data port {port} ({exception.Failure})");
        }
    }

    // Writes the 200 head, relays the data connection's bytes chunked (or close-delimited when
    // curl's connection closes after this request), then requires the origin's 226 or 250: a
    // transfer the origin did not complete aborts curl's connection, since the head has gone.
    private async Task<bool> RelayFtpTransferAsync(FtpControlConnection control, IConnection data, FtpFetch fetch, ProxyTarget target, bool closes)
    {
        var head = NewHead(HttpStatus.Ok)
            .AddField("Content-Type", fetch.IsListing ? "text/plain" : "application/octet-stream")
            .AddField("Transfer-Encoding", closes ? null : "chunked")
            .AddField("Via", "1.1 surl")
            .AddField("Connection", closes ? "close" : null);
        context.Log.Note($"Fetched {fetch.TransferCommand} from FTP origin {target.Host}:{target.Port}; answered 200.");
        await client.WriteAsync(head.ToBytes(), Cancellation);

        if (!await CopyFtpDataAsync(data, chunked: !closes) || !await FtpTransferCompletedAsync(control))
        {
            context.Log.Note($"FTP origin {target.Host}:{target.Port} did not complete the transfer; curl's connection is aborted.");
            client.Abort();

            return false;
        }

        await control.SendQuitAsync();
        await client.WriteAsync(closes ? ReadOnlyMemory<byte>.Empty : LastChunk, Cancellation);
        if (closes)
        {
            await client.CompleteWritesAsync(Cancellation);
        }

        return !closes;
    }

    private static async Task<bool> FtpTransferCompletedAsync(FtpControlConnection control)
    {
        try
        {
            await control.ExpectAsync(null, 226, 250);

            return true;
        }
        catch (FtpOriginException)
        {
            return false;
        }
    }

    // False when the data connection failed rather than closed.
    private async Task<bool> CopyFtpDataAsync(IConnection data, bool chunked)
    {
        var buffer = new byte[FtpCopyBufferBytes];
        try
        {
            int read;
            while ((read = await data.ReadAsync(buffer, Cancellation)) > 0)
            {
                await client.WriteAsync(chunked ? Chunk(buffer.AsSpan(0, read)) : buffer.AsMemory(0, read), Cancellation);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static byte[] Chunk(ReadOnlySpan<byte> bytes)
    {
        var size = Encoding.ASCII.GetBytes(bytes.Length.ToString("X", CultureInfo.InvariantCulture) + "\r\n");

        return [.. size, .. bytes, (byte)'\r', (byte)'\n'];
    }
}
