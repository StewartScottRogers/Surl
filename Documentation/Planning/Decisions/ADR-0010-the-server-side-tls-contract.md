# ADR-0010 — The server-side TLS contract

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

Upstream curl reaches TLS two ways, and Surl has to answer both:

- **TLS from the first byte** (implicit TLS): `https`, `wss`, `ftps` (implicit FTPS),
  `imaps`, `pop3s`, `smtps`, `ldaps`, `gophers`, `mqtts`, `smbs`.
- **An upgrade in the middle of a connection**: FTP `AUTH TLS`, SMTP and IMAP `STARTTLS`,
  POP3 `STLS`. Bytes before the upgrade are plaintext on the same TCP connection.

ADR-0002 ("Consequences") says a secure variant is the same protocol server over a
secured connection, never a second library. ADR-0004 section 2 says `IConnection` always
carries plaintext and leaves how it is secured to this ADR, and section 5 promises that
the exchange log still records plaintext after an upgrade. Only `Surl.Networking`
constructs an `SslStream` (root `CLAUDE.md`). ADR-0006 section 4 fixes the accepted
versions (TLS 1.2 and 1.3 by default, `--tlsv1.x` and `--tls-max`), puts the handshake
inside the head timeout, turns renegotiation off, and says a secure connection past a
connection limit is closed without a handshake. ADR-0007 parses `--cert`, `--key` and
`--cacert` and leaves their formats, and what a secure scheme does without `--cert`, to
this ADR.

BL-006 writes the contract into `Surl.Protocol.Abstractions.UnitLibrary`; BL-012
implements it over `SslStream` in `Surl.Networking.UnitLibrary`.

### What upstream curl 8.21.0 does against a throwaway certificate

Measured with `Record-CurlExchange.ps1 -Tls` (self-signed RSA 2048 certificate for
`CN=127.0.0.1`, subject alternative name IP `127.0.0.1`, valid one day), the build pinned
in `UpstreamCurlBuilds.json` (curl 8.21.0, x86_64-w64-mingw32, Schannel),
`https://127.0.0.1:18743/`, response `HTTP/1.1 200 OK` with a 5-byte body, server
accepting TLS 1.2 and 1.3 (`-TlsProtocol Tls12AndTls13`), 2026-09-28:

| curl arguments | Exit code | First line of stderr |
| --- | --- | --- |
| `-sS -k` | 0 | (none) |
| `-sS` | 60 | `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.` |
| `-sS -k -v` | 0 | (verbose) `*   Trying 127.0.0.1:18743...` |

The same build and tool, other cases that fix a point below:

| Case | curl arguments | Exit code | What it shows |
| --- | --- | --- | --- |
| Verbose ALPN lines | `-sS -k -v` | 0 | `* ALPN: curl offers http/1.1`, then `* ALPN: server did not agree on a protocol. Uses default.` (the recorder offers no ALPN) |
| HTTP/1.0 | `-sS -k -v --http1.0` | 0 | `* ALPN: curl offers http/1.0,http/1.1` |
| HTTP/2 | `-sS -k -v --http2` | 2 | `curl: option --http2: the installed libcurl version does not support this` (`curl -V` lists no `HTTP2` feature) |
| SNI to an IP | `-sS -k -v` | 0 | `* schannel: using IP address, SNI is not supported by OS.` |
| Private root, revocation checked | `-sS --cacert root.pem` (`-TlsRootCertificateFile`) | 60 | `curl: (60) schannel: the revocation status is unknown` |
| Private root, revocation off | `-sS --ssl-no-revoke --cacert root.pem` | 0 | (none); stdout `hello` |
| Missing `--cert` file | `-sS -k --cert nosuch.pem` | 58 | `curl: (58) schannel: Failed to get certificate location or file for <path>` |
| Missing `--cacert` file | `-sS --cacert nosuch.pem` | 2 | `curl: The file '<path>' ` / `provided to --cacert does not exist`, then `curl: option --cacert: is badly used here` |

So: the pinned build needs `-k` (or `--cacert` plus `--ssl-no-revoke`) to accept a
certificate Surl made itself; it offers ALPN `http/1.1` for every `https` request and has
no HTTP/2; and with `-v` over TLS 1.3 it also logs
`* schannel: remote party requests renegotiation`, which is Schannel's reading of TLS 1.3
post-handshake messages (session tickets), not a TLS 1.2 renegotiation.

### Alternatives considered and rejected

- **Hand a protocol server an `SslStream`, or a `Stream` it can wrap.** Breaks the rule
  that only `Surl.Networking` constructs one, and a server could then read past the
  recording decorator (ADR-0004 section 5).
