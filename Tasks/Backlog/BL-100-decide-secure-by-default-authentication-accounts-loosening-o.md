---
id: BL-100
title: Decide secure-by-default authentication, accounts, loosening options and --self-signed, and record ADR-0032
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-100 — Decide secure-by-default authentication, accounts, loosening options and --self-signed, and record ADR-0032

## Goal

An Accepted ADR-0032, marked "Decided by Claude under Stewart's delegation", fixes every
open detail of Surl's secure-by-default authentication - accounts, the loosening options,
`--self-signed`, the contract protocol servers call and how each Phase 1 server applies
it - and `Requirements.md` carries the rows for it, so BL-108 to BL-122 can be built
without asking a question.

## Context

Stewart approved the feature on 2026-09-29 (his words, summarised):

1. **Secure by default.** With no accounts configured, every protocol that has a login
   (HTTP auth, and later FTP, IMAP, POP3, SMTP AUTH, SSH, SMB, LDAP bind, MQTT CONNECT
   credentials, ...) refuses every login. A plain-text password over an unencrypted
   connection (HTTP Basic over `http://`, FTP `USER`/`PASS` without TLS, ...) is refused.
2. **Anonymous reads** over HTTP, Gopher and TFTP stay allowed by default, like a public
   web server; writes still need `--allow-uploads` and, where the protocol has a login, a
   login.
3. **Insecure by design stays as it is**: TFTP, Gopher, DICT and TELNET (no login, or plain
   text only) get no security bolted on.
