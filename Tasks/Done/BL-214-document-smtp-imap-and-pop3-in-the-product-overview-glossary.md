---
id: BL-214
title: Document SMTP, IMAP and POP3 in the product overview, glossary, README and roadmap
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-211, BL-212, BL-213]
touches: [Documentation/Product/Product-Overview.md, Documentation/Wiki, Documentation/Planning/Roadmap.md, README.md, Documentation/Product/Requirements.md, Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md, Surl.Protocol.Imap.UnitLibrary/CLAUDE.md, Surl.Protocol.Pop3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-214 — Document SMTP, IMAP and POP3 in the product overview, glossary, README and roadmap

## Goal

Every document names SMTP, IMAP and POP3 (with `smtps`, `imaps`, `pop3s`) and the mail store as
they are once Phase 3 is proven - the product overview, the glossary, the README, the roadmap,
the requirements and the three protocol libraries' `CLAUDE.md` files - so nothing an agent reads
says Phase 3 is still to come.

## Context

- The work: BL-184 to BL-212 and the ADRs they wrote (BL-184 to BL-188).
- `Documentation/Product/Product-Overview.md`: Authentication ("Built today" gains the SASL
  mechanisms, IMAP `LOGIN`, POP3 `USER`/`PASS` and `APOP`); "Layers" and "Project layout"
  (`Surl.MailStore`, `Surl.LineProtocol` exist). `Documentation/Planning/Roadmap.md`: a
  Milestone 3 entry in the shape of Milestone 0's. `Documentation/Wiki/Glossary.md`: mail store,
  mailbox, maildrop, SASL mechanism, dot-stuffing, and any other new concept, one name each.
  `README.md`: the protocol list and an example for each. `Documentation/Product/Requirements.md`:
  FR-043 to FR-047 name what satisfies them. The three protocol `CLAUDE.md` files: what each
  library holds now, its references, its ADRs.
- The `align-and-document` agent's rule: every statement true of the code as it is now.

## Acceptance criteria

- [x] Each file in `touches` states what is built for SMTP, IMAP, POP3 and the mail store, naming
      the ADRs, and none says Phase 3 is unbuilt.
- [x] `Documentation/Wiki/Glossary.md` defines each new term once, and the code and documents use
      those names.
- [x] `Documentation/Planning/Roadmap.md` has a Milestone 3 entry with status, delivery and
      decisions.

## Notes

- align-and-document rewrote the eight touched files in the BL-213 shape, grounded in the code and ADR-0049, -0050, -0053, -0055, -0056, -0057, -0059. Glossary gained a "Mail: SMTP, IMAP and POP3" section (mail store, mailbox, maildrop, SASL mechanism, dot-stuffing, TLS upgrade and others), using the code's names (e.g. `MailboxStore`). FR-029's stale `--auth` list was corrected too.
- Linux/macOS CI results for the mail conformance tests are not recorded, so the documents say "not yet recorded" rather than "passed".
- Found and filed: BL-265 (SMTP buffers DATA in memory and lacks ADR-0053's 451 4.3.0, against ADR-0050 decision 7), BL-266 (MailStore/LineProtocol CLAUDE.md say Phase 1), BL-267 (ADR-0049 still says gssapi waits on BL-218), BL-268 (one TLS-upgrade parameter, four names; after BL-264).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Product overview, glossary, README, roadmap (Milestone 3), requirements and the SMTP/IMAP/POP3 CLAUDE.md files describe the mail servers and mail store as built
