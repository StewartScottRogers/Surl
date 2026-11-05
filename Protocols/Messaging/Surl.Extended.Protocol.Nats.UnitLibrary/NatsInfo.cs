using System.Buffers;
using System.Net;
using System.Text.Json;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The INFO line the server sends first on every connection (the protocol page, "INFO"; ADR-0113
/// decision 1): compact JSON, its fields in the ADR's order, nothing about the build.
/// </summary>
/// <param name="ServerId">The server's 22-character identity, sent as <c>server_id</c> and <c>server_name</c>.</param>
/// <param name="LocalEndPoint">What the client connected to, sent as <c>host</c> and <c>port</c>.</param>
/// <param name="RemoteEndPoint">The client, sent as <c>client_ip</c>.</param>
/// <param name="ClientId">The connection's number in accept order, from 1.</param>
/// <param name="MaxPayload">The largest payload the server accepts.</param>
/// <param name="AuthRequired">Whether CONNECT must carry credentials.</param>
/// <param name="TlsRequired">Whether the connection must run TLS before CONNECT: <c>natss</c> (ADR-0113 decision 6).</param>
internal sealed record NatsInfo(
    string ServerId,
    EndPoint LocalEndPoint,
    EndPoint RemoteEndPoint,
    ulong ClientId,
    long MaxPayload,
    bool AuthRequired,
    bool TlsRequired)
{
    /// <summary>The NATS server version whose semantics the server follows; never surl's version.</summary>
    public const string ServedVersion = "2.10.0";

    /// <summary>The protocol level announced: asynchronous INFO is understood.</summary>
    public const int ServedProtocol = 1;

    private static readonly byte[] Prefix = "INFO "u8.ToArray();
    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    /// <summary>The whole line, <c>INFO {...}</c> CR LF.</summary>
    public byte[] Encode()
    {
        var line = new ArrayBufferWriter<byte>();
        line.Write(Prefix);
        using (var json = new Utf8JsonWriter(line))
        {
            WriteFields(json);
        }

        line.Write(LineEnd);
        return line.WrittenSpan.ToArray();
    }

    private void WriteFields(Utf8JsonWriter json)
    {
        var (host, port) = AddressAndPort(LocalEndPoint);
        json.WriteStartObject();
        json.WriteString("server_id", ServerId);
        json.WriteString("server_name", ServerId);
        json.WriteString("version", ServedVersion);
        json.WriteNumber("proto", ServedProtocol);
        json.WriteString("host", host);
        json.WriteNumber("port", port);
        json.WriteBoolean("headers", true);
        json.WriteNumber("max_payload", MaxPayload);
        json.WriteNumber("client_id", ClientId);
        json.WriteString("client_ip", AddressAndPort(RemoteEndPoint).Address);
        json.WriteBoolean("auth_required", AuthRequired);
        json.WriteBoolean("tls_required", TlsRequired);
        json.WriteBoolean("tls_verify", false);
        json.WriteBoolean("tls_available", false);
        json.WriteEndObject();
    }

    // An IP address without brackets and its port; an end point with no IP address (a Unix
    // socket) has neither.
    private static (string Address, int Port) AddressAndPort(EndPoint endPoint) =>
        endPoint is IPEndPoint ip ? (ip.Address.ToString(), ip.Port) : (string.Empty, 0);
}