4. **Accounts**: `--user name:password` (curl's name) and `--user-file <file>` for
   deployments, because a command-line password is visible to local users (ADR-0007
   section 4, rule 4 already refuses credentials in a listen URL for that reason).
5. **Loosening options**, each logged as a warning on every start: `--allow-anonymous`
   (accept missing or any credentials), `--allow-plaintext-auth` (a password over an
   unencrypted connection), `--auth <methods>` (the accepted methods: `basic`, `digest`,
   `ntlm`, `negotiate`, `bearer`, ..., mirroring curl's `--basic`/`--digest`/...), and
   `--self-signed` (generate a throwaway TLS certificate so secure schemes work in a test
   without `--cert`). **No umbrella `--insecure` option.**
6. Only what can land now is planned: this ADR, the options, `Surl.Authentication`'s account
   store and checks, the HTTP server's challenges (FR-014), MQTT CONNECT credentials and
   `--self-signed`. Servers not yet built (FTP, IMAP, POP3, SMTP, SSH, SMB, LDAP) get an
   acceptance-criterion note in this ADR, not tasks.

Where the code is today:
- `Surl.Authentication.UnitLibrary` holds only its `CLAUDE.md` and csproj (references
  `Surl.Protocol.Abstractions.UnitLibrary` only); its `CLAUDE.md` says protocol servers
  receive what it provides "through the contracts in Abstractions".
- ADR-0002's table (enforced by `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`)
  lets a protocol server reference only Abstractions, `Surl.Content.UnitLibrary` and
  `Surl.Cryptography.UnitLibrary`, so `Surl.Protocol.Http` and `Surl.Protocol.Mqtt` cannot
  reference `Surl.Authentication`.
- `Surl.Protocol.Http.UnitLibrary/HttpRequestResponder.cs` answers `GET`/`HEAD` with no
  authentication; `PUT` gets `405`. `HttpProtocolServer(ContentStore)` is its constructor.
- `Surl.Protocol.Mqtt.UnitLibrary/MqttConnectJudge.cs` validates the connect flags but does
  not read the will, user name or password (ADR-0014 decision on CONNECT: "accepted and not
  read"); `MqttProtocolServer` claims both `mqtt` and `mqtts`.
- `Surl.Console/ServerTlsComposition.cs` makes a throwaway certificate whenever a secure
  scheme is served without `--cert` (ADR-0010 section 3, ADR-0020); FR-021 states it.
- `Surl.Console/CommandLineRunner.cs` composes every server explicitly.

Measured on 2026-09-29 while planning, with `Record-CurlExchange.ps1` and the pinned
reference build `C:\Program Files\Git\mingw64\bin\curl.exe` (curl 8.21.0, Schannel, SSPI,
SHA-256 `0E7737...8778`), two connections, a canned `401` with one
`WWW-Authenticate: Digest realm="r", nonce="abc", algorithm=<A>, qop="auth"` then a `200`,
`-sS --digest -u a:b http://127.0.0.1:P/x`:

| `algorithm=` | Exit | What curl did |
| --- | --- | --- |
| `MD5` | 0 | Sent `Authorization: Digest username="a",realm="r",nonce="abc",uri="/x",cnonce="...",nc=00000001,algorithm=MD5,response="...",qop="auth"` |
| `SHA-256` | 94 | `curl: (94) An authentication function returned an error` (no second request) |
| `SHA-512-256` | 94 | the same |

So the Windows reference build (SSPI Digest) answers only MD5; the Linux and macOS
reference builds (static OpenSSL, no SSPI, `UpstreamCurlBuilds.json`) use curl's own
Digest code, which the ADR must measure on CI or record as to be measured by BL-113/BL-118.
The Windows build lists `NTLM SPNEGO Kerberos SSPI`; the Linux and macOS builds list
`NTLM` but no `SPNEGO` or `Kerberos`.

The implementation tasks are already filed on these homes, which the ADR records as decided
unless it finds a rule they break (then it says so and the planner refiles):
- BL-108: `Surl.Cli` parses the six options into `SurlCommandLine`.
- BL-109: the contract a protocol server calls lives in `Surl.Protocol.Abstractions`
  (ADR-0002 unchanged: no server references `Surl.Authentication`).
- BL-110: `Surl.Authentication` keeps the accounts and applies the policy; BL-111 Basic and
  Bearer; BL-113 Digest (with BL-112's hand-built SHA-512/256); BL-119/BL-120 NTLM (with a
  hand-built MD4); BL-121 Negotiate carrying NTLM; BL-122 AWS Signature Version 4.
- BL-114: the HTTP server issues `401` challenges and verifies `Authorization` through the
  contract; BL-115: the MQTT server verifies CONNECT credentials through it.
- BL-116: `--self-signed` in `Surl.Console`; BL-117: `Surl.Console` composes accounts,
  policy and warnings; BL-118: conformance with pinned upstream curl.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0032-<slug>.md` exists (the next free number if
      0032 is taken; then use that number in the follow-up tasks BL-102 and BL-108 to
      BL-124), Status Accepted, dated 2026-09-29 or later, "Decided by Claude under
      Stewart's delegation", citing Stewart's approval of 2026-09-29.
- [ ] It decides and states each of these, with the reason:
      1. **Options**: exact syntax, argument kind, default, negatability and every error
         text (ADR-0007 section 2's style) of `--user`, `--user-file`, `--allow-anonymous`,
         `--allow-plaintext-auth`, `--auth` and `--self-signed`; whether `--user` repeats to
         add accounts (a stated difference from ADR-0007's "last value wins" if so) and
         how it combines with `--user-file`; what `--user name` with no `:password` does
         (curl prompts; a server cannot).
      2. **`--user-file` format**: encoding, one account per line, comments and blank lines,
         whether it holds passwords in clear or a hash (and which, BCL only), how Bearer
         tokens and AWS access keys are configured if not as accounts, and the exit code and
         stderr text for a missing, unreadable or malformed file (reusing ADR-0005 codes,
         e.g. `CouldNotReadFile` 37, or a new row by ADR-0005's rules).
      3. **Methods**: the `--auth` words (curl's names: `basic`, `digest`, `ntlm`,
         `negotiate`, `bearer`, `aws-sigv4`, ...), the separator, the default accepted set
         with no `--auth`, which of them count as a plain-text secret (Basic, Bearer, MQTT
         and every other clear password) and which do not (Digest, NTLM, Negotiate, SigV4).
      4. **HTTP policy** (FR-014): which requests need a login (anonymous `GET`/`HEAD`
         allowed by default; what changes once an account is configured; writes), what a
         request carrying credentials gets when no account is configured, the `401` and its
         `WWW-Authenticate` lines (order, realm text, one header per method), what Basic or
         Bearer over `http://` gets without `--allow-plaintext-auth`, and the Digest
         algorithms offered so the Windows reference build (MD5 only, measured above)
         completes.
      5. **MQTT policy** (FR-020): whether a CONNECT without credentials is refused by
         default (the request lists only HTTP, Gopher and TFTP as anonymous), the CONNACK
         return code for a bad or refused login (MQTT 3.1.1 section 3.2.2.3: 4 or 5), and
         what a password over `mqtt://` (not `mqtts://`) gets without
         `--allow-plaintext-auth`.
      6. **The contract** BL-109 adds to Abstractions: type and member names, how a server
         says whether its connection is encrypted (`IConnection.TlsSession`), how HTTP's
         connection-bound NTLM and Negotiate handshakes keep state, and that a new HTTP
         method added to `Surl.Authentication` later needs no change to the HTTP server or
         `Surl.Console`.
      7. **Whether `Surl.Authentication` may reference `Surl.Cryptography`** (MD4, SHA-512/256)
         and the product overview's "Layers" row that must then change (BL-124 edits it).
      8. **Password checks**: constant-time comparison (`CryptographicOperations.FixedTimeEquals`),
         nothing a peer can learn about which accounts exist (ADR-0006 section 3), and
         whether failed logins are rate-limited or delayed (injected `TimeProvider`).
      9. **Warnings**: the exact line each loosening option writes on every start, and the
         log level it is written at (coordinated with BL-101's ADR-0033).
      10. **`--self-signed`**: that without `--cert` and without `--self-signed` a secure
          listen URL (`TlsSchemes.IsImplicitTls`) is refused before any listener binds,
          with the exit code and stderr text; that `--self-signed` with `--cert` is refused
          or ignored (say which); and that the throwaway certificate itself is ADR-0010
          section 3's, unchanged.
      11. **Kerberos**: that Negotiate carries NTLM now (BL-121) and Kerberos inside
          Negotiate is later work, not a refusal (root `CLAUDE.md`, "Decisions").
- [ ] It has a section "Protocol servers not yet built" stating, as an acceptance
      criterion for each of FTP, IMAP, POP3, SMTP, SSH (SCP, SFTP), SMB and LDAP, that its
      login goes through the BL-109 contract, is refused with no accounts, and refuses a
      clear password before TLS without `--allow-plaintext-auth`; and that TFTP, Gopher,
      DICT and TELNET stay as they are.
- [ ] It states it supersedes ADR-0010 section 3's "No `--cert` for a secure scheme" default
      and ADR-0014's "user name and password accepted and not read"; ADR-0010 and ADR-0014
      each gain one "Superseded in part" line under their Status naming ADR-0032, and
      nothing else in them changes.
- [ ] `Documentation/Planning/Decisions/README.md`'s index lists ADR-0032.
- [ ] `Documentation/Product/Requirements.md`: FR-014 is reworded to the decided policy and
      cites ADR-0032; FR-021 no longer says the throwaway certificate is used without
      `--cert` and names `--self-signed`; FR-008 lists the six new options; new rows (next
      free FR numbers) cover accounts and `--user-file`, refusal with no accounts, the
      plain-text refusal, the loosening options and their warnings, and MQTT CONNECT
      credentials; FR-010 lists any new exit code. Each new row is Status Draft and names
      the pinned build it is measured against.
- [ ] No HTML comment remains in the ADR, and every statement in it about current code names
      a file that exists.

## Notes

Help text for the new options is placed by ADR-0034 (BL-102); this ADR only gives each
option its one-line description. Behaviour changes land in BL-108 onward, not here.

## Log

- 2026-09-29: Created.
