#:project Surl.Kerberos.TestKdc.UnitLibrary/Surl.Kerberos.TestKdc.UnitLibrary.csproj
#:project Surl.Networking.UnitLibrary/Surl.Networking.UnitLibrary.csproj

// Runs the hand-built loopback KDC of ADR-0065 (Surl.Kerberos.TestKdc) for
// Record-CurlExchange.ps1 -KerberosTestKdc (decision 4):
//
//   dotnet run Run-KerberosTestKdc.cs -- --password <password> --service HTTP/web.surl.test
//       [--service ...] [--user tester] [--port 88] [--keytab service.keytab] [--log kdc.log]
//
// It writes the service principals' keys as an MIT keytab, binds 127.0.0.1:<port> over UDP and
// TCP, prints "listening on 127.0.0.1:<port>" on standard output, and serves until its standard
// input closes. Each AS or TGS exchange appends one line to the log:
//
//   <AS|TGS> <client principal> <server principal> <ticket enctype, or -> <error code, 0 for none>
//
// A request the KDC could not read is logged with - for each field it lacks.
using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using Surl.Kerberos;
using Surl.Kerberos.TestKdc;
using Surl.Networking;
using Surl.Protocol.Abstractions;

TestKdcArguments arguments = TestKdcArguments.Parse(args);
if (arguments.Password is null || arguments.ServicePrincipals.Count == 0)
{
    await Console.Error.WriteLineAsync("Run-KerberosTestKdc.cs needs --password and at least one --service.");
    return 2;
}

int port = arguments.Port;
KerberosTestKdc kdc = new(arguments.UserName, arguments.Password, arguments.ServicePrincipals, TimeProvider.System, new CryptographicRandomSource());
await File.WriteAllBytesAsync(arguments.KeytabPath, kdc.WriteServiceKeytab());
KdcExchangeLog exchangeLog = new(arguments.LogPath);

SocketListenerFactory listenerFactory = new();
ListenUrl listenUrl = new("kerberos", "127.0.0.1", port);
await using IDatagramListener datagramListener = await listenerFactory.StartDatagramListenerAsync(listenUrl, CancellationToken.None);
await using IConnectionListener connectionListener = await listenerFactory.StartConnectionListenerAsync(listenUrl, CancellationToken.None);
using CancellationTokenSource stopping = new();
Task serving = new KerberosTestKdcServer(kdc).ServeListenersAsync(
    new LoggedDatagramListener(datagramListener, exchangeLog),
    new LoggedConnectionListener(connectionListener, exchangeLog),
    stopping.Token);
Console.WriteLine($"listening on 127.0.0.1:{port}");
Console.Out.Flush();

// Standard input closing is the stop signal: Record-CurlExchange.ps1 closes it once curl exits.
await Console.In.ReadToEndAsync();
await stopping.CancelAsync();
await Task.WhenAny(serving);
return 0;

/// <summary>The command line: every option takes one value, and --service may repeat.</summary>
internal sealed class TestKdcArguments
{
    public string UserName { get; private set; } = "tester";

    public string? Password { get; private set; }

    public int Port { get; private set; } = 88;

    public string KeytabPath { get; private set; } = "service.keytab";

    public string LogPath { get; private set; } = "kdc.log";

    public List<string> ServicePrincipals { get; } = [];

    public static TestKdcArguments Parse(string[] args)
    {
        TestKdcArguments arguments = new();
        for (int index = 0; index < args.Length; index += 2)
        {
            string value = index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"{args[index]} needs a value.");
            arguments.Apply(args[index], value);
        }

        return arguments;
    }

    private void Apply(string option, string value)
    {
        switch (option)
        {
            case "--user": UserName = value; break;
            case "--password": Password = value; break;
            case "--port": Port = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
            case "--keytab": KeytabPath = value; break;
            case "--log": LogPath = value; break;
            case "--service": ServicePrincipals.Add(value); break;
            default: throw new ArgumentException($"Unknown argument {option}.");
        }
    }
}

