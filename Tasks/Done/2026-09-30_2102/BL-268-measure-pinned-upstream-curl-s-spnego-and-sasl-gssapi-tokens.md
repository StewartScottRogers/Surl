---
id: BL-268
title: Measure pinned upstream curl's SPNEGO and SASL GSSAPI tokens against the test KDC
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-265, BL-267]
touches: [Documentation/Planning/Decisions]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-268 — Measure pinned upstream curl's SPNEGO and SASL GSSAPI tokens against the test KDC

## Goal

The Kerberos tokens the pinned Windows reference build sends for `--negotiate` and SASL `GSSAPI`
are measured with `Record-CurlExchange.ps1 -KerberosTestKdc`, and ADR-0057 decisions 8 and 9 are
confirmed or amended from them.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md` decision 6; the choices being checked: ADR-0057 decisions 8 and 9, and ADR-0064.
- Builds: `C:\Program Files\Git\mingw64\bin\curl.exe` (SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, 8.21.0); for HTTP Negotiate
  only, the unpatched static-curl 8.21.0 Windows build (ADR-0042).
- URLs name `web.surl.test` / `mail.surl.test` and pin them with `--resolve ...:127.0.0.1`;
  credentials `-u tester@SURL.TEST:<password>`. Decode each AP-REQ with `service.keytab`.

## Acceptance criteria

- [x] An ADR (or an amendment of ADR-0057) records, with each build's path, SHA-256 and version:
      the SPNEGO `mechTypes` in order, whether the optimistic token is an AP-REQ, its
      `mutual-required` flag, and whether a `mechListMIC` is sent.
- [x] It records, for SMTP, IMAP and POP3 with and without `--sasl-ir`, the SASL `GSSAPI`
      initial token's form, its `mutual-required` flag, and the unwrapped security-layer answer
      (layer byte, maximum size, identity) with and without `--sasl-authzid`, measured against a
      started `surl --keytab` through `-NoServer`.
- [x] It states for each whether ADR-0057 decision 8 or 9 holds; each difference is an amendment
      of ADR-0057 and a filed task that changes surl, which BL-269 then depends on.
- [x] `Documentation/Planning/Decisions/README.md` indexes it.

## Notes

- Filed by BL-242 (ADR-0065).
- Recorded as ADR-0057 Amendment 1 rather than a new ADR: the measurements confirm decisions 8
  and 9, and amending in place avoids racing other lanes for the next ADR number.
- Measured with BL-265's realm mapping in place. The token alone came from
  `Record-CurlExchange.ps1 -KerberosTestKdc` (HTTP mode, canned 401). The whole exchange needed the
  KDC's keytab before surl started, so the KDC was started directly (`Run-KerberosTestKdc.cs`
  with all four principals), four `surl --keytab` servers on it, and 14 cases through
  `Record-CurlExchange.ps1 -NoServer`; every case exited 0. No script change was needed.
- Tokens were decoded by a throwaway C# file-based app (not committed) using
  `Surl.Kerberos`'s keytab reader and AES profiles: ticket (key usage 2), authenticator (11),
  then the client wrap token with the authenticator subkey.
- Both decisions hold, so no amendment changes surl and no task is filed; BL-269 needs no new
  dependency. Learned for BL-269: SMTP and POP3 never send the GSSAPI token inline (curl's 512-
  and 255-byte line limits), IMAP always does (surl advertises SASL-IR), and the client wrap token
  has RRC 12.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0057 Amendment 1 records pinned curl's measured SPNEGO and SASL GSSAPI tokens; decisions 8 and 9 hold, all 14 logins to surl --keytab exit 0
