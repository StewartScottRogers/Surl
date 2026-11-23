---
id: BL-1597
title: State in the --aihelp stomp topic that a STOMP subscription gets live messages only, never a retained one
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1597 — State in the --aihelp stomp topic that a STOMP subscription gets live messages only, never a retained one

## Goal

`surl --aihelp stomp` says that a `/topic/` subscription receives only messages sent while it is subscribed, that a
retained message (an MQTT concept) is never delivered to a STOMP subscriber, and that `/queue/` keeps messages for a
later subscriber.

## Context

Surly cookbook bug report 2026-11-22 (`Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1116-Surl-bug-report.md`,
item 4) asked whether it is intended that a STOMP subscriber to a topic with a retained message (`--topic "home/#;retain"`,
retained publish over MQTT) gets nothing while an MQTT subscriber gets it. It is decided and intended:
- ADR-0100 decision 3.3, "Retained is MQTT's alone to read": AMQP, NATS, STOMP and Redis subscribers never receive one
  on subscribing; they see only live fan-out or their queue. Its rejected alternatives list "Retained messages
  delivered to AMQP, STOMP and NATS subscribers" (none of their specifications has the concept).
- ADR-0118 (`/topic/<t>` bullet): "A topic keeps nothing for STOMP ... the retained slot MQTT's".
- ADR-0176 and ADR-0102 decision 3.1 change only what is retained, not who reads it.

The `stomp` topic, `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:415-421`, says the broker shares the bus with MQTT
and NATS (line 418) but not that topic delivery is live only, so an agent expects MQTT's behaviour. Add the facts as
one sentence after line 418's, e.g.: "A `/topic/` subscription receives only what is sent while it is subscribed: a
retained message is MQTT's alone and is never delivered to a STOMP subscriber, so a test that needs a message waiting
for a later STOMP subscriber sends it to a `/queue/`." No behaviour changes.

## Acceptance criteria

- [x] `AiHelpProse.cs`'s `stomp` paragraphs carry the sentence above (or wording with the same three facts).
- [x] `AiHelpTextTests.Answer_Stomp_SaysATopicSubscriptionGetsLiveMessagesAndNoRetainedOne` (in
      `Application/Surl.Cli.UnitTests`) passes: `--aihelp stomp` contains `never delivered to a STOMP subscriber`.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Application/Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. stomp aihelp topic states topic subscriptions are live only; retained is MQTT's alone
