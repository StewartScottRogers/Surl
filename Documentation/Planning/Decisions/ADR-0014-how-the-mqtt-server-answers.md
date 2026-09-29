# ADR-0014 — How the MQTT server answers

- **Status:** Accepted
- **Superseded in part:** decision 3's "a will, user name and password the flags allow are accepted and not read" is superseded by [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md): the user name and password are checked.
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28, in BL-036

## Context

BL-036 asks for an MQTT server in `Surl.Protocol.Mqtt.UnitLibrary` that completes the
exchanges pinned upstream curl 8.21.0 performs for `curl mqtt://host/topic` (a subscribe)
and `curl -d payload mqtt://host/topic` (a publish). It asks the plan to decide what a
subscriber receives and how a `CONNECT` with an unsupported protocol level is refused, and
to choose a packet-size limit.

Measured with `Record-CurlExchange.ps1 -Raw` against the pinned win-x64 build (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), 2026-09-28:

| Server sends | curl arguments | curl sends | Exit code, stderr |
| --- | --- | --- | --- |
| nothing | `mqtt://127.0.0.1:P/t` | `CONNECT`: `MQTT`, level 4, flags `0x02` (CleanSession only), keep-alive 60, client identifier `curl` and 8 random characters | 56, `Connection disconnected` |
| `CONNACK` 0 | `mqtt://127.0.0.1:P/t` | `SUBSCRIBE` `82 06 00 01 00 01 74 00`: packet identifier 1, filter `t`, QoS 0 | 56 once the server closes |
| `CONNACK` 0, `SUBACK`, `PUBLISH` (`30`) of `hi` on `t`, then close | `mqtt://127.0.0.1:P/t` | nothing more; stdout `00 01 74 68 69` | 56, `Connection disconnected` |
| as above, then `DISCONNECT` `E0 00` | `mqtt://127.0.0.1:P/t` | closes; stdout `00 01 74 68 69` | 0 |
| as above with `PUBLISH` flags `31` (RETAIN set) | `mqtt://127.0.0.1:P/t` | closes; same stdout | 0 |
| `CONNACK` 0, `SUBACK`, `DISCONNECT` | `mqtt://127.0.0.1:P/t` | closes; empty stdout | 0 |
| `CONNACK` 0, `SUBACK`, two `PUBLISH`es, `DISCONNECT` | `mqtt://127.0.0.1:P/a%2F%2B` (filter `a/+`) | closes; stdout both messages, each as topic length, topic, payload | 0 |
| `CONNACK` 0 | `-d hi mqtt://127.0.0.1:P/t` | `PUBLISH` `30 05 00 01 74 68 69` (QoS 0, RETAIN clear), then `DISCONNECT`, then closes, without waiting for anything | 0 |
| `CONNACK` 1 | `mqtt://127.0.0.1:P/t` | closes | 8, `Expected 0000 but got 0001` |

So upstream curl prints every `PUBLISH` it receives as the packet's whole variable header
and payload (topic length, topic, payload), keeps reading after one, and ends its fetch
with exit 0 only when the server sends `DISCONNECT`; a bare close is exit 56. A publish
needs only the `CONNACK`.

## Decision

1. **Every published message is kept, per topic, as that topic's retained message**,
   whatever its `RETAIN` flag says. Upstream curl publishes with `RETAIN` clear; a broker
   that honoured the flag would hand a later `curl mqtt://…/t` nothing, and `surl` would
   be useless as curl's mate. A later publish to the topic replaces the message and an
   empty payload removes it, as a zero-byte retained message does (MQTT 3.1.1, section
   3.3.1.3). The messages live in an `MqttRetainedMessages` given to the server, in memory,
   shared by every connection it answers and gone when it stops. Topics compare ordinally.
