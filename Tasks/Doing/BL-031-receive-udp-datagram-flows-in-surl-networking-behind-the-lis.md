---
id: BL-031
title: Receive UDP datagram flows in Surl.Networking behind the listener seam
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-031 — Receive UDP datagram flows in Surl.Networking behind the listener seam

## Goal

`Surl.Networking.UnitLibrary` binds a UDP listen address and hands each new remote peer's
first datagram to its caller as the datagram-flow type from the listener-seam ADR
(BL-000). Each flow can reply from a fresh port, which TFTP needs.

## Context

- The listener-seam ADR recorded by BL-000 defines the datagram-flow shape (types added
  by BL-005), how bind failures are reported, and how socket code meets the coverage
  gate. Its `README.md` index entry in `Documentation/Planning/Decisions/` names it.
- TFTP, the only datagram protocol upstream curl speaks, requires the server to answer
  each transfer from a new port (RFC 1350, section 4). If the ADR's datagram-flow shape
  cannot express a reply from a new port, stop. Have `task-planner` file a `docs` task
  to extend the ADR, make this task depend on it, and move this task to `Blocked`.
- Builds on BL-011's TCP work: reuse its host-to-address selection and its bind-error
  mapping to `SurlExitCode`.
- Only this project constructs `UdpClient` or a UDP `Socket`. Real-socket tests are
  `[TestCategory("Integration")]` on `127.0.0.1` with port 0.

## Acceptance criteria

- [ ] A UDP listener type in `Surl.Networking.UnitLibrary` implements the ADR's
      datagram listener: bind, report the bound endpoint, yield one flow per new remote
      endpoint carrying its first datagram, and stop on cancellation.
- [ ] A flow receives later datagrams from its peer, sends replies, and can switch to
      replying from a newly bound ephemeral port, as the ADR defines.
- [ ] Logic that needs no socket (demultiplexing datagrams by remote endpoint, flow
      lifetime) is covered by fast tests.
- [ ] `[TestCategory("Integration")]` tests on `127.0.0.1:0`: a `UdpClient` in the test
      sends a datagram and receives the reply, including a reply from a new port; two
      peers get two flows.
- [ ] Members not covered by fast tests carry `[ExcludeFromCodeCoverage]` with a
      justifying comment, as the ADR allows. `Measure-CodeQuality.ps1` reports no
      failing member in `Surl.Networking.UnitLibrary`.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, and the fast
      tests are green.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
