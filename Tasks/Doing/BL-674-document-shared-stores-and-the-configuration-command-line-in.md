---
id: BL-674
title: Document shared stores and the configuration command line in the product overview, README, wiki and glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-638, BL-639, BL-640, BL-667, BL-668]
touches: [Documentation/Product/Product-Overview.md, README.md, Documentation/Wiki, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-674 — Document shared stores and the configuration command line in the product overview, README, wiki and glossary

## Goal

The product overview, README, roadmap, wiki and glossary describe, as built, the stores synergetic protocols share in
one `surl` and the configuration command line and config file for real servers.

## Context

- What was built: BL-622's ADR (shared stores, message bus), BL-641's ADR (config file, item grammar, model),
  BL-642's ADR (what each option configures), and their build tasks BL-629 to BL-668. The extended-server and LDAP
  parts that are not yet built are written as intent, naming their tasks (BL-669, BL-670, BL-671 to BL-673).
- Files and sections:
  - `Documentation/Product/Product-Overview.md`: "Architecture" and "Layers" (the message bus beside the content
    store and the mail store), "Project layout" (the new library in `Protocols/Messaging/`), "Scope" (a short
    section on configuring a real server), "Last updated".
  - `README.md`: a short example of a `-K` config file with a mail domain, a mailbox, a share and a topic grant.
  - `Documentation/Planning/Roadmap.md`: the shared stores and configuration as a milestone or track, with the
    task ranges.
  - `Documentation/Wiki/Glossary.md`: one entry each for the message bus, canonical topic, share, share access,
    mail domain, mail alias, catch-all, shared folder, account group, config file - matching the names the code uses
    (root `CLAUDE.md`, "Say what it does").
  - `Documentation/Wiki/Home.md`: a link to the new section if Home lists the product's topics.
- Every statement must be true of the code at the time of writing (root `CLAUDE.md`); `surl --aihelp` already
  documents each option (its tasks did that), so link to `surl --aihelp <topic>` rather than duplicating tables.

## Acceptance criteria

- [ ] Each file above has the section named, stating what is listed there, with every option name and type name
      matching the code.
- [ ] The README example config file is accepted by `surl -K` as built (shown by running it, or by an existing
      `AiHelpExamples` entry it copies).
- [ ] No document still says `mqtt5` keeps its own retained messages or that a mail recipient's domain is always
      ignored.

## Notes

- Stand-in scope (added 2026-11-05): seeding, resetting, snapshotting and restoring the shared stores, the store
  change events and `--fault` (BL-822 to BL-821) are documented by BL-821 in
  `Documentation/Wiki/Surl-As-A-Stand-In.md`. This task documents the stores and the configuration surface; link
  to that page for test control (or name the tasks as intent if BL-821 has not run) rather than describing it.

## Log

- 2026-11-04: Created.
- 2026-11-05: Notes amended: test control of the stores is BL-821's to document; link, do not duplicate.
- 2026-11-12: Backlog -> Doing.
