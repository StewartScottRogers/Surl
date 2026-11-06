---
id: BL-886
title: End an XMPP stream with policy-violation once 16 MiB routed to it is unsent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-539]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-886 — End an XMPP stream with policy-violation once 16 MiB routed to it is unsent

## Goal

A slow reader never stalls the streams that send to it: a stanza routed to another stream counts as delivered at
once, and a stream with more than 16 MiB of routed stanzas not yet taken by its transport ends with the stream error
`policy-violation`, the sender not told.

## Context

- Decision: ADR-0114 decision 7, the "unsent output to a stream (a slow reader)" row.
- BL-539 routes stanzas through `XmppBoundSession.TryDeliverAsync`, which writes on the recipient's connection
  (`XmppServerConnection.WriteAsync`, a chain that keeps writes in order) and awaits the write, so today the sender
  waits for a slow recipient's transport. This task makes delivery queue the stanza and return, counts the bytes
  queued per stream, and past 16 MiB ends that stream (`policy-violation`, `</stream:stream>`, close) from the
  recipient's own task.
- `Documentation/Conformance/Extended/Xmpp.md` row `Xmpp:Surl-Limits-4` names this task.

## Acceptance criteria

- [ ] Named tests in `Surl.Extended.Protocol.Xmpp.UnitTests` show:
      - a message to a stream whose writes never complete leaves the sender free to send and be answered;
      - a stream with more than 16 MiB of routed stanzas unsent gets
        `<stream:error><policy-violation xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>`
        and is closed, and the sender gets no error.
- [ ] `Xmpp:Surl-Limits-4` in `Documentation/Conformance/Extended/Xmpp.md` names this task's test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-539, whose acceptance criteria did not include the unsent-output limit even though the conformance trace
listed it under BL-539.

## Log

- 2026-11-05: Created.
