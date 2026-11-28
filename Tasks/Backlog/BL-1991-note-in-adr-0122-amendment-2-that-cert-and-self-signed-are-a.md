---
id: BL-1991
title: Note in ADR-0122 Amendment 2 that --cert and --self-signed are allowed beside TLS-SRP since BL-911
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1991 — Note in ADR-0122 Amendment 2 that --cert and --self-signed are allowed beside TLS-SRP since BL-911

## Goal

A reader of ADR-0122 Amendment 2 learns, from a dated note beside its "Refused beside TLS-SRP" bullet, that
`--cert` and `--self-signed` are no longer refused.

## Context

Found while working BL-1959 (2026-11-27). In
`Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md`, "Amendment 2 (2026-11-11)",
the bullet "**Refused beside TLS-SRP**" says `--cert` and `--self-signed` are refused because decision 2's signed
suites are not built, and that "BL-911 lifts this refusal when it adds them".

Checked on 2026-11-27:

- BL-911 is Done: `Surl.TlsSrp` serves the signed suites. BL-1099 (Done) removed the two refusals from the
  command line.
- `Application/Surl.Cli.UnitLibrary/TlsSrpCombination.cs` refuses only `--http3`, `--cacert`, `--tls-max` below
  1.2 and `--tlsv1.3`, and allows `--cert` and `--self-signed`.
- `ServerTlsComposition` in `Application/Surl.Composition.UnitLibrary` refuses under TLS-SRP a `--cert` whose key
  is not RSA, exit 58, as ADR-0122 Amendment 5 ("The certificate's key") records.

Recorded text is never rewritten; the note is added in italics, dated, naming this task.

## Acceptance criteria

- [ ] A dated note beside Amendment 2's "Refused beside TLS-SRP" bullet says `--cert` and `--self-signed` are allowed beside TLS-SRP and add the RSA-signed SRP suites, naming BL-911 and BL-1099.
- [ ] The note says the options still refused are `--http3`, `--cacert`, `--tls-max` below 1.2 and `--tlsv1.3`, and that a `--cert` with a key other than RSA exits 58 (Amendment 5).
- [ ] The diff to ADR-0122 only adds lines.

## Notes

## Log

- 2026-11-27: Created.
