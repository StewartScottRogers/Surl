---
id: BL-462
title: Run the Curl port's webtransport client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-461, BL-406, BL-1047]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-462 — Run the Curl port's webtransport client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl webtransport://`, sending data on a
bidirectional stream, on a unidirectional stream and as a datagram, plus a refused unauthenticated session. Each
assertion comes from WTH3, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help webtransport` and its cases in `Curl.Conformance.Extended.UnitTests/WebTransport/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/WebTransport.md`: every row is covered by a named test or marked `n/a`
      with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-11: Wrote `PinnedCurlPortClientTalksToSurlOverWebTransportTests` (uncommitted, stashed by the shift). Run with the pinned client on Windows, with `--allow-anonymous --self-signed`: the bidirectional echo and the datagram echo pass.
- The unidirectional exchange (`?wt=uni`) hangs until the harness's 30 s timeout. surl's `-v` log shows the session accepted and no refusal. Neither side is proven at fault, and the fix may be in surl's `Surl.Http3` or `Surl.Extended.Protocol.WebTransport`, outside this task's `touches`, so BL-1047 finds the cause and this task depends on it.
- Port defect to report (ADR-0083): without `--allow-anonymous` surl answers the CONNECT `401` (`WebTransport:Surl-7-1`, proved by unit tests), yet the port's client exits 0 with empty stdout. The refusal test must therefore assert only that nothing is echoed, never the exit code.
- 2026-11-11 (finish): the stashed test could not be recovered (stash access denied in this run), so `PinnedCurlPortClientTalksToSurlOverWebTransportTests` was rewritten from the port's `?wt=uni|datagram|bidi` modes. Correction to the note above: without `--allow-anonymous` and with no account set, surl lets the CONNECT in (the policy has nothing to enforce), so the earlier "empty stdout" came from a different cause. The 401 refusal needs `--user alice:secret`; with it the refusal test passes, asserting only that nothing is echoed. The port's client still exits 0 on a refusal (port defect, ADR-0083). All four live tests pass on Windows with the pinned client, including the unidirectional one fixed by BL-1047. `WebTransport.md` now has the live-client table.
- (superseded) To finish: apply that change to the refusal test, add the four-exchange coverage table to `WebTransport.md` as BL-456 did for `SocketIo.md`, and mark the rows with no live test `n/a`.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1047: the live unidirectional exchange hangs, and its cause may be in Surl.Http3 or the WebTransport server, outside this task's touches
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. live Curl port webtransport client passes bidirectional, unidirectional, datagram and refused-login exchanges against surl; WebTransport.md lists them
