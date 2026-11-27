---
id: BL-1909
title: Correct the per-process TLS settings comments in Surl.Console and the QuicCertificate summary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1880, BL-1901]
touches: [Application/Surl.Console, Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1909 — Correct the per-process TLS settings comments in Surl.Console and the QuicCertificate summary

## Goal

The `Surl.Console` comments on TLS settings, and the `ServerTlsComposition.QuicCertificate` summary, are true of the
code.

## Context

Found during BL-1901 (2026-11-27), read from source. Comments and prose only. Line numbers are as of that date; BL-1880
changes `ServerTlsComposition.cs` and `Application/Surl.Console/CLAUDE.md` first, so describe the code as it is then.

- "The process's TLS settings" / "the process's `ServerTlsSettings`": `Application/Surl.Console/CLAUDE.md` (about line
  152), `CommandLineRunner.cs` (about lines 20 and 54) and `Program.cs` (about lines 40, 58 and 67).
  `ServerTlsComposition.Compose` runs once per server start (`SurlServerComposition.Start.cs`) and `Surl.Api` runs many
  servers in one process; `ServerTlsSettings`'s summary in the Networking library has the corrected wording.
- `Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs`, `QuicCertificate` summary: names only `--http3`.
  `ComposeQuicCertificate` also makes the certificate for each listen URL `NeedsQuicCertificate` accepts.
- BL-1881 corrects the other doc comments in `ServerTlsComposition.cs`; leave those to it.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] No comment or `CLAUDE.md` line under `Application/Surl.Console` says there is one set of TLS settings per process.
- [x] The `QuicCertificate` summary agrees with `ComposeQuicCertificate` and `NeedsQuicCertificate`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- 2026-11-27: "the process's TLS settings" is now "the server start's TLS settings" in `CommandLineRunner.cs` and
  `Program.cs`; `Application/Surl.Console/CLAUDE.md` says `ServerTlsComposition` builds at most one
  `ServerTlsSettings` for each start, matching `ServerTlsSettings`'s summary.
- 2026-11-27: `ServerTlsComposition.QuicCertificate` needed no change: BL-1880 already rewrote it, and it agrees with
  `ComposeQuicCertificate` (`--http3` or any URL `NeedsQuicCertificate` accepts: `webtransport`, `coaps`, DNS over
  QUIC).
- 2026-11-27: Edited in the session rather than through `align-and-document`: seven comment lines did not justify a
  second agent.
- 2026-11-27: Gates: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests green. No `Integration`
  run: comments only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Console comments and CLAUDE.md describe TLS settings per server start; QuicCertificate summary verified against the code
