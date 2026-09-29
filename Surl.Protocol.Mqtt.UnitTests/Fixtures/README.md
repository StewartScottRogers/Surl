# MQTT fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw` (BL-029). Never from the Curl port (ADR-0003).

Each case was fed the exact packets `MqttProtocolServer` sends for it before any test
pinned them: `CONNACK` 0 once curl's `CONNECT` had arrived, then, for a subscribe,
everything Surl answers to curl's `SUBSCRIBE` in one burst (`SUBACK`, the retained
`PUBLISH`es, `DISCONNECT`). Every case in the first table below exited 0 with an empty `stderr.txt`. ADR-0014 records
why the subscribe ends with `DISCONNECT`: without it the pinned build exits 56.

Each folder holds the recorder's five files - `request.bin` (the bytes curl sent),
`transcript.txt` (both directions), `stdout.bin`, `stderr.txt` and `exitcode.txt` - and
`reply.bin`, every byte the server sent, in order: the recorder's `-RawReply` values below
decoded and joined, the same bytes as the transcript's `<` lines. curl's `CONNECT` carries
a random client identifier, so `request.bin` differs from one recording to the next; the
server ignores it. The files are embedded resources of `Surl.Protocol.Mqtt.UnitTests`, so
the tests read them without touching the file system. `.gitattributes` here keeps git from
rewriting their bytes. `MqttProtocolServerTests` replays each `request.bin` against the
retained messages the table names and asserts the bytes Surl writes equal `reply.bin`.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell, with
`$ca = '\x20\x02\x00\x00'` (`CONNACK` 0), `$y = 'y' * 200` and `$x = 'x' * 200`:

| Folder | Retained before | Command line |
| --- | --- | --- |
| `subscribe-t` | `t` = `hi` | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca,'\x90\x03\x00\x01\x00\x31\x05\x00\x01thi\xE0\x00' -CurlArgs '-sS','mqtt://127.0.0.1:18883/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\subscribe-t` |
| `subscribe-200-bytes` | `t` = 200 × `y` | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca,('\x90\x03\x00\x01\x00\x31\xCB\x01\x00\x01t' + $y + '\xE0\x00') -CurlArgs '-sS','mqtt://127.0.0.1:18883/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\subscribe-200-bytes` |
| `subscribe-nothing-retained` | nothing | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca,'\x90\x03\x00\x01\x00\xE0\x00' -CurlArgs '-sS','mqtt://127.0.0.1:18883/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\subscribe-nothing-retained` |
| `subscribe-wildcard` | `a/1` = `hi`, `a/2` = `yo`, `b/1`, `a/1/x` | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca,'\x90\x03\x00\x01\x00\x31\x07\x00\x03a/1hi\x31\x07\x00\x03a/2yo\xE0\x00' -CurlArgs '-sS','mqtt://127.0.0.1:18883/a%2F%2B' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\subscribe-wildcard` |
| `publish-hi` | nothing | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca -CurlArgs '-sS','-d','hi','mqtt://127.0.0.1:18883/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\publish-hi` |
| `publish-200-bytes` | nothing | `.\Record-CurlExchange.ps1 -Port 18883 -Raw -RawReply $ca -CurlArgs '-sS','-d',$x,'mqtt://127.0.0.1:18883/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\publish-200-bytes` |

The two 200-byte cases carry a remaining length of 203, encoded in two bytes as
`CB 01` (MQTT 3.1.1, section 2.2.3): outbound in Surl's `PUBLISH` for
`subscribe-200-bytes`, inbound in curl's `PUBLISH` for `publish-200-bytes`.

## Closes (BL-053)

What the pinned build reports when the server closes with no reply, as `MqttProtocolServer`
does for each hardening limit (ADR-0006, section 5). Recorded on 2026-09-29 from the
repository root, in Windows PowerShell, with the same build and SHA-256 as above, `$ca` as
above, `$tmp\big.bin` 1048576 bytes of `x` and `$tmp\small.bin` 201 bytes of `x`, each with
no line ending. A `-Raw` recording with no further reply closes once curl goes idle, which is
what Surl's close looks like from curl's side. `reply.bin` holds what the server sent: nothing
before `CONNACK`, or `CONNACK` 0 alone.

| Folder | curl reported | Command line |
| --- | --- | --- |
| `closed-before-connack` | exit 56, `curl: (56) Connection disconnected` | `.\Record-CurlExchange.ps1 -Port 18853 -Raw -CurlArgs '-sS','mqtt://127.0.0.1:18853/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\closed-before-connack` |
| `publish-closed-before-connack` | exit 56, `curl: (56) Connection disconnected` | `.\Record-CurlExchange.ps1 -Port 18853 -Raw -CurlArgs '-sS','-d','hi','mqtt://127.0.0.1:18853/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\publish-closed-before-connack` |
| `packet-over-the-limit` | exit 0, empty stderr | `.\Record-CurlExchange.ps1 -Port 18853 -Raw -RawReply $ca -CurlArgs '-sS','-d',"@$tmp\big.bin",'mqtt://127.0.0.1:18853/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\packet-over-the-limit` |
| `publish-payload-over-the-limit` | exit 0, empty stderr | `.\Record-CurlExchange.ps1 -Port 18853 -Raw -RawReply $ca -CurlArgs '-sS','-d',"@$tmp\small.bin",'mqtt://127.0.0.1:18853/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\publish-payload-over-the-limit` |