/// <summary>Keys and confounders from the operating system's cryptographic generator.</summary>
internal sealed class CryptographicRandomSource : IKerberosRandomSource
{
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}

/// <summary>
/// Appends one line per exchange to kdc.log, read from the request's and the answer's DER
/// (RFC 4120 section 5): AS-REQ [APPLICATION 10], TGS-REQ [APPLICATION 12], AS-REP [APPLICATION
/// 11], TGS-REP [APPLICATION 13] and KRB-ERROR [APPLICATION 30].
/// </summary>
internal sealed class KdcExchangeLog
{
    private const int TgsRequestTag = 12;
    private const int ErrorTag = 30;
    private readonly string path;
    private readonly Lock writing = new();

    public KdcExchangeLog(string path)
    {
        this.path = path;
        File.WriteAllText(path, string.Empty);
    }

    public void Record(ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> answer)
    {
        (int requestTag, string client, string server) = ReadRequest(request);
        (int answerTag, string encryptionType, int errorCode) = ReadAnswer(answer);
        string exchange = requestTag == TgsRequestTag ? "TGS" : "AS";
        string line = $"{exchange} {client} {server} {(answerTag == ErrorTag ? "-" : encryptionType)} {errorCode}{Environment.NewLine}";
        lock (writing)
        {
            File.AppendAllText(path, line);
        }
    }

    private static (int Tag, string Client, string Server) ReadRequest(ReadOnlyMemory<byte> request)
    {
        try
        {
            AsnReader application = new(request, AsnEncodingRules.DER);
            Asn1Tag tag = application.PeekTag();
            AsnReader fields = application.ReadSequence(tag).ReadSequence();
            AsnReader? body = FindField(fields, 4);
            if (body is null)
            {
                return (tag.TagValue, "-", "-");
            }

            AsnReader bodyFields = body.ReadSequence();
            string? clientName = null;
            string? realm = null;
            string? serverName = null;
            while (bodyFields.HasData)
            {
                Asn1Tag fieldTag = bodyFields.PeekTag();
                AsnReader field = bodyFields.ReadSequence(fieldTag);
                switch (fieldTag.TagValue)
                {
                    case 1: clientName = ReadPrincipalName(field); break;
                    case 2: realm = ReadGeneralString(field); break;
                    case 3: serverName = ReadPrincipalName(field); break;
                    default: break;
                }
            }

            return (tag.TagValue, clientName is null ? "-" : $"{clientName}@{realm}", serverName is null ? "-" : $"{serverName}@{realm}");
        }
        catch (AsnContentException)
        {
            return (0, "-", "-");
        }
    }

