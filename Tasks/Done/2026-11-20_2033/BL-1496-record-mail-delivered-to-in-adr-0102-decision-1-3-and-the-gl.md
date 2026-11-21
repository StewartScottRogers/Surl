---
id: BL-1496
title: Record --mail-delivered-to in ADR-0102 decision 1.3 and the glossary
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1487]
touches: [Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1496 — Record --mail-delivered-to in ADR-0102 decision 1.3 and the glossary

## Goal

ADR-0102 decision 1.3 and the glossary's trace fields row say what `--mail-delivered-to` does.

## Context

ADR-0053 Amendment 1, "What follows". Edit `Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md` and `Documentation/Wiki/Glossary.md` only. This is prose; do not change behaviour. State intent as intent where the code (BL-1498 to BL-1495) has not landed when this runs.

## Acceptance criteria

- [x] ADR-0102 decision 1.3's "One copy per owner per message" bullet gains: "With `--mail-delivered-to` the copy carries one `Delivered-To` per envelope address that reached its owner (ADR-0053 amendment 1)."
- [x] The glossary's trace fields row states the option exists, off by default, that `Delivered-To: <address>` CRLF goes first ahead of `Return-Path`, one per address that reached the owner, and that the lines count against `--max-filesize`.
- [x] The glossary names `DeliveredFor` as the delivery event's member that always lists the addresses, whatever the option.
- [x] Every link and ADR number written resolves to an existing file.

## Notes

Prose only. Glossary wording marks the option as intent where the code has not landed. ADR-0053 exists; no new links added.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0102 decision 1.3 and the glossary record --mail-delivered-to and DeliveredFor