- **A second `ISecureConnectionProtocolServer` interface, or an `https` server beside the
  `http` one.** ADR-0002 rules out a second server; a second interface would make every
  protocol with both an implicit and an upgrade form implement two.
- **Handshake inside `IConnectionListener.AcceptAsync`.** One slow client would stall every
  accept, the handshake would run outside the exchange's head timeout, and a connection
  past a limit would already have cost a handshake - all against ADR-0006.
- **A separate `ITlsUpgrader` service injected into servers.** The upgrade acts on one
  connection's transport; putting it anywhere but on the connection means handing the
  transport around.
- **Refuse a secure listen URL with no `--cert`.** Every local test of `https` would first
  need a certificate made by hand, while upstream curl's `-k` exists for exactly this case.
- **Guess the certificate format from the file's content or extension.** curl names the
  format with `--cert-type`; one option says what it does, a guess hides it.

## Decision

### 1. The Abstractions contract

All types below live in `Surl.Protocol.Abstractions.UnitLibrary`, namespace
`Surl.Protocol.Abstractions`. Every type they use is in the shared framework
(`System.Security.Authentication.SslProtocols`, `System.Net.Security.TlsCipherSuite`,
`System.Security.Cryptography.X509Certificates.X509Certificate2`), so Abstractions still
references nothing.

#### `IConnection` gains two members

```csharp
public interface IConnection : IAsyncDisposable
{
    // ADR-0004 section 2, unchanged:
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    ValueTask CompleteWritesAsync(CancellationToken cancellationToken);
    void Abort();

    // Added by this ADR:
    TlsSession? TlsSession { get; }
    ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken);
}
```

- **`TlsSession`** is `null` while the connection is plaintext and the negotiated session
  once a handshake has completed. It never changes back.
- **`UpgradeToTlsAsync`** runs the server side of a TLS handshake on the connection, with
  the listener's TLS settings (section 3), and returns the session. From then on
  `ReadAsync` returns decrypted bytes and `WriteAsync` encrypts: `IConnection` still
  carries plaintext, as ADR-0004 promised, and the engine's recording decorator forwards
  the call to the connection it wraps, so the exchange log records plaintext on both sides
  of the upgrade.
  - Calling it on a connection whose `TlsSession` is not `null` throws
    `InvalidOperationException`, as does calling it after `CompleteWritesAsync` or while
    a read or write is pending.
  - A handshake that fails - the client's alert, a version outside the accepted range, a
    client certificate that fails verification (section 5), or the peer closing - throws
    `TlsHandshakeException`. A handshake cut off by `cancellationToken` throws
    `OperationCanceledException`, like every other `IConnection` call (ADR-0004); whoever
    passed the head-timeout token treats that as a timed-out handshake. After either, the
    connection is unusable and nothing more is written to it (ADR-0006 section 4: no bytes
    after the TLS alert).
- **Pipelined plaintext is thrown away, never served.** A server upgrading after
  `STARTTLS`, `STLS` or `AUTH TLS` discards every byte it has read beyond the end of that
  command line before calling `UpgradeToTlsAsync`, and never treats them as commands
  inside the TLS session (the STARTTLS command-injection defect class, RFC 3207 section
  4.2's "MUST discard"). `Surl.Networking` never reads ahead of `ReadAsync`, so the only
  such bytes are in the server's own buffer. Each protocol server's tests pin this.

#### `TlsSession` - what a server learns

```csharp
public sealed record TlsSession(
    SslProtocols Protocol,
    TlsCipherSuite CipherSuite,
    string? ApplicationProtocol,
    string? ServerName,
    X509Certificate2? ClientCertificate);
```

| Member | Meaning |
| --- | --- |
| `Protocol` | The negotiated version: `SslProtocols.Tls12` or `SslProtocols.Tls13` by default (ADR-0006 section 4). |
| `CipherSuite` | The negotiated cipher suite, for the verbose log. |
| `ApplicationProtocol` | The ALPN protocol ID agreed, as its ASCII string (`http/1.1`), or `null` when the client offered none or none was agreed. |
| `ServerName` | The SNI host name the client sent, or `null`. The pinned build sends none to an IP literal (measured above). |
| `ClientCertificate` | The client's certificate when client verification is on (section 5) and it passed; otherwise `null`. A server never sees a certificate that failed verification, because that handshake failed. |

#### `TlsHandshakeException`

`public sealed class TlsHandshakeException : IOException`, with the message of the
platform exception behind it and that exception as `InnerException`. Deriving from
`IOException` keeps ADR-0004's rule that a transport failure surfaces as `IOException`.

#### `TlsSchemes` - which schemes are TLS from the first byte

`public static class TlsSchemes` with `public static bool IsImplicitTls(string scheme)`,
true for exactly `https`, `wss`, `ftps`, `imaps`, `pop3s`, `smtps`, `ldaps`, `gophers`,
`mqtts` and `smbs`: the schemes upstream curl opens with a TLS handshake. `ftps` is
implicit FTPS; explicit FTPS is `ftp` with `AUTH TLS`, as in curl. A static pure function
over a fixed list needs no injection, and the engine and the listener factory read the
same list.

### 2. Who performs which handshake

- **Implicit TLS is the engine's.** `IConnectionListener.AcceptAsync` returns a plaintext
  connection for every scheme. For a listen URL whose scheme `TlsSchemes.IsImplicitTls`
  names, `Surl.Core`'s engine - after the connection-limit check (a connection past a
  limit is closed without a handshake, ADR-0006 section 5) - calls
  `UpgradeToTlsAsync` with the exchange's head-timeout token (ADR-0006 section 4), and
  only then calls `ServeAsync`. So an `https` server receives a connection whose
  `TlsSession` is already set, and the HTTP server serves `http` and `https` with the same
  code, reading `connection.TlsSession` only where the protocol cares.
