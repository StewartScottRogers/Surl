---
id: BL-1697
title: Withhold passwords in SOCKS-tunnelled text protocols from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1697 — Withhold passwords in SOCKS-tunnelled text protocols from the exchange log

## Goal

With `-v`, `--trace`, `--trace-ascii` or `--log-file`, a SOCKS4, SOCKS4a or SOCKS5 listener's log writes an FTP
`PASS`/`ACCT` argument and an HTTP `Authorization`/`Proxy-Authorization` credential carried through its tunnel as
`****`, as the origin servers' own log lines already do.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 1
  (High, security), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report:
  - `surl --directory site --user app:s3cret-pw --allow-plaintext-auth http://localhost:18080/ ftp://localhost:18021/ socks5h://127.0.0.1:18108/ -v --log-file socks.log` (`site/hello.txt` present).
  - `curl -u app:s3cret-pw -x socks5h://127.0.0.1:18108 ftp://localhost:18021/hello.txt`
  - `curl --basic -u app:s3cret-pw -x socks5h://127.0.0.1:18108 http://localhost:18080/hello.txt`
  - `socks.log` shows the proxy's exchange `#1 < PASS s3cret-pw\r\n` and `#4 < Authorization: Basic YXBwOnMzY3JldC1wdw==\r\n`.
    The origin's exchanges show `#2 < PASS ****\r\n` and `#5 < Authorization: Basic ****\r\n`.
  - Expected: `****` on the proxy's lines too.
- Why it happens: BL-1434 built `Socks5PasswordRedactor`
  (`Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/Socks5PasswordRedactor.cs`), which masks only the RFC 1929
  `PASSWD` and logs everything after the subnegotiation, the tunnel included, unchanged (its doc comment, line 13).
  ADR-0182 Amendment 2 already decided the opposite for the HTTP proxy: tunnelled bytes after `CONNECT`'s `200`
  pass through the same redactor, so tunnelled plaintext HTTP has its `Authorization` masked. The SOCKS tunnel is
  the one gap. `SocksProtocolServer.CreateRedactor` is where the redactor is made.
- Decision (Claude under Stewart's delegation; the lane records it as ADR-0182 Amendment 6):
  1. After the server sent a success reply (SOCKS5 `05 00 …`, SOCKS4 `00 5A …`, seen through
     `IReceivedBytesRedactor.ObserveSent`), the client's tunnelled bytes are redacted by a tunnel phase.
  2. The SOCKS server cannot know the tunnelled protocol, so it looks at the first tunnelled client byte: an
     ASCII letter (`A`-`Z`, `a`-`z`) starts a text protocol's command or request line, and the rest of the tunnel
     goes through `LineBytesRedactor` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/LineBytesRedactor.cs`)
     with a new SOCKS-library line rule; any other first byte (TLS `0x16`, MQTT, LDAP BER and other binary
     protocols) passes through unchanged as today, so ciphertext and binary frames are never held or mangled.
  3. The rule masks, case-insensitively, the argument of a `PASS ` or `ACCT ` command line (`PASS ****`), and the
     credentials of an `Authorization:` or `Proxy-Authorization:` header line keeping the scheme word
     (`Authorization: Basic ****`), as ADR-0182 decision 1 says. Every other line is written as received. A body
     line that happens to match is masked too: that loses log detail and never reveals a secret.
  4. The SOCKS library may not reference `Surl.HttpMessage` (protocol servers reference only ADR-0002 decision 3's
     table), so the rule is its own small class in the SOCKS library, not `HttpRequestHeadLineRule`.
  5. The event stream keeps raw bytes (ADR-0182 decision 4); `SocksUpstreamTestCaseServer` keeps raw bytes.
- Stand-in: no store is involved; what a test observes on the event stream is unchanged (raw bytes). The change
  is to the levelled log only.

## Acceptance criteria

- [x] Named fast tests in `Protocols/Proxy/Surl.Protocol.Socks.UnitTests` (in `SocksExchangeLogRedactionTests.cs`)
      show, for a SOCKS5 tunnel with no authentication and one with RFC 1929, that the tunnelled lines
      `PASS s3cret-pw\r\n` and `Authorization: Basic YXBwOnMzY3JldC1wdw==\r\n` are logged as `PASS ****\r\n` and
      `Authorization: Basic ****\r\n`, also when split across reads at every byte.
- [x] A named test shows the same for a SOCKS4 tunnel, and one shows `ACCT` and `Proxy-Authorization` masked.
- [x] Named tests show a tunnel whose first client byte is `0x16` or `0x10` logged byte for byte unchanged, and a
      non-secret tunnelled line (`USER app\r\n`, `GET /hello.txt HTTP/1.1\r\n`) logged as received.
- [x] Regression test for this report: a test named for the repro drives the report's FTP and HTTP exchanges
      through `SocksProtocolServer` with a recording log and asserts no `s3cret-pw` and no `YXBwOnMzY3JldC1wdw==`
      in the logged text.
- [x] `Documentation/Planning/Decisions/ADR-0182-…md` has an "Amendment 6 (BL-1697)" stating decisions 1 to 5
      above, and the doc comment of the SOCKS redactor no longer says the tunnel is logged unchanged.
- [x] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary -warnaserror` is clean, the fast tests are green
      (no `TestCategory=Integration` needed), and the SOCKS library stays at 100% line and branch coverage.

## Notes

- Out of scope, to be filed by the lane as follow-up tasks if it confirms the gap: SASL logins (SMTP/IMAP/POP3
  `AUTH`, IMAP `LOGIN`) and binary logins (MQTT `CONNECT`, LDAP simple bind) carried through a SOCKS tunnel, and a
  sentence in `surl --aihelp security` (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`) naming the SOCKS tunnel.
- Delivered in session from the task's decided design: `SocksExchangeBytesRedactor` (handshake, then tunnel),
  `SocksTunnelBytesRedactor` (text or binary by the first byte) and `SocksTunnelLineRedactionRule`.
- Defaults taken beyond decisions 1 to 5, to keep a secret out of the log: a folded continuation of a credential
  header is masked (as the origin's rule does), and a bare `LF` inside a held line ends a line. Both are in Amendment 6.
- A success reply is `00 5A`, or `05 00` longer than two bytes, so the method selection `05 00` starts no tunnel.
- Review (code-reviewer): no defect in the design. HTTP/2 cleartext in a tunnel is not masked: recorded as a limit in
  Amendment 6 and filed as BL-1704. The test double now shows the redactor a write after it is sent, as the engine does.
- No wire byte changed, so no upstream curl measurement was needed. No live run of `surl` with curl was made; the
  report's exchanges are the fast regression test.
- Filed: BL-1704 (HTTP/2), BL-1705 (SASL, IMAP LOGIN), BL-1706 (MQTT, LDAP), BL-1707 (clients that send ahead),
  BL-1708 (`--aihelp logging` and manual text; `Surl.Cli` is outside this task's `touches`).
- Verified: build `-warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests green (28981 passed,
  3 skipped), SOCKS library 100% line and branch coverage.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. SOCKS4 and SOCKS5 tunnels carrying a text protocol log PASS, ACCT, Authorization and Proxy-Authorization secrets as ****.
