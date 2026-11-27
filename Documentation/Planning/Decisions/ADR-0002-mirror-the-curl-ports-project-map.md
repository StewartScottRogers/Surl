# ADR-0002 — Mirror the Curl port's project map, with `Surl.Content` in place of a file protocol library

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Stewart, approving the proposed shell on 2026-09-28
- **Amended:** decision 3's table by [ADR-0048](ADR-0048-the-hand-built-ssh-primitive-libraries.md) (2026-09-29), which adds the four hand-built SSH primitive libraries: `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Curve25519.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, each referencing nothing, and `Surl.Cryptography.Ed25519.UnitLibrary`, referencing `Surl.Cryptography.Curve25519.UnitLibrary`.
- **Amended:** decision 3's table by [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md) (2026-09-29), which adds `Surl.Cryptography.Rc4.UnitLibrary` and `Surl.Cryptography.BcryptPbkdf.UnitLibrary`, each referencing nothing (their rows recorded here on 2026-09-30 by BL-252). Since then: `Surl.Cryptography.BcryptPbkdf.UnitLibrary` stopped referencing nothing on 2026-09-30, when BL-255 added its reference to `Surl.Cryptography.Blowfish.UnitLibrary` (ADR-0061, below).
- **Amended:** decision 3's table by [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) (2026-09-29), which adds the two libraries the mail servers share: `Surl.LineProtocol.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`, and `Surl.MailStore.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`.
- **Amended:** decision 3's table by [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md) (2026-09-30), which adds the hand-built Kerberos library, `Surl.Kerberos.UnitLibrary`, referencing nothing. `Surl.Authentication.UnitLibrary` will reference it (BL-240); no protocol server needs it, since every login goes through the policy contracts. Since then: BL-240 added `Surl.Authentication`'s reference on 2026-09-30. "No protocol server needs it" stopped being true on 2026-11-12, when BL-765 ([ADR-0121](ADR-0121-how-surl-serves-socks4-socks4a-socks5-and-socks5h.md) amendment 2) added `Surl.Protocol.Socks.UnitLibrary`'s reference, for SOCKS5 GSS-API authentication.
- **Amended:** decision 3's table by [ADR-0061](ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md) (2026-09-30), which adds `Surl.Cryptography.Blowfish.UnitLibrary`, `Surl.Cryptography.Cast128.UnitLibrary` and `Surl.Cryptography.Ripemd160.UnitLibrary`, each referencing nothing, and lets `Surl.Cryptography.BcryptPbkdf.UnitLibrary` reference `Surl.Cryptography.Blowfish.UnitLibrary`. BL-254 created the projects.
- **Amended:** decision 3's table by [ADR-0070](ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md) (2026-09-30), which adds the library the HTTP, WebSocket and RTSP servers share, `Surl.HttpMessage.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`. BL-292 creates the projects and BL-293 moves the HTTP/1.x message machinery into it from `Surl.Protocol.Http.UnitLibrary`.
- **Amended:** decision 3's table by [ADR-0081](ADR-0081-how-surl-serves-http-2.md) (2026-11-04), which adds the hand-built HPACK library, `Surl.Hpack.UnitLibrary`, referencing nothing, in `Protocols/Http/`. BL-365 creates the projects; `Surl.Protocol.Http.UnitLibrary` references it for HTTP/2.
- **Amended:** decision 3's table by [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) (2026-11-04), which adds the four libraries HTTP/3 over QUIC needs: in `Cryptography/`, `Surl.Cryptography.MlKem.UnitLibrary`, referencing nothing, and `Surl.Tls13.UnitLibrary`, referencing `Surl.Cryptography.Curve25519.UnitLibrary` and `Surl.Cryptography.MlKem.UnitLibrary`; in `Protocols/Http/`, `Surl.Quic.UnitLibrary`, referencing `Surl.Tls13.UnitLibrary`, `Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, and `Surl.Qpack.UnitLibrary`, referencing `Surl.Hpack.UnitLibrary`. BL-373, BL-375 and BL-382 create three of the projects and BL-544 creates `Surl.Cryptography.MlKem`; `Surl.Protocol.Http.UnitLibrary` references `Surl.Quic` and `Surl.Qpack` for HTTP/3. Since then: `Surl.Quic` also references `Surl.Protocol.Abstractions.UnitLibrary`, added on 2026-11-05 by BL-572 (ADR-0086, below). `Surl.Cryptography.Curve25519` and `Surl.Cryptography.MlKem` stopped being all that `Surl.Tls13` references on 2026-11-11, when BL-1010 added `Surl.Cryptography.Curve448`; BL-1030 added `Surl.Cryptography.Sm2` on 2026-11-12 (both ADR-0147, below), and BL-1145 added `Surl.Cryptography.ChaCha20` and `Surl.Cryptography.Poly1305` on 2026-11-14 (ADR-0157, below).
- **Amended:** decision 3's table by [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md) (2026-11-04), which adds the three libraries the extended servers share with the original ones: in `Protocols/Http/`, `Surl.HttpServing.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary` and `Surl.Content.UnitLibrary`, and `Surl.WebSocket.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.HttpMessage.UnitLibrary`; in `Cryptography/`, `Surl.Dtls.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`. BL-407, BL-408 and BL-412 create the projects. Since then: `Surl.Dtls` also references `Surl.Cryptography.Curve25519.UnitLibrary`, added on 2026-11-05 by BL-414 (ADR-0087, below). No original server references `Surl.Dtls`: among the production projects, only `Surl.Extended.Protocol.Coap.UnitLibrary` and `Surl.Composition.UnitLibrary` do.
- **Amended:** decision 3's table by [ADR-0087](ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md) (2026-11-04): `Surl.Dtls.UnitLibrary` also references `Surl.Cryptography.Curve25519.UnitLibrary`, for the x25519 group.
- **Amended:** decision 3's table by [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md) (2026-11-04), which moves the HTTP/2 and HTTP/3 framing out of `Surl.Protocol.Http.UnitLibrary`, so it no longer lives only there, into two libraries in `Protocols/Http/` that the HTTP server and the extended servers share: `Surl.Http2.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Hpack.UnitLibrary`, and `Surl.Http3.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Quic.UnitLibrary` and `Surl.Qpack.UnitLibrary`; and lets `Surl.Quic.UnitLibrary` reference `Surl.Protocol.Abstractions.UnitLibrary`, for the loop that drives a QUIC connection from an `IDatagramFlow`. BL-409 and BL-410 create the projects, and BL-572 adds `Surl.Quic`'s reference.
- **Amended:** decision 3's table by [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md) (2026-11-04), which adds the HTTP and HTTPS proxy server, `Surl.Protocol.HttpProxy.UnitLibrary` in `Protocols/Http/`, referencing `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary` and `Surl.Authentication.UnitLibrary`. BL-552 creates the projects. Correction (2026-11-27, BL-1836): the table lists horizontal libraries only, so it has no row for the proxy, a protocol server that no other protocol server may reference. The proxy's project file does not reference `Surl.Authentication.UnitLibrary`, which is not in the table: BL-552 created it on 2026-11-05 referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.HttpMessage.UnitLibrary`, and ADR-0088 amendment 3 (BL-553, 2026-11-05) settled that it takes an `IAuthenticationPolicy` and needs no further reference for logins. It also references `Surl.Hpack` and `Surl.Http2` since BL-940 (2026-11-06) and `Surl.LineProtocol` since BL-1734 (2026-11-25, [ADR-0182](ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md) amendment 16).
- **Amended:** decision 3's table by [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md) (2026-11-04), which adds the message bus the MQTT and extended messaging servers share, `Surl.MessageBus.UnitLibrary` in `Protocols/Messaging/`, referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary` (for `IContentFileSystem`). BL-629 creates the projects. Since then: not every extended messaging server references it. On 2026-11-05 [ADR-0114](ADR-0114-how-the-xmpp-server-answers-xmpp-and-xmpps.md) rejected routing XMPP over the bus, and `Surl.Extended.Protocol.Xmpp.UnitLibrary` does not reference it; the Redis server, in `Protocols/DataStore/`, does.
- **Amended:** decision 3's table by [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md) (2026-11-05), which adds the MCP core the `Surl.Mcp` host and the extended MCP server share, `Surl.McpServing.UnitLibrary` in `Protocols/Http/`, referencing nothing. BL-619 creates the projects.
- **Amended:** decision 3's table by [ADR-0105](ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md) (2026-11-05), which adds the hand-built codecs the Kafka broker needs and any server may use, `Surl.Compression.UnitLibrary` in `Foundation/`, referencing nothing. BL-702 creates the projects.
- **Amended:** decision 3's table by [ADR-0122](ADR-0122-how-surl-serves-https-over-tls-srp.md) (2026-11-05), which adds the two hand-built libraries TLS-SRP needs, in `Cryptography/`: `Surl.Cryptography.Srp.UnitLibrary`, referencing nothing, and `Surl.TlsSrp.UnitLibrary`, referencing `Surl.Cryptography.Srp.UnitLibrary`. BL-564 and BL-565 create the projects; `Surl.Networking.UnitLibrary` references `Surl.TlsSrp` (BL-567).
- **Amended:** decision 3's table by [ADR-0147](ADR-0147-the-remaining-tls-1-3-groups-curl-offers-over-quic.md) (2026-11-06), which adds two hand-built libraries in `Cryptography/`, each referencing nothing: `Surl.Cryptography.Curve448.UnitLibrary` (X448) and `Surl.Cryptography.Sm2.UnitLibrary` (ECDH on curveSM2); and lets `Surl.Tls13.UnitLibrary` reference both. BL-1007 and BL-1029 create the projects; BL-1010 and BL-1030 add the references.
- **Amended:** decision 3's table by [ADR-0157](ADR-0157-how-surl-serves-tls-1-3-where-sslstream-cannot-so-macos-passes-upstream-test-4001.md) (2026-11-14): its decision 2 has `Surl.Tls13.UnitLibrary`'s record layer use `Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, and BL-1145 added both references the same day. ADR-0157's header does not name this table; the change is recorded here on 2026-11-27 by BL-1836.
- **Amended:** decision 3's table by [ADR-0189](ADR-0189-the-http-server-sets-cookies-from-set-cookie-options-and-publishes-the-cookies-curl-sends-back.md) (2026-11-24), whose decision 7 has `Surl.Cookies.UnitLibrary`, in `Foundation/`, reference `Surl.Protocol.Abstractions.UnitLibrary` only and the HTTP server use it. The project has existed since the Phase 0 shell; on 2026-11-24 BL-1660 added `Surl.Protocol.Http.UnitLibrary`'s reference to it and let `ProtocolIsolationTests` allow it. ADR-0189's header says it amends nothing; the row is recorded here on 2026-11-27 by BL-1836.

## Context

Stewart asked for a shell for Surl that matches the Curl port "using the same mindset as
a sibling", to be completed later. The Curl port organises curl into 26 production
projects, each followed by its `.UnitTests` twin, created together on its first day: one
library per protocol family, horizontal libraries for authentication, cookies,
networking, output and cryptography, a command-line library, a serving core, the
contracts, a library for upstream curl's own test cases, and the executable.

Three questions followed:

1. **Which projects?** Mirroring the map makes the pairing legible - each Surl server
   answers what the matching client library asks - and lets the port's conventions,
   agents and task board carry over unchanged.
2. **What of `file://`?** curl reads `file://` URLs from its own disk. There is no wire,
   so there is no server to write, and a library named `Surl.Protocol.File` would claim
   a protocol server that cannot exist. Yet serving files is the heart of half of Surl's
   protocols - HTTP, FTP, SFTP, SCP, TFTP, Gopher and SMB all publish a directory.