A close before `CONNACK` - a head timeout, and the bare close a connection refusal gets - is
reported as exit 56. A close after the `PUBLISH` of `-d` is not reported at all: curl sends a
QoS 0 `PUBLISH` and exits 0 without waiting for any reply. `packet-over-the-limit` is a
`PUBLISH` of 1048583 bytes (remaining length `83 80 40`), over the default 1048576-byte
packet limit; `publish-payload-over-the-limit` a 201-byte payload, over the 200-byte
`MaxUploadBytes` `PublishPayloadLimitTests` sets. The recorder read the whole `PUBLISH`,
where Surl reads only its first bytes before closing; the pinned build had already exited 0
by then either way.

curl prints each `PUBLISH` it receives as its topic length, topic and payload, so
`stdout.bin` for `subscribe-t` is `00 01 74 68 69`.

## mqtts (BL-068)

Recorded on 2026-09-29 from the repository root, in Windows PowerShell, with the same
pinned build (SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`),
`$ca` as above, with `Record-CurlExchange.ps1 -Raw -Tls`: the recorder answered over TLS
1.2 with its throwaway self-signed certificate (so curl was given `-k`), recorded the
decrypted bytes in `request.bin` and `transcript.txt`, and sent a TLS close_notify before
closing. Each case was fed the same packets as its `mqtt` twin (`subscribe-t`,
`publish-hi`), so its `reply.bin` is a copy of that twin's. curl sent the same packets as
over `mqtt`, and each case exited 0 with an empty `stderr.txt`; `stdout.bin` for
`mqtts-subscribe-t` is `00 01 74 68 69`, as over `mqtt`. `MqttProtocolServerTests` replays
them through an `InMemoryConnection` that already holds a TLS session, as the engine hands
one over (ADR-0010), with listen URL `mqtts://127.0.0.1:18884/`.

| Folder | Retained before | Command line |
| --- | --- | --- |
| `mqtts-subscribe-t` | `t` = `hi` | `.\Record-CurlExchange.ps1 -Port 18884 -Raw -Tls -RawIdleMilliseconds 300 -RawReply $ca,'\x90\x03\x00\x01\x00\x31\x05\x00\x01thi\xE0\x00' -CurlArgs '-sS','-k','mqtts://127.0.0.1:18884/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\mqtts-subscribe-t` |
| `mqtts-publish-hi` | nothing | `.\Record-CurlExchange.ps1 -Port 18884 -Raw -Tls -RawIdleMilliseconds 300 -RawReply $ca -CurlArgs '-sS','-k','-d','hi','mqtts://127.0.0.1:18884/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\mqtts-publish-hi` |

## Logins (BL-115)

The `CONNECT` curl sends with `-u`, and what curl reports for the two `CONNACK` refusals
ADR-0032 decision 5 answers a login with. Recorded on 2026-09-29 from the repository root, in
Windows PowerShell, with the same pinned build (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) and `Record-CurlExchange.ps1 -Raw`.

| Folder | curl reported | Command line |
| --- | --- | --- |
| `connect-user-and-password` | exit 0, empty stderr | `.\Record-CurlExchange.ps1 -Port 18885 -Raw -RawReply '\x20\x02\x00\x00','\x90\x03\x00\x01\x00\x31\x05\x00\x01thi\xE0\x00' -CurlArgs '-sS','-u','tester:secret','mqtt://127.0.0.1:18885/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\connect-user-and-password` |
| `connack-bad-user-name-or-password` | **exit 8**, `curl: (8) Expected 0000 but got 0004` | `.\Record-CurlExchange.ps1 -Port 18885 -Raw -RawReply '\x20\x02\x00\x04' -CurlArgs '-sS','-u','tester:secret','mqtt://127.0.0.1:18885/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\connack-bad-user-name-or-password` |
| `connack-not-authorized` | **exit 8**, `curl: (8) Expected 0000 but got 0005` | `.\Record-CurlExchange.ps1 -Port 18885 -Raw -RawReply '\x20\x02\x00\x05' -CurlArgs '-sS','mqtt://127.0.0.1:18885/t' -OutDirectory Surl.Protocol.Mqtt.UnitTests\Fixtures\connack-not-authorized` |

With `-u tester:secret` curl sets connect flags `C2` (user name, password, CleanSession) and
sends the user name and password after its client identifier, each as a two-byte length and
its bytes: `00 06 tester 00 06 secret`. Without `-u` the flags are `02` and nothing follows the
client identifier. `CONNACK` 4 and `CONNACK` 5 both end curl with **exit 8**
(`CURLE_WEIRD_SERVER_REPLY`); only the return code in the message differs. That is what BL-118
asserts end to end. `connect-user-and-password`'s `reply.bin` is the `subscribe-t` answer, and
each refusal's `reply.bin` is its `CONNACK` alone, after which curl closed.
`MqttProtocolServerTests` replays `connect-user-and-password`'s `request.bin` against a fake
authentication policy for each verdict.
