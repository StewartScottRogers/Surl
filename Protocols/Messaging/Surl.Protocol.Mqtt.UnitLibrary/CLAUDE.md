# Surl.Protocol.Mqtt.UnitLibrary

Phase 1, alongside HTTP.

The MQTT server (MQTT 3.1.1), `MqttProtocolServer`: accepts the connect, the subscribe
(curl's plain fetch) and the publish (curl's `-d`) upstream curl sends. Every publish is
published to the one `Surl.MessageBus` the server is given, retained (`BusMessage.Retain`)
only when its `RETAIN` flag is 1 (MQTT 3.1.1 section 3.3.1.3, ADR-0176; a `--topic <filter>;retain`
declaration makes the bus retain a matching one regardless); a RETAIN 0 publish, all upstream curl sends,
reaches live subscribers and neither replaces nor removes a retained message; a subscribe is answered with the bus's retained messages its
filters match (`MqttBusRetainedMessages`, which subscribes to each filter only for the moment
it reads them), then `DISCONNECT`, sent because upstream curl 8.21.0 exits 56 on a bare close.
The server keeps no retained messages of its own (BL-635). ADR-0014 records every answer.

The held subscription (ADR-0014 Amendment 1, BL-1720): `MqttProtocolServer`'s optional third
constructor value, a publish count, absent by default (`surl` gives it `--mqtt-hold-subscription`'s,
BL-1721). With it a `SUBSCRIBE` is answered as above but stays open (`MqttHeldSubscription`,
`MqttPacketResponder.HeldSubscription.cs`): the bus subscribes all its filters in one step
(`BusConnection.SubscribeTogether`, BL-1732), before `SUBACK`, so a retained publish made meanwhile
is sent once, never both retained and live; each later publish
is sent once per connection (`BusConnection.AfterMessageDelivered`) as a `PUBLISH` `0x30` while the
client's packets go on being read; `DISCONNECT` follows the count-th `PUBLISH`, retained ones
counted, never for count 0; a first `SUBSCRIBE` with every filter refused is ended with
`DISCONNECT`; more than 1000 publishes or 16 MiB waiting closes with no bytes and a log note. Keep
Alive is not enforced: curl sends no `PINGREQ` while publishes keep arriving. Only the connection's
own loop writes, so no two writes run at once, and one read runs beside them.

Topic names and filters are mapped with the bus's `TopicNameLookup` (`MessageBus.Names`) for the
exchange's scheme: MQTT's names are the bus's as they stand (`MqttTopicNames`, BL-630, which also
checks each filter), with each `--topic-map` alias naming `mqtt` or `mqtts` on its left applied
both ways (ADR-0102 decision 3.3, BL-1705). A wildcard filter gets no alias, and a retained
message on a topic the map hides from the scheme is not sent. The `CONNECT`'s will is skipped, so
there is no will topic to map.

Every `CONNECT`'s user name and password (the will is read and skipped) go to the
`IAuthenticationPolicy` from `Surl.Protocol.Abstractions`, with the connection's
`TlsSession`; the server maps its verdict to `CONNACK` 0, 4 or 5 and closes after a refusal
(ADR-0032 decision 5). The server decides nothing about accounts or plain text itself. Its only
constructor takes the policy, so no server is made without one; tests pass
`AnonymousAuthenticationPolicy`.

Every publish and subscribe goes to the bus with the connection's account - the `CONNECT`'s
user name when the policy answered `Accepted`, otherwise anonymous (a login `AcceptedUnchecked`
proves no account) - so the bus's `--topic-access` grants decide it (ADR-0102 decision 3.4,
BL-665). A refused filter is `SUBACK` return code `0x80`, then the usual `DISCONNECT` (with a hold
count, below, `DISCONNECT` follows only when every filter of the first `SUBSCRIBE` is refused); a refused
publish is dropped (nothing retained or delivered), noted, and acknowledged as any other at its
QoS, since MQTT 3.1.1 has no negative acknowledgement, and the connection goes on.

The chosen-error fault (ADR-0124 decision 4, ADR-0139; `MqttPacketResponder.Faults.cs`): the
exchange's `IFaultPlan` is asked at an accepted `CONNECT`, at each valid `SUBSCRIBE` filter (the
filter as sent as its path) and at each `PUBLISH` (the bus's topic, a `--topic-map` alias applied, as
its path), with the account the login proved, before the bus. A matching `error` rule's reason code
128 to 255 is answered `CONNACK` with the 3.1.1 code of the same meaning (else 3) and a close, `SUBACK` `0x80` for that filter, or a close with nothing
published; each publishes one `FaultAppliedEvent`. A `delay` or `drop` rule there is applied through
`CommandStageFaults` (ADR-0141, BL-920): a delay holds the answer its `ms` with the idle clock paused, a
drop aborts the connection with no answer and nothing published. A reply that does not fit, or another
kind, is noted and not applied.

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
`SUBSCRIBE` before the server reads it. `MqttUpstreamTestCaseReplies` also takes the case's `<servercmd>`
lines (ADR-0155, BL-1139): `error-CONNACK`, `remlen-CONNACK` and `PINGRESP-as-CONNACK` make the server read the
`CONNECT`, write their four bytes (`ConnectAnswer`) straight to the connection, log them and end it. The other four (BL-1140: `PUBLISH-before-SUBACK`, `short-PUBLISH`, `DISCONNECT-malformed`,
`excessive-remaining`) make `AnswerSubscribeWithScriptAsync` answer `CONNECT` with a normal `CONNACK`, read the `SUBSCRIBE`,
and write the case's packets in ADR-0155 decisions 4 to 7's order (the broken ones straight to the connection, logged by
the half itself); `short-PUBLISH` implies `PUBLISH-before-SUBACK`.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).

The exchange log never holds a `CONNECT` password (ADR-0182, BL-1433): `MqttProtocolServer` implements
`IExchangeBytesRedactingServer`, and `MqttConnectBytesRedactor` writes the first packet with its password
field as length 4 and `****`, every other byte and every later packet as received.