3. **All at once, or phase by phase?** Empty projects before anything works are a cost,
   but the dark factory plans and schedules by project name (a task's `touches`), and
   the reference-graph test can only guard projects that exist.

## Decision

1. Every Curl port project has a Surl twin of the same shape: `Surl.<Area>.UnitLibrary`,
   `Surl.Protocol.<Name>.UnitLibrary` for the fifteen protocol servers, and `Surl.Console`
   (assembly `surl`), each followed by its `.UnitTests` twin - 26 production projects
   and 26 test projects, all created in the Phase 0 shell.
2. `Surl.Content.UnitLibrary` takes the place of a file protocol library. It is the
   content store: it maps a request path onto the served directory without escaping it,
   lists directories, names media types, reads byte ranges, reports sizes and
   modification times, and decides where uploads land - through an injected file-system
   seam, so its tests need no disk.
3. A protocol server may reference `Surl.Protocol.Abstractions.UnitLibrary` and the
   horizontal libraries in this table, and nothing else:

   | Horizontal library | May itself reference |
   | --- | --- |
   | `Surl.Compression.UnitLibrary` (ADR-0105) | nothing |
   | `Surl.Content.UnitLibrary` | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.Cookies.UnitLibrary` (ADR-0189) | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.Cryptography.BcryptPbkdf.UnitLibrary` (ADR-0051, ADR-0061) | `Surl.Cryptography.Blowfish.UnitLibrary` |
   | `Surl.Cryptography.Blowfish.UnitLibrary` (ADR-0061) | nothing |
   | `Surl.Cryptography.Cast128.UnitLibrary` (ADR-0061) | nothing |
   | `Surl.Cryptography.ChaCha20.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.Curve25519.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.Curve448.UnitLibrary` (ADR-0147) | nothing |
   | `Surl.Cryptography.Ed25519.UnitLibrary` (ADR-0048) | `Surl.Cryptography.Curve25519.UnitLibrary` |
   | `Surl.Cryptography.MlKem.UnitLibrary` (ADR-0082) | nothing |
   | `Surl.Cryptography.Poly1305.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.Rc4.UnitLibrary` (ADR-0051) | nothing |
   | `Surl.Cryptography.Ripemd160.UnitLibrary` (ADR-0061) | nothing |
   | `Surl.Cryptography.Sm2.UnitLibrary` (ADR-0147) | nothing |
   | `Surl.Cryptography.Srp.UnitLibrary` (ADR-0122) | nothing |
   | `Surl.Cryptography.UnitLibrary` | nothing |
   | `Surl.Dtls.UnitLibrary` (ADR-0084, ADR-0087) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Cryptography.Curve25519.UnitLibrary` |
   | `Surl.Hpack.UnitLibrary` (ADR-0081) | nothing |
   | `Surl.Http2.UnitLibrary` (ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Hpack.UnitLibrary` |
   | `Surl.Http3.UnitLibrary` (ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Quic.UnitLibrary`, `Surl.Qpack.UnitLibrary` |
   | `Surl.HttpMessage.UnitLibrary` (ADR-0070) | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.HttpServing.UnitLibrary` (ADR-0084) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary`, `Surl.Content.UnitLibrary` |
   | `Surl.Kerberos.UnitLibrary` (ADR-0057) | nothing |
   | `Surl.LineProtocol.UnitLibrary` (ADR-0050) | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.MailStore.UnitLibrary` (ADR-0050) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary` |
   | `Surl.McpServing.UnitLibrary` (ADR-0103) | nothing |
   | `Surl.MessageBus.UnitLibrary` (ADR-0100) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary` |
   | `Surl.Qpack.UnitLibrary` (ADR-0082) | `Surl.Hpack.UnitLibrary` |
   | `Surl.Quic.UnitLibrary` (ADR-0082, ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Tls13.UnitLibrary`, `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary` |
   | `Surl.Tls13.UnitLibrary` (ADR-0082, ADR-0147, ADR-0157) | `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Curve25519.UnitLibrary`, `Surl.Cryptography.Curve448.UnitLibrary`, `Surl.Cryptography.MlKem.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary`, `Surl.Cryptography.Sm2.UnitLibrary` |
   | `Surl.TlsSrp.UnitLibrary` (ADR-0122) | `Surl.Cryptography.Srp.UnitLibrary` |
   | `Surl.WebSocket.UnitLibrary` (ADR-0084) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary` |

   Every project the table names exists. `ProtocolIsolationTests`, in
   `Surl.Protocol.Abstractions.UnitTests`, holds the table as its `HorizontalLibraries`
   allow-list and fails when a protocol server references a project that is neither
   Abstractions nor in the table, when a library in the table references a project outside
   its row, or when Abstractions references anything. A further horizontal library joins
   the table through a new ADR when Surl needs one.

## Consequences

Good:

- The pairing reads at a glance: `Surl.Protocol.Ftp` answers what curl's FTP support
  asks.
- The Curl port's agents, commands, rules and task board carry over with only the names
  changed, and a task can name any project in `touches` from day one.
- The architecture test is live from the first commit.

Costs and caveats:

- Twenty-four of the twenty-six production projects hold no code yet, and
  `Measure-CodeQuality.ps1` reports only the assemblies that have members. Accepted:
  that is the Phase 0 shell by design. (This describes the shell as decided on
  2026-09-28, not the solution since.)
- One protocol server per family means the secure variants (`https`, `ftps`, `imaps` and
  the rest) are the same server over a secured connection, never a second library.

## Alternatives considered

- **Create projects phase by phase.** Rejected: it breaks the match Stewart asked for,
  and the factory's lanes plan by project name.
- **Keep `Surl.Protocol.File`.** Rejected: it would name a protocol server that cannot
  exist - the opposite of "say what it does".
- **Put the content store in `Surl.Core`.** Rejected: protocol servers may not reference
  `Surl.Core`, and every file-serving server needs the store.
