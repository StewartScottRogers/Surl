# ADR-0082 — How Surl serves HTTP/3 over QUIC, and what is hand-built

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-372. BL-373 to BL-385, BL-544 and BL-545 build what it decides.
- **Amends:** [ADR-0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md)
  decision 3 (HTTP/3 on Windows is measured with the static 8.21.0 build; decision 1 below),
  [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3's table (four rows;
  decision 2), [ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 4 (`h3` is served,
  over QUIC, by the hand-built TLS 1.3 of decision 3) and
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5 for HTTP/3 connections
  (decision 7). Every other section of those ADRs applies to HTTP/3 as written.

## Context

The product overview puts HTTP/3 in scope ("HTTP versions: 1.0, 1.1, 2 and 3 over QUIC, on the
server side", Phasing row 6), and root `CLAUDE.md` names QUIC and HTTP/3 among the pieces built by
hand, each in its own `Surl.<Area>.UnitLibrary`. ADR-0010 section 4 says `h3` is QUIC's and never
travels over `SslStream`. HTTP/3 is RFC 9114; QUIC is RFC 9000 (transport), RFC 9001 (TLS for
QUIC), RFC 9002 (loss detection and congestion control) and RFC 9368 (version negotiation);
QPACK is RFC 9204.

Upstream curl 8.21.0 (tag `curl-8_21_0`, `docs/cmdline-opts/http3.md` and `http3-only.md`):
`--http3` tries HTTP/3 and falls back to HTTP/1.1 or HTTP/2 over TCP after a short delay;
`--http3-only` has no fallback; HTTP/3 is for HTTPS only. Without either option curl reaches
HTTP/3 only through an `Alt-Svc` cache it was given with `--alt-svc <file>`. Builds with the
`HTTP3` feature: the `linux-x64` and `osx-arm64` reference builds and, on Windows, the two
supplementary builds (static 8.21.0 and curl.se's 8.22.0_2); the Git for Windows reference build
(Schannel) has none.

### What the base class library offers

From Microsoft's ".NET QUIC support" page,
<https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview> (checked
2026-11-04; page updated 2024-11-18), and "Cross-platform cryptography",
<https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography>
(checked 2026-11-04; page dated 2026-08-03):

- `System.Net.Quic` is a wrapper over the native MsQuic library. `QuicListener.IsSupported` is
  true only where MsQuic loads and TLS 1.3 is available:
  - **Windows:** Windows 11 or Windows Server 2022 and later; `msquic.dll` ships with the .NET
    runtime as its own native file.
  - **Linux:** the `libmsquic` package (2.2 or later) must be installed from
    packages.microsoft.com (or Alpine's repository), with OpenSSL and `libnuma1`.
  - **macOS:** "partially supported through a non-standard Homebrew package manager with some
    limitations": `brew install libmsquic` and `DYLD_FALLBACK_LIBRARY_PATH` set before running.
- `AesGcm` is supported on Windows, Linux and macOS. AES-ECB (`Aes.EncryptEcb`) is supported on
  all three. `ChaCha20Poly1305` needs Windows 10 build 20142 or later and OpenSSL 1.1.0 or later.
  ECDH and ECDSA on P-256, P-384 and P-521 and RSA-PSS are supported on all three. ML-KEM needs
  OpenSSL 3.5 on Linux or a Windows Insider build, and is not supported on Apple platforms. No
  X25519 in the BCL at all. `HKDF`, `HMAC`, `SHA256` and `SHA384` are everywhere.

NFR-006 asks for one native ahead-of-time file that needs nothing else installed. Rule 2 of the
overview's architecture says a protocol server receives its transport from the listener seam and
never constructs a socket; `QuicListener` binds its own UDP socket. And QUIC carries TLS 1.3
handshake messages in `CRYPTO` frames, with no TLS records (RFC 9001 section 4), which `SslStream`
cannot produce, so a hand-built QUIC also needs a hand-built TLS 1.3 handshake.

### Measured

Each pinned `HTTP3` build was run on 2026-11-04 with `--http3-only` against a UDP port that
recorded and answered nothing, and its Initial packets were decrypted to the ClientHello by
BL-363's `-QuicInitial` (`Run-QuicInitialRecorder.cs`):

- **S** - stunnel/static-curl 8.21.0 for Windows, SHA-256
  `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (OpenSSL 4.0.1, ngtcp2
  1.23.0, nghttp3 1.16.0): `Record-CurlExchange.ps1 -Port 18443 -QuicInitial -CurlArgs
  '-sS','-k','--http3-only','--connect-timeout','3','https://localhost:18443/'`.
- **W** - curl.se 8.22.0_2 for Windows, SHA-256
  `B028548A8C0D3DC2C899FE415DCE1EF1D2406801B3ADC43DF9921683026E86CC` (LibreSSL 4.3.2, ngtcp2
  1.25.0, nghttp3 1.18.0): the same command.
- **L** - stunnel/static-curl 8.21.0 for Linux, SHA-256
  `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` (OpenSSL 4.0.1, ngtcp2
  1.23.0, nghttp3 1.16.0), run in WSL Ubuntu at its `defaultPath` with
  `--http3-only --connect-timeout 3 https://127.0.0.1:18443/`; its datagrams were captured with
  `socat -u UDP4-RECV:18443` (four 1200-byte datagrams, framed into `datagrams.bin` with their
  two-byte lengths) and decrypted on Windows by `Run-QuicInitialRecorder.cs --datagrams`. WSL has
  neither PowerShell nor the .NET 10 SDK, so the recorder itself could not run there.
- **M** - the `osx-arm64` reference build is not on this machine. It is the same source,
  OpenSSL, ngtcp2 and nghttp3 as S and L; BL-385 records it in CI's macOS leg.

| Field | S | L | W |
| --- | --- | --- | --- |
| Exit, stderr | 28, `Connection timed out after 3007 milliseconds` | 28, `Connection timed out after 3002 milliseconds` | 28, `Connection timed out after 3013 milliseconds` |
| Datagrams | 4 x 1200 bytes, Initial packet numbers 0 to 3, payload 1131 bytes each, token empty, `PING` frames | same | same |
| QUIC version, DCID, SCID | 1, 20 bytes, 20 bytes | same | same |
| ClientHello length | 1519 bytes (1501 for `127.0.0.1`: no `server_name`) | 1501 bytes | 1479 bytes |
| `legacy_version`, `supported_versions` | `0303`; `0304` (TLS 1.3 only) | same | same |
| Cipher suites, in order | `1302` `TLS_AES_256_GCM_SHA384`, `1303` `TLS_CHACHA20_POLY1305_SHA256`, `1301` `TLS_AES_128_GCM_SHA256` | same | the same three, then `00FF` (renegotiation SCSV) |
| `supported_groups`, in order | `11EC` X25519MLKEM768, `11EB` SecP256r1MLKEM768, `11EE` SecP384r1MLKEM1024, `001D` x25519, `0017` secp256r1, `001E` x448, `0018` secp384r1, `0019` secp521r1, `0029` curveSM2, `0100` ffdhe2048, `0101` ffdhe3072 | same | `11EC` X25519MLKEM768, `001D` x25519, `0017` secp256r1, `0018` secp384r1, `0019` secp521r1 |
| `key_share` | X25519MLKEM768 (1216 bytes), x25519 (32 bytes) | same | same |
| `signature_algorithms`, in order | `0905` `0906` `0904` (ML-DSA-65, -87, -44), `0403` `0503` `0603` (ECDSA P-256, P-384, P-521), `0807` Ed25519, `0808` Ed448, `081A` `081B` `081C` (ECDSA brainpool, TLS 1.3), `0809` `080A` `080B` (RSA-PSS PSS), `0804` `0805` `0806` (RSA-PSS RSAE), `0401` `0501` `0601` (RSA PKCS#1), `0708` (SM2) | same | `0806`, `080B`, `0601`, `0603`, `0805`, `080A`, `0501`, `0503`, `0804`, `0809`, `0401`, `0403` |
| `psk_key_exchange_modes` | `psk_dhe_ke` | same | not sent |
| ALPN | `h3`, `h3-29` | same | same |
| Other extensions | `encrypt_then_mac`, `extended_master_secret`, `post_handshake_auth` (all empty) | same | `ec_point_formats` (`00`) |
| Extension order | `0039` `000A` `0016` `0017` `0031` `000D` `002B` `002D` `0033` (`0000`) `0010` | same, without `0000` | `0000` `002B` `0033` `000A` `0010` `0039` `000D` `000B` |
| `quic_transport_parameters`, in order | `initial_source_connection_id` (0x0f) = the SCID; `initial_max_stream_data_bidi_local` (0x05) = 32768; `initial_max_stream_data_bidi_remote` (0x06) = 32768; `initial_max_stream_data_uni` (0x07) = 1048576000; `initial_max_data` (0x04) = 1048576000; `initial_max_streams_bidi` (0x08) = 262144; `initial_max_streams_uni` (0x09) = 262144; `version_information` (0x11) = chosen 1, available [1] | same | same |

Absent from every build: `max_idle_timeout`, `max_udp_payload_size`, `active_connection_id_limit`,
`max_datagram_frame_size`, `disable_active_migration`, `early_data`, `pre_shared_key`,
`session_ticket`. Each build's ngtcp2 splits its ClientHello into many `CRYPTO` frames out of
order and sends the whole flight twice (packets 2 and 3 repeat 0 and 1), so the server must
reassemble `CRYPTO` data by offset and ignore what it already has.

Two more runs, on 2026-11-04:

| Command | Build | Listening | Exit | Result |
| --- | --- | --- | --- | --- |
| `curl -sS -k --http3-only https://127.0.0.1:18459/` | S, W | nothing on UDP 18459 | 56 | `curl: (56) Failed to connect to 127.0.0.1:18459 after 2 ms: Failure when receiving data from the peer` (Windows reports the ICMP port-unreachable as a receive failure) |
| the same | L | nothing on UDP 18459 | 7 | `curl: (7) Failed to connect to 127.0.0.1:18459 after 1 ms: Could not connect to server` |
| `curl -sS -k -i --http3 https://localhost:18444/` (`Record-CurlExchange.ps1 -Tls`) | S | TCP only | 0 | after 287 ms, `GET / HTTP/1.1` over TLS on TCP; stdout `HTTP/1.1 200 OK` |

## Decision

1. **QUIC is hand-built, on every platform; `System.Net.Quic` is not used.** It needs a native
   MsQuic file beside the executable on Windows, an installed package on Linux and Homebrew on
   macOS, so it breaks NFR-006 everywhere, and its listener binds its own socket, which breaks
   Rule 2. It also hides the wire: the transport parameters, the limits of decision 7 and the
   bytes curl is measured against would be MsQuic's, not Surl's. One hand-built QUIC, one TLS 1.3
   handshake and one QPACK run the same code on Windows, Linux and macOS. **Windows measures
   HTTP/3 with the static 8.21.0 build (S)**, as ADR-0081 decision 1 does for HTTP/2: it is the
   reference release and sent a ClientHello identical, field for field, to the Linux reference
   build's (L). ADR-0017 decision 3 is amended: 8.22.0_2 (W) stays pinned as a comparison build
   that no test needs; the Git for Windows reference build has no HTTP/3.
2. **The libraries.** Four new horizontal libraries, each with its `.UnitTests` twin beside it
   and held to the root `CLAUDE.md` quality gates, and four new rows in ADR-0002 decision 3's
   table:

   | Library | Folder | Holds | May itself reference |
   | --- | --- | --- | --- |
   | `Surl.Cryptography.MlKem.UnitLibrary` | `Cryptography/` | ML-KEM-768 (FIPS 203): key generation from a seed, encapsulation, decapsulation. Hand-built: the BCL's `MLKem` is missing on macOS and on most Windows and Linux machines. | nothing |
   | `Surl.Tls13.UnitLibrary` | `Cryptography/` | The TLS 1.3 server handshake for QUIC (RFC 8446 sections 4 and 7, RFC 9001 section 4): the handshake messages and extensions, the key exchange of decision 3, `CertificateVerify` signing, `Finished` and the key schedule, handing each encryption level's secrets to its caller. No record layer and no alerts on the wire: an alert is reported to the caller as a code. | `Surl.Cryptography.Curve25519.UnitLibrary`, `Surl.Cryptography.MlKem.UnitLibrary` |
   | `Surl.Quic.UnitLibrary` | `Protocols/Http/` | QUIC version 1 for a server: variable-length integers, packets and frames, packet protection and header protection, the connection with its packet number spaces, `CRYPTO` reassembly, streams and flow control, loss detection and congestion control. It does no I/O: a connection takes received datagrams and the time from an injected `TimeProvider`, and returns datagrams to send and the next deadline, so its tests need no socket. | `Surl.Tls13.UnitLibrary`, `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary` |
   | `Surl.Qpack.UnitLibrary` | `Protocols/Http/` | QPACK (RFC 9204): the static table, field line representations and the encoder and decoder stream instructions, with HPACK's Huffman code. | `Surl.Hpack.UnitLibrary` |

   The HTTP/3 frames, the control and QPACK streams, and the HTTP/3 server live in
   `Surl.Protocol.Http.UnitLibrary`, which references `Surl.Quic` and `Surl.Qpack`. `Surl.Quic`
   and `Surl.Qpack` sit in `Protocols/Http/` beside `Surl.Hpack` because only the HTTP servers use
   them; `Surl.Tls13` and `Surl.Cryptography.MlKem` are hand-built cryptography and sit in
   `Cryptography/` (ADR-0079). The planning names in BL-373 to BL-385 are kept.
3. **TLS 1.3 for QUIC.**
   - *Version and ALPN.* TLS 1.3 only (`supported_versions` must list `0304`); RFC 9001 forbids
     anything older. The server agrees ALPN `h3` and nothing else; a ClientHello without `h3`
     (`h3-29` alone, say) ends with alert `no_application_protocol`. SNI is read and ignored: one
     certificate serves every name (ADR-0010 section 3).
   - *Cipher suites.* All three TLS 1.3 suites curl offers: `TLS_AES_128_GCM_SHA256`,
     `TLS_AES_256_GCM_SHA384` and `TLS_CHACHA20_POLY1305_SHA256`. The server takes the first
     suite in the client's order that it supports, so curl gets `TLS_AES_256_GCM_SHA384`, and
     curl's `--tls13-ciphers` picks any of the three. AES-GCM is the BCL's `AesGcm` and header
     protection the BCL's AES-ECB, supported on all three platforms; Initial packets always use
     AES-128-GCM (RFC 9001 section 5.2). ChaCha20-Poly1305 is the hand-built
     `Surl.Cryptography.ChaCha20` and `Surl.Cryptography.Poly1305` (ADR-0048) on every platform:
     the BCL's `ChaCha20Poly1305` is missing on older Windows builds, and QUIC's header protection
     needs the raw ChaCha20 block function with a chosen counter, which the BCL does not expose.
     `HKDF`, `HMAC` and the SHA-2 hashes are the BCL's.
   - *Key exchange groups*, in the server's order of preference: X25519MLKEM768, x25519,
     secp256r1, secp384r1, secp521r1. The server picks the first group in that order for which
     the client sent a key share; with none, it sends a HelloRetryRequest naming the first group in
     that order that the client's `supported_groups` lists; with none of those either, alert
     `handshake_failure`. x25519 is the hand-built `Surl.Cryptography.Curve25519`; the NIST curves
     are the BCL's `ECDiffieHellman`; X25519MLKEM768 (draft-ietf-tls-ecdhe-mlkem: ML-KEM-768
     ciphertext then x25519 share, shared secret ML-KEM's then x25519's) uses
     `Surl.Cryptography.MlKem`. Until that library lands, X25519MLKEM768 is skipped and every
     measured build gets x25519 with no HelloRetryRequest, since each sends an x25519 share. The
     groups curl lists only in `supported_groups` - SecP256r1MLKEM768, SecP384r1MLKEM1024, x448,
     curveSM2, ffdhe2048 and ffdhe3072 - are reached only when a curl is told `--curves` naming
     nothing else; they are served too, by BL-545. BL-544 builds `Surl.Cryptography.MlKem` and
     X25519MLKEM768.
   - *Signatures.* `CertificateVerify` is signed with the certificate's key, served as ADR-0010
     section 3 serves it: an RSA key with `rsa_pss_rsae_sha256` (`0804`), an ECDSA key with the
     scheme of its curve (`0403`, `0503`, `0603`). Every measured build offers all four. A
     client that offers none of them for the key gets alert `handshake_failure`. The throwaway
     certificate (RSA 2048) therefore signs with `0804`.
   - *What is not built.* No session tickets are sent (`NewSessionTicket`), so there is no
     resumption and no 0-RTT; `psk_key_exchange_modes` is read and ignored; a `pre_shared_key` or
     `early_data` extension is ignored as if absent (RFC 8446 section 4.2.10 lets a server
     decline). No post-handshake authentication. Client certificates follow ADR-0010 section 5:
     the server sends `CertificateRequest` exactly when it would over TCP.
   - *Alerts.* A TLS alert ends the connection with `CONNECTION_CLOSE` carrying the QUIC error
     `0x0100` plus the alert number (RFC 9001 section 4.8): `no_application_protocol` is `0x0178`,
     `handshake_failure` `0x0128`, `decrypt_error` `0x0133`.
4. **QUIC features.**
   - *Version.* QUIC version 1 only, the only version curl offers (`version_information`
     chosen 1, available [1]). A long-header datagram of at least 1200 bytes with any other
     version is answered with a Version Negotiation packet listing `0x00000001` (RFC 9000
     section 6); a shorter one is dropped. The server's transport parameters carry
     `version_information` chosen 1, available [1] (RFC 9368).
   - *Retry.* Not sent. Address validation relies on RFC 9000 section 8.1's limit: before the
     client's address is validated (a Handshake packet from it decrypts), the server sends at most
     three times the bytes it has received. ADR-0006's connection limits bound the state.
   - *0-RTT.* Not accepted: without session tickets curl has nothing to send it with; a 0-RTT
     packet is dropped.
   - *Connection IDs.* The server's connection ID is 8 random bytes, from the injected random
     source. It issues no `NEW_CONNECTION_ID`, keeps the client's `NEW_CONNECTION_ID`s up to the
     default `active_connection_id_limit` of 2, and answers `RETIRE_CONNECTION_ID` as RFC 9000
     section 19.16 requires. No stateless reset token is sent and no stateless reset is used.
   - *Migration.* `disable_active_migration` is sent. A datagram from a new remote address is a
     new flow (decision 5): its Initial starts a new connection, anything else is dropped.
   - *Key update.* Received key updates are followed (RFC 9001 section 6 requires it); the
     server never starts one.
   - *Transport parameters sent:* `original_destination_connection_id`,
     `initial_source_connection_id`, `max_idle_timeout` = `--idle-timeout` in milliseconds,
     `initial_max_data` = 1048576, `initial_max_stream_data_bidi_remote` = 262144,
     `initial_max_stream_data_uni` = 65536, `initial_max_streams_bidi` = 100,
     `initial_max_streams_uni` = 3 (the client's control stream and its QPACK encoder and
     decoder streams, RFC 9114 section 6.2), `disable_active_migration` and
     `version_information`. The rest keep RFC 9000's defaults and are not sent. The server widens
     the windows with `MAX_DATA`, `MAX_STREAM_DATA` and `MAX_STREAMS` as it consumes data and
     streams close, so an upload never stalls on the server's credit.
   - *Packets.* Every datagram the server sends is at most 1200 bytes, the size RFC 9000
     guarantees; there is no path MTU discovery. The server coalesces its Initial and Handshake
     packets, sends `HANDSHAKE_DONE` once the handshake completes, and discards Initial and then
     Handshake keys at the points RFC 9001 section 4.9 names. The spin bit is 0; ECN is not used;
     the datagram extension (RFC 9221) is not offered.
   - *Loss detection and congestion control.* RFC 9002 in full: packet and time thresholds, the
     probe timeout, and NewReno with RFC 9002 section 7's initial window (10 x 1200 bytes),
     minimum window, recovery and persistent congestion. Sending is limited by the congestion
     window; there is no pacing beyond it.
5. **The seam: the existing datagram contract, one flow per remote endpoint.** The HTTP/3 server
   is an `IDatagramProtocolServer` and each QUIC connection rides one `IDatagramFlow`
   (ADR-0004): curl never migrates, and the server disables migration, so a remote endpoint and
   a connection are one and the same; the flow's datagrams feed `Surl.Quic`'s connection and its
   output goes back through `SendAsync`. `MoveToNewLocalPortAsync` is TFTP's and is not used. No
   contract in `Surl.Protocol.Abstractions` changes; BL-380 adds only `InMemoryDatagramFlow`,
   the in-memory double tests drive (there is none today), beside `InMemoryConnection`. A refused
   flow (decision 7) is answered through the HTTP/3 server's `IDatagramRefusalWriter`.
   `Surl.Networking` (BL-381) binds a `UdpDatagramListener` for an `https` listen URL when
   HTTP/3 is on (decision 6), at the same address and port as its TCP listener, and demultiplexes
   it with `DatagramDemultiplexer` as TFTP's is, within ADR-0006's connection limits (a flow
   counts as one connection).
6. **The command line: off by default, `--http3` turns it on.**
   - `surl --http3 https://host:port/` serves HTTP/3 over QUIC on UDP at the same address and
     port as the HTTPS listener, as well as HTTP/1.x and HTTP/2 on TCP. The option keeps curl's
     name and turns it around, as `--max-filesize` does: curl's `--http3` asks for HTTP/3, surl's
     offers it. It takes no argument. It applies to every `https` listen URL and to nothing else
     (`wss` stays TCP: curl's WebSocket upgrade is HTTP/1.1's). Given with no `https` listen URL
     it is `FailedInit` (2), as other options that need a scheme are.
   - Off by default because it opens a second socket and a hand-built TLS stack on every HTTPS
     listener, which a deployment should choose (secure by default). Without it, a curl that asks
     only for HTTP/3 fails as it does against any server with no UDP listener - measured, exit 56
     on Windows and 7 on Linux - and `--http3` falls back to TCP (measured, exit 0).
   - *TLS versions.* QUIC is TLS 1.3 only, so `--http3` with `--tls-max` below `1.3` is
     `FailedInit` (2); `--tlsv1.2` and lower minimums apply to the TCP listener alone.
   - *Port.* With port 0 the TCP listener binds first and the UDP listener then binds the same
     port number; if that UDP port is taken both are released and the pair is tried again, up to
     10 times, then the bind failure ends startup as a TCP bind failure does. With a fixed port a
     UDP port in use ends startup the same way.
   - *Status line.* Unchanged: `Listening on https://<host>:<port>/` names the one port both
     transports share, so `SurlOnLoopback` and every reader of the line still find one URL per
     listen URL. With `-v` the verbose log notes, once per listener, `Serving HTTP/3 over QUIC
     on UDP port <port>`.
   - *`Alt-Svc`.* With `--http3`, every response on an `https` listener over TCP (HTTP/1.x and
     HTTP/2) carries `Alt-Svc: h3=":<port>"; ma=86400` (RFC 7838), how a server tells a client
     it can switch; curl uses it when run with `--alt-svc <file>`. HTTP/3 responses do not carry
     it. Without `--http3` no response carries it, so every HTTP/1.x and HTTP/2 byte already
     pinned stays as it is.
7. **The ADR-0006 limits on QUIC and HTTP/3.**

   | Limit (ADR-0006 section 1) | QUIC and HTTP/3 | Ends with |
   | --- | --- | --- |
   | Too many connections | A flow past `--max-connections` or `--max-connections-per-address` whose first datagram is a version 1 Initial | The refusal writer sends one Initial packet, protected with the Initial keys of the client's DCID, holding `CONNECTION_CLOSE` (0x1c) `CONNECTION_REFUSED` (0x02); any other first datagram gets no bytes |
   | Handshake time | The TLS handshake not complete within `--head-timeout` from the first datagram | `CONNECTION_CLOSE` `NO_ERROR` at the highest encryption level both sides have, then the connection is discarded |
   | Head timeout | A request stream's `HEADERS` frame not complete within `--head-timeout` from its first byte | The stream: `RESET_STREAM` and `STOP_SENDING` `H3_REQUEST_INCOMPLETE` (0x10d) |
   | Maximum framed message (`--max-message`) | Advertised as `SETTINGS_MAX_FIELD_SECTION_SIZE`. A `HEADERS` frame longer than it | The stream is answered `:status 431` with FIN, then `STOP_SENDING` `H3_NO_ERROR` (RFC 9114 section 4.1); without a dynamic table (decision 8) the rest of the connection is untouched |
   | Frame size | Any other frame but `DATA` longer than `--max-message` | `CONNECTION_CLOSE` `H3_EXCESSIVE_LOAD` (0x107) |
   | Concurrent streams | `initial_max_streams_bidi` 100, renewed by `MAX_STREAMS` as streams close; a stream past the credit | `CONNECTION_CLOSE` `STREAM_LIMIT_ERROR` (0x04), RFC 9000's own rule |
   | Flow-control windows | Data past the credit decision 4 grants | `CONNECTION_CLOSE` `FLOW_CONTROL_ERROR` (0x03) |
   | Maximum upload (`--max-filesize`) | A `content-length` over the limit, or `DATA` past it | `:status 413` with FIN, then `STOP_SENDING` `H3_NO_ERROR`; the partial upload is deleted |
   | Idle timeout | No packet either way for `--idle-timeout`, sent as `max_idle_timeout` | The connection is discarded silently (RFC 9000 section 10.1): ADR-0006's "close with no bytes" |
   | Maximum duration | `--max-time` reached | `GOAWAY` naming the next request stream the server would accept, then `CONNECTION_CLOSE` `H3_NO_ERROR` (0x100) |
   | Shutdown (ADR-0059) | The listener stops | `GOAWAY` as above; open streams finish within the shutdown grace; then `CONNECTION_CLOSE` `H3_NO_ERROR` |

   Protocol errors that are not limits follow RFC 9114 and RFC 9000: a malformed request (a
   missing or repeated pseudo-header, an uppercase field name, a connection-specific field) is a
   stream error `H3_MESSAGE_ERROR` (0x10e); a frame not allowed where it arrived
   `H3_FRAME_UNEXPECTED` (0x105); a malformed frame `H3_FRAME_ERROR` (0x106); a first control
   frame other than `SETTINGS` `H3_MISSING_SETTINGS` (0x10a); a closed control or QPACK stream
   `H3_CLOSED_CRITICAL_STREAM` (0x104); a second control or QPACK stream of one type
   `H3_STREAM_CREATION_ERROR` (0x103); a QPACK decoding failure `QPACK_DECOMPRESSION_FAILED`
   (0x200); a QUIC frame or packet error the transport code RFC 9000 section 20.1 names. Curl's
   exit code for each limit is measured by BL-385 against a live surl; where it differs from
   this table's expectation, the table is corrected by amendment.
8. **QPACK: no dynamic table.** The server sends `SETTINGS_QPACK_MAX_TABLE_CAPACITY` 0 and
   `SETTINGS_QPACK_BLOCKED_STREAMS` 0 (both the defaults, so neither is sent), so curl's encoder
   uses only the static table and literals and no request stream ever waits on another. The
   server's encoder does the same, with Huffman literals where shorter. Each side's encoder and
   decoder streams carry nothing: the server opens its control stream (`SETTINGS` first) and its
   QPACK encoder and decoder streams, as nghttp3 does, and accepts the client's. Without a
   dynamic table a request stream can be reset alone, which decision 7 relies on, and every
   field section decodes on its own.
9. **What carries over from HTTP/2.**
   - *The answering shape.* `HttpRequestAnswerer` and `HttpAnswer` (ADR-0081 decision 8) answer
     HTTP/3 requests; BL-383's HTTP/3 writer turns an `HttpAnswer` into `HEADERS` and `DATA`
     frames on the request stream, then FIN. A log line's version reads `HTTP/3`.
   - *No server push.* The server sends no `PUSH_PROMISE` and ignores `MAX_PUSH_ID` and
     `CANCEL_PUSH`. `PRIORITY_UPDATE` frames (RFC 9218) and the `priority` field are read and
     ignored. No extended `CONNECT` (`SETTINGS_ENABLE_CONNECT_PROTOCOL` is not sent), no
     HTTP datagrams.
   - *Uploads, `100 Continue` and request-bound logins* as ADR-0081 decision 7 says for HTTP/2.
   - *Logins bound to a connection* - NTLM and Negotiate - are answered with a stream reset
     `H3_VERSION_FALLBACK` (0x110), RFC 9114's code for "retry over HTTP/1.1", where HTTP/2 uses
     `HTTP_1_1_REQUIRED`. Whether curl then retries over TCP is measured by BL-385.

## Alternatives considered

- **`System.Net.Quic` where `QuicListener.IsSupported`, hand-built elsewhere.** Rejected: two
  QUIC stacks, two sets of wire bytes to pin per platform, a native file beside the executable on
  Windows, and the seam bypassed on the platforms that use it.
- **A new QUIC contract in `Surl.Protocol.Abstractions`, routing by connection ID.** Rejected for
  now: it only pays with connection migration, which curl does not do and the server disables.
- **HTTP/3 on by default, as HTTP/2 is.** Rejected: HTTP/2 rides the TLS stack HTTPS already uses,
  while HTTP/3 adds a UDP socket and a hand-built TLS 1.3 on every HTTPS listener. Curl never
  asks for HTTP/3 unless told to, so off by default costs a default curl nothing.
- **An `--alt-svc <seconds>` option.** Rejected: `Alt-Svc` is only true when HTTP/3 is served,
  so `--http3` decides it, and one option is less surface than two.
- **Retry on every connection.** Rejected: an extra round trip for every curl, for a defence the
  anti-amplification limit and ADR-0006's connection limits already give.
- **A QPACK dynamic table.** Rejected: curl's requests are small, and a dynamic table would make
  every stream depend on the encoder stream and stop decision 7 from resetting one stream alone.

## Consequences

Good:

- One QUIC and one TLS 1.3 handshake on all three platforms, every byte Surl's own, with no
  native file and nothing to install (NFR-006).
- Every byte the HTTP/3 tasks pin about curl's ClientHello was measured from S, L and W first.

Costs and caveats:

- Four new libraries and a hand-built ML-KEM are a large body of cryptographic code, each held to
  the quality gates; RFC 8448, RFC 9001 Appendix A and FIPS 203's vectors test them.
- The macOS build's ClientHello is inferred from its sources and recorded only when BL-385 runs in
  CI. The behaviour of curl past each limit, for `H3_VERSION_FALLBACK`, and for `Alt-Svc` with
  `--alt-svc` is not yet measured; BL-385 measures it.
- The recorder sends nothing, so nothing here shows curl accepting a server flight; BL-376's
  test-side client and BL-385's live runs do.
