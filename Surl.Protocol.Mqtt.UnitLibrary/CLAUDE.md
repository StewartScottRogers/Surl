# Surl.Protocol.Mqtt.UnitLibrary

Phase 1, alongside HTTP.

The MQTT server (MQTT 3.1.1), `MqttProtocolServer`: accepts the connect, the subscribe
(curl's plain fetch) and the publish (curl's `-d`) upstream curl sends. Every publish is
kept as its topic's retained message in `MqttRetainedMessages`; a subscribe is answered
with the retained messages its filters match, then `DISCONNECT`. ADR-0014 records every
answer.

Every `CONNECT`'s user name and password (the will is read and skipped) go to the
`IAuthenticationPolicy` from `Surl.Protocol.Abstractions`, with the connection's
`TlsSession`; the server maps its verdict to `CONNACK` 0, 4 or 5 and closes after a refusal
(ADR-0032 decision 5). The server decides nothing about accounts or plain text itself. The
constructor without a policy passes `AnonymousAuthenticationPolicy` until BL-117 composes
`surl` with the real one.

`MqttRetainedMessages` made by its constructor lives in memory only; `surl` makes it so
when no `--directory` is given. Made by
`MqttRetainedMessages.LoadAsync(MqttRetainedMessageFile)`, it starts with what the file
holds and `SaveChangesAsync` (called after every publish) rewrites the whole file through a
temporary name renamed into place (ADR-0031 decision 6). With `--directory <path>`, `surl`
points the `MqttRetainedMessageFile` at `<path>/.surl/mqtt`, so the file is
`<path>/.surl/mqtt/retained-messages` (`MqttRetainedMessageFile.FileName`). The file is
read and written only through `Surl.Content`'s `IContentFileSystem`; nothing here calls
`System.IO.File` or `Directory`.

**URL schemes answered:** `mqtt` and `mqtts` (`MqttProtocolServer.Schemes`). `mqtts` is
the same server over implicit TLS: the engine performs the handshake and hands the server
a connection that already carries plaintext (ADR-0010), so nothing here constructs an
`SslStream` or calls `UpgradeToTlsAsync`, and an `mqtts` exchange is answered exactly as
an `mqtt` one.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`, ADR-0031), and may also reference
the other horizontal library ADR-0002 lists (`Surl.Cryptography.UnitLibrary`) - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
