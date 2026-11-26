---
id: BL-1748
title: Withhold an IMAP LOGIN password when the line has no tag or uses tabs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1748 — Withhold an IMAP LOGIN password when the line has no tag or uses tabs

## Goal

`LOGIN alice <password>` (no tag, no leading space) and a `LOGIN` line whose words are separated by tabs are logged by
the IMAP server with the password as `****`.

## Context

- Found while working BL-1747. `ImapLineRedactionRule.TryFindLoginArguments` splits words on spaces only and treats the
  first word as the tag unless the line starts with a space. `LOGIN alice <password>` therefore reads as tag `LOGIN`,
  command `alice`, and `A1<tab>LOGIN<tab>alice<tab><password>` reads as one word. The server refuses both lines, but the
  client has already sent the password.
- A line whose tag really is `LOGIN` (`LOGIN LOGIN alice <password>`) must keep masking the password.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Imap.UnitTests` shows the password masked on a `LOGIN` line with no tag and no
      leading space.
- [x] A named test shows the password masked on a `LOGIN` line whose words are separated by tabs.
- [x] A named test shows the password masked on `LOGIN LOGIN alice <password>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Tests in `ImapExchangeLogRedactionTests`: `Login_WithNoTagAndNoLeadingSpace_IsMasked`,
  `Login_WithTabsBetweenWords_IsMasked`, `Login_WithATagOfLogin_MasksBothArguments`.
- Decisions (ADR-0183 amendment 3), all choosing to withhold more when a refused line is ambiguous: `LOGIN LOGIN a b`
  masks both `a` and `b`, because `LOGIN login <password>` is also a line with no tag; on a line with a tab nothing after
  `****` is written and a literal it announces is masked.
- Also fixed here: the line after `A1 LOGIN alice` (no password) had its first word masked.
- `Documentation/.../ADR-0183` added to `touches` for the amendment; the only other task in `Doing` (BL-1733) does not
  name it.
- Follow-ups filed: BL-1749 (SASL lines with tabs), BL-1750 (the SOCKS tunnel's IMAP `LOGIN` rule).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. IMAP LOGIN passwords on lines with no tag or with tabs are logged as ****
