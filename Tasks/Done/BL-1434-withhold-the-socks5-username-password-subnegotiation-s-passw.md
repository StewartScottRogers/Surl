---
id: BL-1434
title: Withhold the SOCKS5 username/password subnegotiation's password from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1434 — Withhold the SOCKS5 username/password subnegotiation's password from the exchange log

## Goal

`surl -v -U alice:s3cret --allow-plaintext-auth socks5://127.0.0.1:0/`, answering
`curl --socks5 127.0.0.1:<port> --proxy-user alice:s3cret http://<origin>/`, never logs the RFC 1929
password bytes; they are written as BL-1417's ADR says.

## Context

- Bug report item: secrets in the `-v` log. RFC 1929's request is `VER 0x01, ULEN, UNAME, PLEN,
  PASSWD`, sent after the method selection picks `0x02`.
- `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/SocksProtocolServer.cs` is the server;
  `Socks5Exchange.cs:89` runs the subnegotiation. The redactor follows the greeting
  (`VER NMETHODS METHODS`), then rewrites only `PASSWD` of the subnegotiation, across chunk
  boundaries. SOCKS4 user IDs are not passwords. Tunnelled bytes after the reply pass through.
- `SocksUpstreamTestCaseServer.cs` keeps raw bytes (Surl.Conformance).

## Acceptance criteria

- [x] `SocksProtocolServer` implements BL-1418's contract. Fast tests in
      `Protocols/Proxy/Surl.Protocol.Socks.UnitTests` show the subnegotiation logged with `PASSWD` in the
      ADR's form, also split across reads at every byte.
- [x] Tunnelled bytes after the CONNECT reply that contain the password's bytes are logged unchanged,
      and a SOCKS4 request is logged as before (named tests).
- [x] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and coverage stays at 100% line and branch.

## Notes

Socks5PasswordRedactor is a byte-wise state machine (no buffering): greeting, wait for ObserveSent method 0x02, mask PASSWD as ****, then pass through. SOCKS4/tunnel unchanged.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. SOCKS5 RFC 1929 password is masked as **** in the exchange log
