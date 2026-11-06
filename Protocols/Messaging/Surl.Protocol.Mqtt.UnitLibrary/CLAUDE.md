# Surl.Protocol.Mqtt.UnitLibrary

Phase 1, alongside HTTP.

The MQTT server (MQTT 3.1.1), `MqttProtocolServer`: accepts the connect, the subscribe
(curl's plain fetch) and the publish (curl's `-d`) upstream curl sends. Every publish is
published to the one `Surl.MessageBus` the server is given, retained (`BusMessage.Retain`)
whatever its `RETAIN` flag; a subscribe is answered with the bus's retained messages its
filters match (`MqttBusRetainedMessages`, which subscribes to each filter only for the moment
it reads them), then `DISCONNECT`. Topic names and filters are checked and mapped with the
bus's `MqttTopicNames` (BL-630). The server keeps no retained messages of its own (BL-635).
ADR-0014 records every answer.

Every `CONNECT`'s user name and password (the will is read and skipped) go to the
`IAuthenticationPolicy` from `Surl.Protocol.Abstractions`, with the connection's
`TlsSession`; the server maps its verdict to `CONNACK` 0, 4 or 5 and closes after a refusal
(ADR-0032 decision 5). The server decides nothing about accounts or plain text itself. Its only
constructor takes the policy, so no server is made without one; tests pass
`AnonymousAuthenticationPolicy`.

Every publish and subscribe goes to the bus with the connection's account - the `CONNECT`'s
user name when the policy answered `Accepted`, otherwise anonymous (a login `AcceptedUnchecked`
proves no account) - so the bus's `--topic-access` grants decide it (ADR-0102 decision 3.4,
BL-665). A refused filter is `SUBACK` return code `0x80`, then the usual `DISCONNECT`; a refused
publish is dropped (nothing retained or delivered), noted, and acknowledged as any other at its
QoS, since MQTT 3.1.1 has no negative acknowledgement, and the connection goes on.

After every publish the server calls the bus's `SaveChangesAsync`; a `StorageFailed` result
is noted in the exchange log and the change stays in memory. Whether anything is written is
the bus's: `Surl.Composition` composes one bus per run and loads it from `<path>/.surl` with
`--directory` (an old `<path>/.surl/mqtt/retained-messages` is read when the bus has no
`retained` file), and in memory only without it. Nothing here touches a file.

**URL schemes answered:** `mqtt` and `mqtts` (`MqttProtocolServer.Schemes`). `mqtts` is
the same server over implicit TLS: the engine performs the handshake and hands the server
a connection that already carries plaintext (ADR-0010), so nothing here constructs an
`SslStream` or calls `UpgradeToTlsAsync`, and an `mqtts` exchange is answered exactly as
an `mqtt` one.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.MessageBus.UnitLibrary` (ADR-0100 decision 3.1), and may also reference
the other horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it -
nothing else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

The server half of upstream curl's test cases (ADR-0080 decision 2, BL-396), never composed by
the shipped `surl`: `MqttUpstreamTestCaseServer` is `MqttProtocolServer` on a bus of its own per
connection, its connection wrapped in `MqttPacketLoggingConnection`, which cuts each direction
into packets with `MqttPacketCutter`, logs them in `MqttUpstreamTestCaseReplies` in runtests'
`client CONNECT 18 0004...` form, and publishes the case's `<data>`, retained, to each topic of a
`SUBSCRIBE` before the server reads it. No MQTT `<servercmd>` is implemented.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