    private static (int Tag, string EncryptionType, int ErrorCode) ReadAnswer(ReadOnlyMemory<byte> answer)
    {
        try
        {
            AsnReader application = new(answer, AsnEncodingRules.DER);
            Asn1Tag tag = application.PeekTag();
            AsnReader fields = application.ReadSequence(tag).ReadSequence();
            if (tag.TagValue == ErrorTag)
            {
                AsnReader? errorCode = FindField(fields, 6);
                return (tag.TagValue, "-", errorCode is null ? -1 : (int)errorCode.ReadInteger());
            }

            // KDC-REP's ticket [5] is a Ticket [APPLICATION 1], whose enc-part [3] names its etype [0].
            AsnReader? ticket = FindField(fields, 5);
            AsnReader? ticketEncryptedPart = ticket is null ? null : FindField(ticket.ReadSequence(new Asn1Tag(TagClass.Application, 1)).ReadSequence(), 3);
            AsnReader? encryptionType = ticketEncryptedPart is null ? null : FindField(ticketEncryptedPart.ReadSequence(), 0);
            return (tag.TagValue, encryptionType is null ? "-" : encryptionType.ReadInteger().ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
        }
        catch (AsnContentException)
        {
            return (0, "-", -1);
        }
    }

    private static AsnReader? FindField(AsnReader fields, int contextTag)
    {
        while (fields.HasData)
        {
            Asn1Tag fieldTag = fields.PeekTag();
            AsnReader field = fields.ReadSequence(fieldTag);
            if (fieldTag.TagClass == TagClass.ContextSpecific && fieldTag.TagValue == contextTag)
            {
                return field;
            }
        }

        return null;
    }

    private static string ReadPrincipalName(AsnReader field)
    {
        AsnReader? nameString = FindField(field.ReadSequence(), 1);
        if (nameString is null)
        {
            return "-";
        }

        AsnReader components = nameString.ReadSequence();
        List<string> parts = [];
        while (components.HasData)
        {
            parts.Add(ReadGeneralString(components));
        }

        return string.Join('/', parts);
    }

    // AsnReader decodes no GeneralString as text, so a KerberosString's content is read as bytes.
    private static string ReadGeneralString(AsnReader reader)
    {
        ReadOnlyMemory<byte> encoded = reader.ReadEncodedValue();
        AsnDecoder.ReadEncodedValue(encoded.Span, AsnEncodingRules.DER, out int contentOffset, out int contentLength, out _);
        return System.Text.Encoding.UTF8.GetString(encoded.Span.Slice(contentOffset, contentLength));
    }
}

/// <summary>Hands the KDC each UDP flow wrapped so its one request and answer are logged.</summary>
internal sealed class LoggedDatagramListener(IDatagramListener inner, KdcExchangeLog exchangeLog) : IDatagramListener
{
    public ListenUrl ListenUrl => inner.ListenUrl;

    public IReadOnlyList<EndPoint> BoundEndPoints => inner.BoundEndPoints;

    public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken) =>
        new LoggedDatagramFlow(await inner.AcceptFlowAsync(cancellationToken), exchangeLog);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A UDP flow whose first datagram and the answer sent to it are logged.</summary>
internal sealed class LoggedDatagramFlow(IDatagramFlow inner, KdcExchangeLog exchangeLog) : IDatagramFlow
{
    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public ReadOnlyMemory<byte> FirstDatagram => inner.FirstDatagram;

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => inner.ReceiveAsync(cancellationToken);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        exchangeLog.Record(inner.FirstDatagram, datagram);
        return inner.SendAsync(datagram, cancellationToken);
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => inner.MoveToNewLocalPortAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

/// <summary>Hands the KDC each TCP connection wrapped so its one request and answer are logged.</summary>
internal sealed class LoggedConnectionListener(IConnectionListener inner, KdcExchangeLog exchangeLog) : IConnectionListener
{
    public ListenUrl ListenUrl => inner.ListenUrl;

    public IReadOnlyList<EndPoint> BoundEndPoints => inner.BoundEndPoints;

    public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken) =>
        new LoggedConnection(await inner.AcceptAsync(cancellationToken), exchangeLog);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// A TCP connection that keeps what it read and wrote and, once the KDC half-closes it, logs the
/// request and answer without their four-byte length prefixes (RFC 4120 section 7.2.2).
/// </summary>
internal sealed class LoggedConnection(IConnection inner, KdcExchangeLog exchangeLog) : IConnection
{
    private const int LengthPrefixLength = 4;
    private readonly MemoryStream read = new();
    private readonly MemoryStream written = new();

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = await inner.ReadAsync(buffer, cancellationToken);
        read.Write(buffer.Span[..count]);
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        written.Write(bytes.Span);
        return inner.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        if (read.Length >= LengthPrefixLength && written.Length >= LengthPrefixLength)
        {
            exchangeLog.Record(read.ToArray().AsMemory(LengthPrefixLength), written.ToArray().AsMemory(LengthPrefixLength));
        }

        return inner.CompleteWritesAsync(cancellationToken);
    }

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        read.Dispose();
        written.Dispose();
        return inner.DisposeAsync();
    }
}
