---
id: BL-1453
title: Amend ADR-0157 so a server limited to TLS 1.3 on macOS serves every client that offers TLS 1.3
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1453 — Amend ADR-0157 so a server limited to TLS 1.3 on macOS serves every client that offers TLS 1.3

## Goal

ADR-0157 carries an amendment, "Decided by Claude under Stewart's delegation". On a platform whose
`SslStream` has no TLS 1.3 server (macOS), a listener limited to TLS 1.3 (`--tlsv1.3`) serves every
client that offers TLS 1.3 through `Surl.Tls13Stream`, even one that also offers TLS 1.2. The amendment
also says what happens where the hand-built stream cannot serve (`--cacert` client certificates).

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` item 3 (Surl `763754785`, unchanged since). On
  `macos-latest`, `surl --tlsv1.3 --cert server.pem --key server.key https://127.0.0.1:PORT/` starts,
  and `curl --cacert server.pem https://127.0.0.1:PORT/` exits 35. Without `--tlsv1.3`, TLS 1.2 is
  served. The report asks for a start-time refusal, but ADR-0157 already gives macOS a TLS 1.3 server,
  so serving is the faithful answer (root `CLAUDE.md`: nothing is left out because the platform lacks a
  primitive).
- Why ADR-0157 misses this case: decision 1 sends a ClientHello to the hand-built stream only when it
  "offers TLS 1.3 and nothing lower" (`Foundation/Surl.Networking.UnitLibrary/ClientHelloPeek.cs:32-35`,
  `OffersOnlyTls13`; `ServerTlsHandshake.cs:97-117`). Upstream curl without `--tlsv1.3` offers 1.2 and
  1.3, so it goes to `SslStream` with `AcceptedProtocols` = TLS 1.3 only, which macOS cannot serve.
  ADR-0157 targeted upstream test 4001 (an ECH client offering 1.3 alone).
- Decision 3 of ADR-0157: the hand-built stream does no client certificates. So `--tlsv1.3 --cacert` on
  macOS still cannot complete; the amendment decides between a start-time refusal (exit code and text,
  ADR-0010's table) and a later task that adds client certificates to `Surl.Tls13Stream`.
- `--tlsv1.3` with TLS-SRP is already refused (ADR-0122).

## Acceptance criteria

- [x] An amendment section in
      `Documentation/Planning/Decisions/ADR-0157-how-surl-serves-tls-1-3-where-sslstream-cannot-so-macos-passes-upstream-test-4001.md`
      states the routing rule: when the settings' lowest accepted version is TLS 1.3 and the platform's
      `SslStream` has no TLS 1.3 server, any ClientHello whose `supported_versions` lists TLS 1.3 goes
      to `Surl.Tls13Stream`, and one that does not gets the alert the amendment names.
- [x] It decides `--tlsv1.3` with `--cacert` on such a platform. If it is a start-time refusal, it gives
      the exit code and exact text and files that task (`Surl.Composition`, `ServerTlsComposition`) with
      `task-board.ps1 new`; if it is client certificates in `Surl.Tls13Stream`, it files that task.
- [x] It names how the rule is proved without a Mac on the lanes: fast tests with a fake
      `ITls13Support` and `ClientHelloBytes` (BL-1454), and whether a macOS conformance run through
      `dispatch-workflow.ps1` is needed.

## Notes

Decided: serve (route any ClientHello offering 1.3), client certificates in Tls13Stream filed as BL-1466; proof by BL-1454 fake-support tests plus one macOS dispatch run. Touched only the ADR.


## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` item 3.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0157 amendment 1 decides routing of any TLS 1.3 offer, files BL-1466, names proof
