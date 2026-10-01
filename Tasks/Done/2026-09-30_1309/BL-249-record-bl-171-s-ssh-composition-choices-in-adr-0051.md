---
id: BL-249
title: Record BL-171's SSH composition choices in ADR-0051
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-041
created: 2026-09-30
completed: 2026-09-30
---
# BL-249 — Record BL-171's SSH composition choices in ADR-0051

## Goal

ADR-0051 states the choices BL-171 made where it was silent or where the code departs from its
wording, each marked "Decided by Claude under Stewart's delegation".

## Context

- BL-171 could not edit `Documentation/Planning/Decisions` (outside its touches); its Notes list
  the choices. Record, in ADR-0051 (an amendment section, as BL-229 and BL-231 do):
  1. The host-key note is written `* Serving SSH host key ...`, with the `* ` every verbose note
     surl writes carries (ADR-0033; the throwaway-certificate note of ADR-0010 did the same in
     ADR-0020), and only when an `scp` or `sftp` listen URL is served.
  2. `--allow-weak-ssh-algorithms` stays refused as not available in this build until the server
     offers the weak algorithms (BL-221, then BL-250), since decision 11's warning would be false.
  3. The "needs a host key" refusal comes after the "needs a certificate" one (58) and before the
     `--user-file`, `--authorized-keys` and `--hostkey` files are read; the `--authorized-keys`
     files are read after the `--user-file`, and the `--hostkey` files after both.
  4. The `ssh` category and topic also hold `--directory` and `--pass`, which the SSH server reads
     (the content store; decrypting `--hostkey` keys).
- Code: `Surl.Console/SshHostKeyComposition.cs`, `Surl.Console/CommandLineRunner.cs`.

## Acceptance criteria

- [x] ADR-0051 has a section recording the four choices above, each with its reason and marked
      "Decided by Claude under Stewart's delegation", and every statement is true of the code.

## Notes

- Done directly rather than through align-and-document: one ADR section, every statement checked
  against `SshHostKeyComposition.cs`, `CommandLineRunner.cs`, `AuthenticationComposition.cs` and
  `CommandLineOptions.cs` as they are now. BL-250 is still in Backlog, so choice 2 is still true.
- Written as "Amendment 1" at the end of ADR-0051, after ADR-0049's amendment form, and linked
  from the header's "Amended by" line. Choice 3 also names the refusals before the listen-URL
  checks (unavailable option, data directory), which the code does first.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0051 Amendment 1 records BL-171's four SSH composition choices