2. **A subscribe is one-shot.** `SUBSCRIBE` is answered `SUBACK`, granting QoS 0 to every
   valid filter (a server may grant less than asked, section 3.9.3) and `0x80` to every
   invalid one; then a QoS 0 `PUBLISH`, `RETAIN` set (section 3.3.1.3), for each retained
   message a valid filter matches, each topic once, in ordinal order of topic; then
   `DISCONNECT`, and the connection is closed. MQTT 3.1.1 defines `DISCONNECT` only from
   client to server, but it is the one thing that makes upstream curl end a fetch with exit
   0, and curl is the oracle (ADR-0003). A subscriber is never held open waiting for a
   future publish: curl's fetch is a read of what the topic holds now, and with nothing
   retained it gets `SUBACK` and `DISCONNECT` and exits 0 with empty output, the way an
   empty file is fetched.
3. **Unsupported protocol level.** `CONNECT` with protocol name `MQTT` at any level but 4,
   or `MQIsdp` (MQTT 3.1), is answered `CONNACK` return code 1, unacceptable protocol
   version, and closed (section 3.1.2.2). Any other protocol name, connect flags section
   3.1.2 forbids (the reserved bit, will QoS 3, will QoS or will retain without the will
   flag, a password without a user name), or a field cut short closes with no reply. An
   empty client identifier without CleanSession is answered `CONNACK` 2 (section 3.1.3.1).
   A will, user name and password the flags allow are accepted and not read; the server
   keeps no session.
4. **The rest of MQTT 3.1.1 a client may send** is answered too, although curl sends none
   of it: QoS 1 `PUBLISH` gets `PUBACK`, QoS 2 `PUBREC`, `PUBREL` `PUBCOMP`, `UNSUBSCRIBE`
   `UNSUBACK`, `PINGREQ` `PINGRESP`, and `DISCONNECT` closes. A malformed packet (among
   them DUP on a QoS 0 `PUBLISH`, packet identifier 0, an empty `UNSUBSCRIBE` filter),
   wrong fixed header flags, a packet only a server sends, a packet of reserved type 0 or
   15, a first packet other than `CONNECT` and a second `CONNECT` close with no reply, as
   section 4.8 says.
5. **Packet size.** ADR-0006 exists, so its numbers hold: a packet whose fixed header
   announces more than `ExchangeLimits.MaxMessageBytes` (default 1 MiB), fixed header
   included, and a remaining length whose fourth byte still has its continuation bit set,
   close with no bytes (ADR-0006, section 5, "MQTT closes without a reply"). The reader
   reads the fixed header a byte at a time and the body exactly, so an over-limit packet
   is refused before any body byte is read. The body's buffer starts at 64 KiB at most and
   doubles as bytes arrive, so with `--max-message 0` a remaining length the peer announces
   but never sends does not make the server allocate it. The first-packet head timeout and the
   `PUBLISH` payload limit (`MaxUploadBytes`), with curl's recorded reaction to each close,
   are BL-053.
6. **Scheme.** The server declares `mqtt` only. `mqtts` is the same server behind implicit
   TLS (ADR-0002), which the serving engine performs (BL-065); declaring it is a follow-up
   task (BL-068).
7. **The retained messages are bounded**, because any peer can publish and they outlive
   every connection: at most 10000 topics and 104857600 payload bytes (100 MiB, ADR-0006's
   upload default) in all, both constructor parameters of `MqttRetainedMessages`. A
   `PUBLISH` that would take the store past either is not kept and closes the connection
   with no reply, as ADR-0006 section 5 closes MQTT for every limit; a replacement counts
   only the difference, and an empty payload frees its topic. Wiring the bounds to `surl`
   options, if wanted, is BL-042's composition's to decide.

## Consequences

- `curl -d hi mqtt://host/t` followed by `curl mqtt://host/t` against one `surl` prints
  `00 01 74 68 69` and exits 0 both times; BL-042 proves it live.
- Because the output of a subscribe carries the topic before each payload, a user who
  wants only the payload strips it; that is upstream curl's output format, not Surl's.
- A long-lived MQTT subscriber (another broker client) gets the retained messages and is
  then disconnected, so `surl` is not a general-purpose broker. That is a deliberate
  limit of a server built to answer curl.