- **An upgrade is the server's.** The FTP, SMTP, IMAP and POP3 servers call
  `UpgradeToTlsAsync` themselves after answering `AUTH TLS`, `STARTTLS` or `STLS`.
- **Failures.** When the engine's implicit handshake throws `TlsHandshakeException`, the
  engine notes it, disposes the connection without writing, and never calls `ServeAsync`.
  When a server's upgrade throws it, the server lets it escape (there is nothing more to
  say on that connection); the engine treats a `TlsHandshakeException` from `ServeAsync`
  as a failed handshake, not as a server fault. Either way the verbose note is
  `TLS handshake failed: <message>` (not `Protocol server threw ...`), and a completed
  handshake is noted `TLS handshake completed: <protocol>, <cipher suite>, ALPN
  <application protocol or "none">`. BL-012 and the engine's task pin the exact texts
  from these shapes. A failed handshake never ends the process and has no exit code: it
  is an exchange outcome.

### 3. Where the server certificate and key come from

The options keep curl's names; curl's client-side meaning ("my certificate") becomes the
server's own certificate.

| Option | Argument | Meaning in surl | Default |
| --- | --- | --- | --- |
| `--cert` | `<file>` | The server certificate, optionally followed by its intermediate certificates (sent in the handshake in file order), and with PEM optionally the private key too. | none: a throwaway certificate (below) |
| `--cert-type` | `<PEM\|DER\|P12>` | The format of `--cert`, curl's names. Case-insensitive, as in curl. | `PEM` |
| `--key` | `<file>` | The private key for `--cert`. | the key in the `--cert` file |
| `--key-type` | `<PEM\|DER>` | The format of `--key`. | `PEM` |
| `--pass` | `<phrase>` | The passphrase for an encrypted private key or a PKCS#12 file. | none |

- **Formats.** `PEM`: one or more `CERTIFICATE` blocks, the first being the server's own;
  a key block is read from `--key` or, without `--key`, from the same file. `DER`: exactly
  one certificate; `--key` is required. `P12`: a PKCS#12 file holding the certificate, its
  key and optionally intermediates; `--key` is refused with it. A key is PKCS#8
  (`PRIVATE KEY`), encrypted PKCS#8 (`ENCRYPTED PRIVATE KEY`, with `--pass`), PKCS#1
  (`RSA PRIVATE KEY`) or SEC1 (`EC PRIVATE KEY`) in PEM, and PKCS#8 or encrypted PKCS#8 in
  DER. RSA (2048 bits or more) and ECDSA (P-256, P-384, P-521) keys are served. An
  Ed25519 or Ed448 key cannot be served through `SslStream` on any platform in .NET 10,
  so in Phase 1 it is `CertificateProblem` (below); serving one is later work, not a
  refusal.
- **Any other `--cert-type` or `--key-type` word** (curl also accepts `ENG` and `PROV` for
  OpenSSL engines and providers) is `FailedInit` (2), like every other word outside the
  allowed set (ADR-0007, row 36: `option --cert-type: is badly used here`). So are `--key`,
  `--key-type` or `--pass` without `--cert`, and `--key` with `--cert-type P12`.
