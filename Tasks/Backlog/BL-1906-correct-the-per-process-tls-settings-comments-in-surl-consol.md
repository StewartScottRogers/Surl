---
id: BL-1906
title: Correct the per-process TLS settings comments in Surl.Console and the QuicCertificate summary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1880, BL-1901]
touches: [Application/Surl.Console, Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1906 — Correct the per-process TLS settings comments in Surl.Console and the QuicCertificate summary

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

- [ ] No comment or `CLAUDE.md` line under `Application/Surl.Console` says there is one set of TLS settings per process.
- [ ] The `QuicCertificate` summary agrees with `ComposeQuicCertificate` and `NeedsQuicCertificate`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
