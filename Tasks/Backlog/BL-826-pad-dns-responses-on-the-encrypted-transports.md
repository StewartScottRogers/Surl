---
id: BL-826
title: Pad DNS responses on the encrypted transports
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-483, BL-1035]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-826 — Pad DNS responses on the encrypted transports

## Goal

A DNS over HTTPS answer to a query whose OPT carried the EDNS(0) Padding option carries a Padding option of its own
that makes the whole message a multiple of 468 bytes, and the same padding is available to DoT and DoQ; UDP and TCP
answers are never padded.

## Context

- Decision: ADR-0092 decision 2 (EDNS(0) bullet): RFC 7830's Padding option is answered on the encrypted transports
  only, by RFC 8467 section 4.1's recommended policy (responses to padded queries padded to a multiple of 468 bytes).
- `DnsOption.PaddingCode` (12) exists; nothing pads yet. `DnsQueryAnswerer.AnswerOverHttp` and
  `DnsOverHttpsHandler` (BL-483) are the DoH path; BL-484's DoQ path needs the same writer.
- The padding is counted after any cut `DnsResponseWriter.WriteWithin` makes, and the OPT stays last (RFC 6891 section 7).

## Acceptance criteria

- [ ] A named test sends a DoH `POST` whose OPT carries an empty Padding option and checks the answer's length is a
      multiple of 468 and its OPT holds one Padding option of zero bytes making it so (RFC 7830 section 3).
- [ ] A named test shows a DoH query without Padding gets an unpadded answer, and the UDP and TCP servers never pad.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-483, which built DoH without padding to keep to its acceptance criteria.

- Code written, left uncommitted for the shift to stash; none of it is built or tested yet:
  - `DnsResponseWriter.WritePaddedWithin(response, limit)` and `PaddingBlockLength` (468): when the query's OPT
    carried a Padding option and the response has an OPT, it writes the response through `WriteWithin` with an
    empty Padding option appended last to the response's OPT, rounds that length up to a multiple of 468, and
    rewrites it with that many 0x00 octets of padding. Padding only lengthens the OPT, which every cut keeps, so
    the second write makes the same cut; when the padded length would pass the limit, the empty Padding option
    is kept (choice: RFC 7830 allows any padding length, and an empty one still tells the client padding is
    understood).
  - `DnsTcpResponseWriter.WritePadded` frames that for the encrypted stream transports; `DnsOverQuicConnectionServer`
    now uses it (RFC 9250 section 5.4), and `DnsOverHttpsHandler` uses `WritePaddedWithin`. UDP and TCP still
    call `Write`/`WriteWithin` and never pad. There is no DoT server yet; `WritePadded` is the writer it will use.
  - Tests: `DnsResponseWriterTests` (padded to 468 and 936, padded after a cut with the OPT last, empty Padding
    past the limit, unpadded without Padding or OPT), `DnsOverHttpsHandlerTests.AnswerAsync_PostWhoseOptCarriesAnEmptyPaddingOption_AnswersAMultipleOf468WithOnePaddingOptionOfZeros`
    and `..._PostWhoseOptCarriesNoPadding_AnswersUnpadded`, `ServeAsync_QueryWhoseOptCarriesPadding_IsAnsweredUnpadded`
    in the UDP and TCP server tests, and a DoQ padding test; `DnsTestQueries.PaddedOpt` writes the query's OPT.
  - Still to do once it builds: the library's CLAUDE.md "Not yet: EDNS(0) padding" line.
- 2026-11-11: `dotnet build` cannot run: `global.json` pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2
  and 11.0.100 are installed. Waits on BL-1035, as BL-774 and BL-778 do.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the code is written and left for the shift to stash