- **Exit codes.** Two rows join ADR-0005's table, both upstream curl's numbers with the
  same meaning from the server's side:

  | `SurlExitCode` member | Number | Upstream `CURLE_*` | Failure it covers |
  | --- | --- | --- | --- |
  | `CertificateProblem` | 58 | `CURLE_SSL_CERTPROBLEM` | `--cert` or `--key` is missing, unreadable, not in the named format, has a key that does not match the certificate, needs a `--pass` it was not given or was given a wrong one, or holds a key type Surl cannot serve. Measured: curl returns 58 for a `--cert` file that does not exist. |
  | `CaCertificateBadFile` | 77 | `CURLE_SSL_CACERT_BADFILE` | `--cacert` exists but holds no certificate Surl can read. |

  A `--cacert` file that does not exist is `FailedInit` (2), as measured from curl above.
  All three files are read at startup, before any listener binds, so a bad file ends the
  process before `Listening on ...` is written; the stderr texts are pinned by the task
  that implements them, from the measured curl lines above in the `surl: ` form of
  ADR-0007 section 5.
- **No `--cert` for a secure scheme.** Surl serves a **throwaway certificate** it makes
  at startup with `CertificateRequest`: self-signed, RSA 2048 with SHA-256 and PKCS#1
  signature padding (the key and algorithm measured above), subject `CN=surl throwaway`,
  subject alternative names DNS `localhost` and IP `127.0.0.1` and `::1` plus every listen
  URL's host, `NotBefore` one hour before now and `NotAfter` thirty days after, both from
  the injected `TimeProvider`, basic constraints not a CA, key usage digital signature and
  key encipherment, extended key usage server authentication. It lives only in memory and
  is never added to a certificate store. Upstream curl then needs `-k` (measured: exit 60
  without it, 0 with it). With `-v` the verbose log notes, once at startup,
  `Serving a throwaway certificate, SHA-256 <fingerprint>`, so an operator can tell it is
  in use. It is made only when a listen URL is an implicit-TLS scheme or a protocol that
  can upgrade (`ftp`, `smtp`, `imap`, `pop3`) is served, and at most once per process;
  plain `http` never pays for an RSA key.
- **One certificate for the process.** Every listener serves the same certificate. A
  certificate per listen URL, or chosen by SNI, would need an option curl has no name for,
  and no upstream curl case needs one.

### 4. ALPN

- For `https` and `wss` in Phase 1, `Surl.Networking` offers exactly one ALPN protocol
  ID, **`http/1.1`**. The pinned build offers `http/1.1` for every `https` request
  (`http/1.0,http/1.1` with `--http1.0`, measured), so it always agrees on `http/1.1`, and
  the pinned build has no HTTP/2 to ask for `h2` with. `h2` joins the list with the HTTP/2
  server, in the ADR that task records; `h3` is QUIC's and never travels over `SslStream`.
- For every other scheme no ALPN list is offered, so none is agreed: upstream curl sends
  ALPN only for HTTP.
- A client that offers no ALPN is served without it (`ApplicationProtocol` is `null`).

### 5. Client-certificate verification for upstream curl's `--cert`

- **Switched on by `--cacert <file>`**, curl's name for "the CA certificates that verify
  the peer". With it, every TLS handshake - implicit and upgrade, every listener -
  requests a client certificate and **requires** one: a client that sends none, or one
  that does not chain to a trust anchor, fails the handshake (`TlsHandshakeException`,
  section 2). Without `--cacert`, no client certificate is requested. There is no
  "request but do not require" mode: upstream curl sends the same thing either way, and
  one option means one behaviour.
- **The trust anchors are exactly the certificates in `--cacert`**: a PEM file of one or
  more `CERTIFICATE` blocks, or one DER certificate (curl's own `--cacert` reads PEM; DER
  is accepted too because it costs nothing and fails no curl case). The operating system's
  trust store is never consulted: the chain is built with
  `X509ChainTrustMode.CustomRootTrust` and `CustomTrustStore` holding those
  certificates, with `X509RevocationMode.NoCheck` (a throwaway CA has no revocation
  endpoint, and upstream curl itself needs `--ssl-no-revoke` for one, measured above),
  the verification time taken from the `TimeProvider`, and the client-authentication
  extended key usage required when the certificate carries an extended key usage
  extension.
- **What the server learns** is `TlsSession.ClientCertificate`.
- **No CA list is sent in the certificate request** (`SslCertificateTrust` with
  `sendTrustInHandshake: false`), because Windows cannot send one; upstream curl sends the
  certificate `--cert` names without needing the list.
- **Curl's side on the pinned build.** Schannel reads `--cert` as a PKCS#12 file
  (`--cert-type P12`) or a Windows certificate-store path, and logs
  `schannel: disabled automatic use of client certificate` when none is given (measured
  above with `-v`). BL-012's conformance follow-up measures a verified client certificate
  end to end; the recorder does not yet request one.

