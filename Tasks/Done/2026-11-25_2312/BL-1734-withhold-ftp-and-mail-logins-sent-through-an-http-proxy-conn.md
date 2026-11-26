---
id: BL-1734
title: Withhold FTP and mail logins sent through an HTTP proxy CONNECT tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1713]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1734 — Withhold FTP and mail logins sent through an HTTP proxy CONNECT tunnel from the exchange log

## Goal

FTP `PASS` and `ACCT` arguments and SMTP, IMAP and POP3 login credentials sent through an HTTP proxy `CONNECT` tunnel
are logged as `****`.

## Context

- Found while working BL-1713. The proxy judges tunnelled lines only as HTTP head lines (ADR-0182 Amendment 2):
  `HttpProxyConnectionBytesRedactor` (`Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary`) hands them to
  `HttpRequestHeadBytesRedactor`, which masks only HTTP credential fields.
- The SOCKS tunnel is the nearest model: `SocksTunnelLineRedactionRule` masks `PASS` and `ACCT` (ADR-0182 Amendment 6),
  and BL-1710 adds the mail rules there. Read what BL-1710 did if it is done.
- Protocol libraries never reference each other, so that rule and `ImapLineRedactionRule` cannot be referenced. The
  proxy library may reference the libraries ADR-0002 decision 3's table allows, among them `Surl.LineProtocol`
  (ADR-0050), whose public `SaslLineRedactionRule` covers `AUTH` and `AUTHENTICATE` initial responses and the lines
  answering `334 ` or `+ `. `LineBytesRedactor` and `ILineRedactionRule` are in `Surl.Protocol.Abstractions`. What is
  not shared that way (`PASS`, `ACCT`, IMAP `LOGIN`) is written in the proxy library. Moving a rule into a shared
  library changes projects outside this task's `touches`; file it as a follow-up if it is worth doing.
- The redactor learns that the tunnel was granted, and sees the continuation prompts, through `ObserveSent`.
- Tunnels inside an HTTP/2 `CONNECT` stream are not this task: their `DATA` frames are the subject of a separate task
  filed from BL-1713.
- HTTP carried in a tunnel keeps today's masking, and the HTTP/2 preface handling BL-1713 added stays.
- Stand-in: only the levelled log and trace dumps change. The event stream keeps raw bytes (ADR-0182 decision 4), so a
  test still sees what the client sent; no store or fault is involved.

## Acceptance criteria

- [x] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_FtpPassAndAcct_AreLoggedAsMasks` passes: after a granted
      `CONNECT` the log holds `PASS ****` and `ACCT ****`, also when the client's bytes arrive one at a time.
- [x] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_MailSaslCredentials_AreAbsentFromTheLog` passes for an
      `AUTH PLAIN <base64>` line and for continuation lines answering `334 ` and `+ `.
- [x] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_ImapLogin_PasswordIsAbsentFromTheLog` passes.
- [x] The tests `HttpProxyExchangeLogRedactionTests` and `HttpProxyConnectionBytesRedactorTests` held before this task
      pass with their expected output unchanged.
- [x] ADR-0182 gains an amendment saying what is masked inside a `CONNECT` tunnel and what is not, and the
      `CreateRedactor` doc comment and the proxy library's `CLAUDE.md` (references included) match.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the proxy library stays at 100% line and branch
      coverage.

## Notes

- Design (ADR-0182 amendment 16): `HttpProxyRequestOrTunnelBytesRedactor` reads HTTP request heads until a line that
  is not one starts where a request may start; from there `HttpProxyTunnelLinesRedactor` judges every line. The tunnel
  rules are near copies of the SOCKS ones (BL-1767 shares them); the proxy library now references `Surl.LineProtocol`.
- The client's bytes decide where the tunnel starts, not the `200` seen through `ObserveSent` as the Context suggests:
  switching at the `200` would log tunnelled bytes sent before it. `ObserveSent` still feeds the continuation rules.
- Pipeline: planned, tested and implemented in the session, without delegating those stages, to save tokens;
  `code-reviewer` reviewed. Conformance audit skipped: no byte Surl sends or accepts changes, only the log.
- Review found two leaks, fixed with failing tests first: a `CONNECT` head with a bare-line-feed line, and a `CONNECT`
  head declaring a body, both left the tunnel's `PASS` in clear.
- Verified: build `-warnaserror` clean, format clean, fast tests 29551 passed, 0 failed, 3 skipped; the proxy library
  measures 100% line and branch coverage, 0 failing members.
- Follow-ups filed: BL-1767 (share the tunnel rules), BL-1765 (MQTT and LDAP in the proxy tunnel), BL-1766 (help text).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. FTP PASS/ACCT and SMTP, IMAP and POP3 logins sent through an HTTP proxy CONNECT tunnel are logged as ****
