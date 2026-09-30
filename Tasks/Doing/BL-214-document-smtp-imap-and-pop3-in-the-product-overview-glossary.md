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
completed:
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

- [ ] Each file in `touches` states what is built for SMTP, IMAP, POP3 and the mail store, naming
      the ADRs, and none says Phase 3 is unbuilt.
- [ ] `Documentation/Wiki/Glossary.md` defines each new term once, and the code and documents use
      those names.
- [ ] `Documentation/Planning/Roadmap.md` has a Milestone 3 entry with status, delivery and
      decisions.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
