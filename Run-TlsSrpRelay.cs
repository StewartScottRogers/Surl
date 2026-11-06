#:project Cryptography/Surl.TlsSrp.UnitLibrary/Surl.TlsSrp.UnitLibrary.csproj

// The TLS-SRP relay of Record-CurlExchange.ps1 -TlsSrp (BL-566, ADR-0122): it shakes hands with a
// pinned upstream curl build as Surl.TlsSrp's server, then relays the plaintext to the recorder's
// own server, so request.bin and the canned responses work as with -Tls:
//
//   dotnet run --file Run-TlsSrpRelay.cs -- <listen address> <port> <user> <password> <salt hex> <random hex> <capture directory>
//
// The one account is <user> with the verifier of <password> and <salt> in RFC 5054 Appendix A's
// 2048-bit group. Every byte the server draws (its random, b and each explicit IV) is <random hex>
// repeated, so a test replays the recording with the same bytes. It reads the backend port from
// its first line of standard input, prints "listening" once bound, then serves each connection
// until it is killed, writing every byte curl sent to client-<n>.bin and every byte the server
// sent to server-<n>.bin in the capture directory, n counting connections from 1, and the
// handshake's outcome to standard error.
//
// It is a measuring fixture, never a surl server: surl serves TLS-SRP through Surl.Networking.
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Srp;
using Surl.TlsSrp;

var listenAddress = IPAddress.Parse(args[0]);
var port = int.Parse(args[1]);
var userName = args[2];
var salt = Convert.FromHexString(args[4]);
var randomBytes = Convert.FromHexString(args[5]);
var captureDirectory = args[6];
var group = SrpGroup.Rfc5054Group2048;
var verifier = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeVerifier(group, salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(args[3])));
var accounts = new TlsSrpAccounts([new SrpVerifierEntry(userName, salt, verifier, group)], new byte[TlsSrpAccounts.SimulationSecretLength]);
// Windows PowerShell's standard input writer may lead with a byte order mark; keep the digits.
var backendPort = int.Parse(string.Concat(Console.ReadLine()!.Where(char.IsAsciiDigit)));
var listener = new TcpListener(listenAddress, port);
listener.Start();
Console.WriteLine("listening");
Console.Out.Flush();

var connectionNumber = 0;
while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = RelayAsync(client, Interlocked.Increment(ref connectionNumber));
}

async Task RelayAsync(TcpClient client, int number)
{
    var capture = new CapturingStream(client.GetStream());
    try
    {
        var server = new TlsSrpServer(accounts, new RepeatingRandomNumberGenerator(randomBytes));
        TlsSrpSession session;
        try
        {
            session = await server.AuthenticateAsync(capture, CancellationToken.None);
        }
        catch (Exception exception) when (exception is TlsAlertException or TlsPeerAlertException or IOException)
        {
            var alert = exception is TlsAlertException sent ? $" (sent {sent.Description})" : string.Empty;
            Console.Error.WriteLine($"relay: handshake failed{alert}: {exception.Message}");
            return;
        }

        Console.Error.WriteLine($"relay: handshake completed: {session.CipherSuite}, user {session.UserName}, ALPN {session.ApplicationProtocol ?? "none"}, EMS {session.ExtendedMasterSecret}, ETM {session.EncryptThenMac}");
        await using (session.Stream)
        {
            using var backend = new TcpClient();
            await backend.ConnectAsync(IPAddress.Loopback, backendPort);
            var backendStream = backend.GetStream();
            _ = CopyThenShutdownAsync(session.Stream, backendStream, backend.Client);
            try
            {
                await backendStream.CopyToAsync(session.Stream);
            }
            catch (IOException)
            {
                // curl hung up first.
            }
        }
    }
    finally
    {
        client.Dispose();
        await File.WriteAllBytesAsync(Path.Combine(captureDirectory, $"client-{number}.bin"), capture.Received.ToArray());
        await File.WriteAllBytesAsync(Path.Combine(captureDirectory, $"server-{number}.bin"), capture.Sent.ToArray());
    }
}

static async Task CopyThenShutdownAsync(Stream from, Stream to, Socket toSocket)
{
    try
    {
        await from.CopyToAsync(to);
        toSocket.Shutdown(SocketShutdown.Send);
    }
    catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
    {
        // The connection closed while bytes were still being relayed.
    }
}

/// <summary>Fills every request with the same bytes, repeated, so a recording can be replayed.</summary>
internal sealed class RepeatingRandomNumberGenerator(byte[] pattern) : RandomNumberGenerator
{
    public override void GetBytes(byte[] data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = pattern[index % pattern.Length];
        }
    }
}

/// <summary>Passes reads and writes through to the connection and keeps a copy of both directions.</summary>
internal sealed class CapturingStream(Stream inner) : Stream
{
    public MemoryStream Received { get; } = new();

    public MemoryStream Sent { get; } = new();

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await inner.ReadAsync(buffer, cancellationToken);
        lock (Received)
        {
            Received.Write(buffer.Span[..count]);
        }

        return count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        lock (Sent)
        {
            Sent.Write(buffer.Span);
        }

        await inner.WriteAsync(buffer, cancellationToken);
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        base.Dispose(disposing);
    }
}
