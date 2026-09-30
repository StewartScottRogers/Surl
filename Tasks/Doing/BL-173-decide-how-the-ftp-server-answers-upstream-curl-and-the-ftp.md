---
id: BL-173
title: Decide how the FTP server answers upstream curl and the FTP data-connection seam
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-173 — Decide how the FTP server answers upstream curl and the FTP data-connection seam

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, how
`Surl.Protocol.Ftp` answers every command curl sends, how logins map onto ADR-0032, and the
data-connection seam through which a protocol server gets passive and active data connections
without constructing a socket, so BL-174 to BL-183 can be built without a question.

## Context

- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Ftp` (with `-FtpReply`, `-FtpData`,
  `-Tls`) already serves an FTP session with passive and active data connections, `AUTH TLS`
  and `PROT P`. Record the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`,
  SHA-256 `0E7737...8778`) for at least: a download; `-I`; `-r 0-9`; `-C -` download and upload;
  a directory URL (`ftp://h/dir/`); `-l`; `-T` upload; `--ftp-create-dirs -T` into a missing
  directory; `-Q "DELE x"` and a `-Q` rename; `--ftp-method singlecwd` and `nocwd`;
  `--disable-epsv`; `-P -`/`--ftp-port -` and `--disable-eprt`; `-B`; `-u user:pass`; no `-u`
  (curl's default anonymous login); a `530` answer to `PASS` (curl's exit code); `--ssl-reqd`
  on `ftp://` (AUTH TLS, PBSZ, PROT); `--ftp-ssl-control`; `--ftp-ssl-ccc`; `ftps://`
  implicit. Extend the script where a case needs it (this task touches it), rather than writing
  a throwaway server.
- **Answers.** Greeting (no version, ADR-0006 section 3); each command curl sends and its reply
  code and text; the listing format for `LIST` (the Unix `ls -l` style curl parses for
  wildcard matching, or as measured), `NLST`, `MLSD`/`MLST` (RFC 3659); `SIZE` and `MDTM`
  formats; `REST`; `TYPE A` (converted or not); `-Q` commands (`DELE`, `RNFR`/`RNTO`, `MKD`,
  `RMD`, `SITE` forms) and what the content store cannot do; `CCC`; `ABOR` and a data connection
  curl closes early (`-r`).
- **Logins** (ADR-0032, "Protocol servers not yet built"): `USER`/`PASS` through
  `CheckPasswordLoginAsync` with the `TlsSession` at the moment of `PASS`; no account refuses
  (the reply code, e.g. `530`); `PASS` before `AUTH TLS` refused unchecked without
  `--allow-plaintext-auth`; curl's default anonymous login with and without `--allow-anonymous`;
  commands before login; the `CheckedLogin` method word (ADR-0038).
- **The data-connection seam** (ADR-0004 rule 2: only `Surl.Networking` constructs sockets).
  Decide the Abstractions types BL-174 adds: e.g. a passive listener bound to the control
  connection's local address that accepts one connection within a timeout and only from the
  control connection's peer address (RFC 2577 section 4 port-stealing defence), and an active
  connect to the address `PORT`/`EPRT` names only when it is the peer's address (RFC 2577
  section 3 bounce defence); the port range (ephemeral, or an option); the `PASV` address when
  bound to `0.0.0.0` or an IPv6 address (`EPSV` then); TLS on a data connection
  (`IConnection.UpgradeToTlsAsync`, and whether the pinned Schannel build needs TLS session
  resumption on the data connection - measure with `-Tls` and `PROT P`). **Shape it so no
  existing type with implementers outside BL-174's touches gains a member**: add it to
  `ExchangeContext` as a property with a default, or as a new interface, not as a member of
  `IListenerFactory` (implemented by `Surl.Console.UnitTests/FakeListenerFactory.cs` and by
  `Surl.Networking`). Decide how `Surl.Core` (BL-176) supplies it per exchange, how data bytes
  count against the exchange's idle clock (ADR-0006: they do), whether a data connection counts
  against `--max-connections`, and how data bytes appear in the verbose and trace logs
  (ADR-0033).
- **Limits** (ADR-0006 section 5's FTP column: `421` for connection limits, head timeout, idle
  and duration; `500` for a line past `--max-line`; `552` for an upload past `--max-filesize`,
  the partial upload deleted; `550` for "not permitted", section 2).
- **Help.** The category name and description (ADR-0034 decision 1: `ftp`, "FTP and FTPS
  protocol"), for BL-182.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, tool and
      arguments, date, transcript excerpt) and decides every point in Context.
- [ ] It gives the data-connection contract as C# (as ADR-0032 section 6 did) and states that
      no existing interface with outside implementers changes.
- [ ] It lists the curl 8.21.0 command lines BL-183 must prove, with the expected exit code for
      each.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