### 6. Platform differences, and how BL-012's tests stay platform-neutral

| Difference | Windows | Linux | macOS | What Surl does |
| --- | --- | --- | --- | --- |
| A server key that exists only in memory (PEM import, `CertificateRequest`) | Schannel cannot use an ephemeral key for a server handshake | works (OpenSSL) | `EphemeralKeySet` is not supported | Every server certificate, loaded or made, is exported to PKCS#12 in memory and re-imported with `X509CertificateLoader.LoadPkcs12`: `EphemeralKeySet` on Linux, default key storage on Windows and macOS. On Windows the re-imported key is a temporary key container, deleted when the listener factory is disposed, so no key outlives the process. One `SslStreamCertificateContext` is built per process with the intermediates. |
| TLS 1.3 on the server side | supported (Windows 11 / Server 2022) | supported (OpenSSL 1.1.1+) | not supported by .NET's server-side `SslStream` | Nothing to work around (ADR-0006 section 4): on macOS the default range negotiates TLS 1.2, and `--tlsv1.3` there makes every handshake fail. |
| TLS 1.0 and 1.1 | disabled in Schannel by default | depends on the OpenSSL security level | per the OS | `--tlsv1.0`/`--tlsv1.1` ask; the OS decides (ADR-0006 section 4). |
| ALPN offered but no protocol in common | the handshake completes without ALPN | OpenSSL ends it with `no_application_protocol` | per the OS | Not reachable from the pinned build, which always offers `http/1.1` (section 4). Not normalised; each behaviour is pinned in its own `[OSCondition]` test if BL-012 tests it. |
| Sending the CA list in a certificate request | not supported | supported | supported | Never sent (section 5). |
| Cipher suites | OS defaults | OS defaults | OS defaults | OS defaults; `CipherSuitesPolicy` is not supported on Windows (ADR-0006 section 4). |

BL-012's tests hold on all three platforms because:

- **Every certificate and key is generated in the test** with `CertificateRequest`
  (RSA 2048 and ECDSA P-256; a CA, a leaf it signs and a stranger for client
  verification), written where a file is needed to a directory under
  `Path.GetTempPath()` and deleted afterwards. Never an `X509Store`, never a Windows
  certificate store, never a committed certificate or key file.
- **Handshakes run over an in-memory duplex stream pair** with an `SslStream` client in
  the test, so they open no socket and are fast tests; the members that only wrap a
  socket stay under ADR-0004 section 8.
- **No test asserts one negotiated version** except under `[OSCondition]`: a default
  handshake asserts `Tls12` or `Tls13`, and a TLS 1.3-only test runs with
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]`.
- **No test asserts platform error text.** A failed handshake is asserted by its type
  (`TlsHandshakeException`), never by its message.
- **Time comes from a fake `TimeProvider`** for the throwaway certificate's validity and
  for client-chain verification, so no test depends on the wall clock.

## Consequences

- **BL-006** adds to Abstractions: `IConnection.TlsSession`,
  `IConnection.UpgradeToTlsAsync`, `TlsSession`, `TlsHandshakeException` and
  `TlsSchemes`. `InMemoryConnection` gains an optional initial `TlsSession` (standing in
  for implicit TLS), a session to hand out on upgrade (default: TLS 1.3, no ALPN, no
  client certificate), an option to make the upgrade throw `TlsHandshakeException`, and
  an `UpgradeRequested` flag; after an upgrade it goes on replaying the same byte script,
  which is plaintext. Every existing `IConnection` implementation gains the two members.
- **BL-012** implements sections 3 to 6 in `Surl.Networking`: loading the three option
  files, the throwaway certificate, the handshake, ALPN, client verification and the
  platform table.
- **Follow-up work** filed with this ADR: BL-063, `Surl.Cli` parses `--cert-type`,
  `--key-type` and `--pass` (ADR-0007's table gains the three rows through this ADR);
  BL-064, `SurlExitCode` gains `CertificateProblem` (58) and `CaCertificateBadFile` (77);
  BL-065, `Surl.Core`'s engine performs the implicit handshake and notes its outcome
  (section 2). Because `Surl.Core`'s recording decorator implements `IConnection`, BL-006's
  two new members also change `Surl.Core` (a forwarding implementation), or that build
  breaks.
- ADR-0004's `IConnection` is extended as it said it would be, not superseded: every
  other member keeps its meaning.
- ADR-0005's table gains two rows, as its "Consequences" says a later failure does.
- Changing any of this is a new ADR that supersedes this one.
