---
id: BL-1990
title: Add dated amendment notes to ADR-0107 for its stale link, container-id, refused attach, delivery state and topic subscription statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1959]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1990 — Add dated amendment notes to ADR-0107 for its stale link, container-id, refused attach, delivery state and topic subscription statements

## Goal

A reader of ADR-0107 learns, from dated notes beside the statements BL-1959 left unmarked, where ADR-0146 or the
code has since changed them.

## Context

Found while working BL-1959 (2026-11-27). All statements are in
`Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md` ("ADR-0107").
Add notes in the form BL-1959's notes use ("*Amended <date> (BL-###), pointing at ...:*"); recorded text is never
rewritten. "ADR-0146" is
`Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md`.
The code is in `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary` (its `CLAUDE.md` describes it).
Each statement was checked on 2026-11-27.

- **Decision 0**, "Nothing of a connection outlives it; a link is never resumed (decision 5)": durable and shared
  subscriptions are bus queues that outlive a connection (ADR-0146 decisions 3.1 and 3.6). Links are still not
  resumed (decision 3.1).
- **Decision 3**, the `open` table's `container-id` row, "digits from `RandomNumberGenerator`": `Amqp1Container`
  draws them from the `IIdentifierRandom` it is given when the options give no `container-id`
  (`Amqp1ContainerOptions.NewContainerId`). `RandomNumberGenerator` is only the unseeded fallback,
  `Amqp1UnseededIdentifierRandom`.
- **Decision 4**, the `detach` bullet, "nothing of a link is kept for resumption": a kept subscription survives a
  `detach` with `closed` false, and a `closed` true one destroys it (ADR-0146 decisions 3.4 and 3.6).
- **Decision 4**, the refused-attach table: BL-1959's note under it covers only the `coordinator` row. ADR-0146
  adds `amqp:invalid-field` for a bad selector (decision 2.2, `selector not valid`), `amqp:resource-locked` for a
  subscription another link holds (decisions 3.3 and 3.5, `subscription in use`), and `amqp:not-found` for a
  `/queue/surl-subscription-...` address (decision 3.4) and for an attach with no source naming no subscription
  (decision 3.3).
- **Decision 5**, "The states it uses: `accepted` and `rejected` only": the container also writes `declared` in
  answer to a `declare` (ADR-0146 decision 1.3) and `transactional-state`, on a transactional posting's answer and
  on its own transfers acquired in a transaction (decisions 1.4 and 1.5).
- **Decision 5**, "**From a topic** the link is one live bus subscription to the filter": a durable, shared,
  `session-end` or `connection-close` subscription is a bus queue bound to the filter (ADR-0146 decisions 3.1, 3.5
  and 3.6). `Amqp1ContainerConnection.Subscriptions.cs` offers its messages with their queue delivery tags, so for
  such a link the bullet's drop past 1000, and the later "For a topic delivery every outcome only settles it",
  need checking against that code and saying in the note where they do not hold.
- **Alternatives considered**, the last bullet, "Serving transactions, durable subscriptions and filters now.
  Deferred to BL-707": BL-707 decided them in ADR-0146, and the bullet has no pointer to it.

## Acceptance criteria

- [x] Dated notes at decision 0 and at decision 4's `detach` bullet name ADR-0146, say which subscriptions outlive a link or connection, and say links are still never resumed.
- [x] A dated note at decision 3's `open` table says the `container-id` digits come from the container's `IIdentifierRandom`, with `RandomNumberGenerator` only the unseeded fallback.
- [x] A dated note at decision 4's refused-attach table lists ADR-0146's refusals named in Context with their error conditions.
- [x] A dated note at decision 5's states sentence names `declared` and `transactional-state` and when each is written.
- [x] A dated note at decision 5's "From a topic" bullet says a kept subscription is a bus queue, and how its outcomes and its waiting bound differ from a live subscription's, as the code has them.
- [x] A dated note at the last "Alternatives considered" bullet points at ADR-0146, and the diff to ADR-0107 only adds lines.

## Notes

- Delivered by `align-and-document`. `git diff --numstat` for ADR-0107: 105 added, 0 removed. A header line
  listing where the BL-1990 notes are was added too, as BL-1959 did.
- Where the code is narrower than Context, the notes follow the code: only an `expiry-policy` of `never`
  lets a subscription outlive its connection (`session-end` and `connection-close` queues are exclusive to
  it), and a `shared` source with `durable` `none` or `link-detach` is a live subscription, not a bus queue.
- The `container-id` note also says `Surl.Composition` passes no `IIdentifierRandom`, so a composed container
  uses the unseeded fallback even in a seeded run (BL-2022).
- Verification: `dotnet build` 0 warnings, 0 errors; fast tests passed with 0 failures. No `.cs` or project
  file changed.
- Filed: BL-2022 to BL-2031 (code defects and stale statements read from the code, none proven by a run) and
  BL-2032 (`--max-filesize` missing from the `amqp1` help page).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0107 has dated BL-1990 notes where ADR-0146 or the code differs on stale links, container-id, refused attaches, delivery states, topic subscriptions and the deferral
