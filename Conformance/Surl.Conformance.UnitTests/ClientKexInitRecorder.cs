using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A loopback listener that stands in for an SSH server just long enough to read what a client
/// sends in clear: it sends the identification line <c>SSH-2.0-surl</c> CR LF, reads the client's
/// identification line and its first binary packet, the <c>SSH_MSG_KEXINIT</c> (RFC 4253
/// sections 4.2 and 7.1), and hangs up - what ADR-0051 recorded for the Windows build with
/// <c>Record-CurlExchange.ps1 -Raw</c>, done here so every platform's CI leg records its own
/// build's lists. It serves one connection. Disposing it stops it.
/// </summary>
internal sealed class ClientKexInitRecorder : IDisposable
{
    private const int NameListCount = 10;
    private const int MaxPacketLength = 35000;

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);

    private ClientKexInitRecorder() => listener.Start();

    /// <summary>Gets the port the recorder listens on.</summary>
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Starts a recorder on an ephemeral loopback port.</summary>
    public static ClientKexInitRecorder Start() => new();

    /// <summary>
    /// Accepts one connection and reads the client's identification line and <c>KEXINIT</c>.
    /// </summary>
    /// <returns>
    /// The identification line without its ending, and the <c>KEXINIT</c>'s ten name-lists in
    /// order: kex, host key, cipher and MAC (client to server, then server to client), compression
    /// (the same), then the two language lists.
    /// </returns>
    public async Task<(string Identification, IReadOnlyList<string> NameLists)> RecordAsync(CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        await stream.WriteAsync("SSH-2.0-surl\r\n"u8.ToArray(), cancellationToken);

        var identification = await ReadLineAsync(stream, cancellationToken);
        var lengthField = new byte[4];
        await stream.ReadExactlyAsync(lengthField, cancellationToken);
        var packetLength = BinaryPrimitives.ReadInt32BigEndian(lengthField);
        Assert.IsTrue(packetLength is > 0 and <= MaxPacketLength, $"packet_length {packetLength}");
        var packet = new byte[packetLength];
        await stream.ReadExactlyAsync(packet, cancellationToken);
        return (identification, ReadNameLists(packet));
    }

    /// <inheritdoc/>
    public void Dispose() => listener.Stop();

    // padding_length (1), SSH_MSG_KEXINIT (1, value 20), cookie (16), then the name-lists.
    private static List<string> ReadNameLists(byte[] packet)
    {
        Assert.AreEqual(20, packet[1], "the client's first packet is not a KEXINIT");
        var offset = 1 + 1 + 16;
        var nameLists = new List<string>(NameListCount);
        for (var index = 0; index < NameListCount; index++)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset, 4));
            nameLists.Add(Encoding.ASCII.GetString(packet, offset + 4, length));
            offset += 4 + length;
        }

        return nameLists;
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        var next = new byte[1];
        while (true)
        {
            await stream.ReadExactlyAsync(next, cancellationToken);
            if (next[0] == (byte)'\n')
            {
                return Encoding.ASCII.GetString([.. line]).TrimEnd('\r');
            }

            line.Add(next[0]);
        }
    }
}
