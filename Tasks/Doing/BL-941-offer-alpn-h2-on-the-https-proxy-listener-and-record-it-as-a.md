---
id: BL-941
title: Offer ALPN h2 on the HTTPS proxy listener and record it as ADR-0088 Amendment 5
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-940, BL-1035]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-941 — Offer ALPN h2 on the HTTPS proxy listener and record it as ADR-0088 Amendment 5

## Goal

An `https` listener named by `--proxy` (`ListenUrl.IsProxy`) offers ALPN `h2` then `http/1.1`, as an
`https` origin listener does, so curl given `--proxy-http2` agrees `h2` and is served by BL-940's
HTTP/2 path, and ADR-0088 records the change as Amendment 5.

## Context

- Last of the tasks BL-595 ("Serve the HTTPS proxy over HTTP/2 for --proxy-http2") was split into on
  2026-11-06 after it timed out at 120 minutes. Needs BL-940: offering `h2` before the proxy serves it
  would break every `--proxy-http2` curl.
- Today `TlsApplicationProtocols.ForListener` in `Foundation/Surl.Networking.UnitLibrary` pins exactly
  `http/1.1` for `{ IsProxy: true, Scheme: "https" }` (ADR-0088 Amendment 2); `ListenUrl.IsProxy`'s doc
  comment in `Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs` and
  `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md` say the same.
- Written already, in the stash `darkfactory BL-595 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it); none of these files has moved on the branch since the stash's base `a84caea4`. Take them with
  `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Networking.UnitLibrary Foundation/Surl.Networking.UnitTests/TlsApplicationProtocolsTests.cs Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs | git apply`:
  `ForListener` becomes `ForScheme(listenUrl.Scheme)` with its doc comment; the `TlsApplicationProtocolsTests`
  row `("https", true, ...)` expects `h2,http/1.1` and the test is renamed
  `ForListener_OffersTheSchemesListForAProxyListenerToo`; the `IsProxy` doc comment and the Networking
  `CLAUDE.md` paragraph name Amendment 5.
- Not written yet: `Foundation/Surl.Networking.UnitTests/ServerTlsHandshakeHttpsProxyTests.cs` still pins the
  old behaviour and fails after the change -
  `ForListener_HttpsProxy_OffersExactlyHttp11` (now `h2` then `http/1.1`),
  `ServeAsync_HttpsProxyHandshakeLikeCurl_AgreesHttp11AndProxiesOneRequest`'s `"h2,http/1.1"` row (the
  handshake now agrees `h2`; keep the `"http/1.1"` row, and add a test that a client offering
  `h2,http/1.1` agrees `h2` and gets one `CONNECT` or forwarded GET answered over HTTP/2 through
  `HttpProxyProtocolServer`, as curl's `--proxy-http2` does), and
  `UpgradeToTlsAsync_HttpsProxyAndClientOffersOnlyH2_ThrowsTlsHandshakeException` (the handshake now
  succeeds with `h2`). Rewrite them to the new behaviour; their comments cite BL-554's measurement and
  Amendment 2, which Amendment 5 supersedes for the offer.
- Also not written: the ADR amendment. The lost run had started it by adding
  `; Amendment 5 (BL-595) 2026-11-06` to the `Date` line; write it as BL-941.
- Upstream: the manpage (https://curl.se/docs/manpage.html, checked 2026-11-06 at curl 8.23.1; the
  option was added in 8.1.0) says `--proxy-http2` "Negotiate[s] HTTP/2 with an HTTPS proxy. The proxy
  might still only offer HTTP/1 and then curl sticks to using that version." What the pinned static
  8.21.0 build actually agreed is in BL-940's fixtures: `alpn.txt` in `https-proxy-h2-connect` and
  `https-proxy-h2-forward` (`h2`, with `--proxy-http2`) and in `https-proxy-h2-offered-http11-chosen`
  (without it). Cite those, not the manpage alone, in the amendment.
- Stand-in: no store and no new events; what a test observes is the agreed ALPN in the exchange's notes
  (`HTTPS proxy: curl's connection to the proxy is TLS (..., ALPN h2)`).

## Acceptance criteria

- [x] `TlsApplicationProtocolsTests.ForListener_OffersTheSchemesListForAProxyListenerToo` passes with the
      `("https", true)` row expecting `h2,http/1.1`.
- [x] `ServerTlsHandshakeHttpsProxyTests` passes with: the proxy listener offering `h2` then `http/1.1`;
      a client offering only `http/1.1` agreeing `http/1.1` and proxied as before; a client offering
      `h2,http/1.1` agreeing `h2` and answered over HTTP/2; and no test left asserting a handshake
      failure for an `h2`-only client.
- [x] `ListenUrl.IsProxy`'s doc comment and `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md` say an
      `https` proxy listener offers `h2` then `http/1.1` (ADR-0088 Amendment 5).
- [x] `Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md`
      has `## Amendment 5` stating that the HTTPS proxy offers `h2` then `http/1.1`, serves `CONNECT`
      and forwarded requests over HTTP/2 when `h2` is agreed (BL-940), that this supersedes decision 6's
      and Amendment 2's `http/1.1`-only offer, and the measurement it rests on; its `Date` line names
      Amendment 5 (BL-941).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Networking.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): Work is written and was left uncommitted for the shift to stash: `ForListener`
  is `ForScheme(listenUrl.Scheme)`; `TlsApplicationProtocolsTests` row and rename; `ListenUrl.IsProxy`
  doc comment; Networking `CLAUDE.md`; `ServerTlsHandshakeHttpsProxyTests` rewritten
  (`ForListener_HttpsProxy_OffersH2ThenHttp11`, the `http/1.1`-only test, and
  `ServeAsync_HttpsProxyHandshakeOfferingH2_AgreesH2AndForwardsOneGetOverHttp2` with rows `h2,http/1.1`
  and `h2`, sending curl's 132-byte first burst from `https-proxy-h2-forward` as a hex constant because
  the Networking tests cannot read HttpProxy's fixtures; the `h2`-only failure test is gone); ADR-0088
  Amendment 5 written and its `Date` line updated. The stash from the main checkout's BL-595 run
  could not be listed from the lane (`git stash list` denied), so it was rewritten by hand.
- Verified only by a temporary `global.json` pinned to 11.0.100 (reverted at once): the 17 tests in
  `ServerTlsHandshakeHttpsProxyTests` and `TlsApplicationProtocolsTests` pass. The pinned SDK 10.0.401
  is not installed on this machine, so `dotnet build` fails with "A compatible .NET SDK was not
  found" and the last criterion cannot be checked; the task waits on BL-1035 (Stewart's reinstall).
  The next run applies the stash, builds, runs the fast tests and checks coverage.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code and ADR Amendment 5 written and left for the shift's stash
- 2026-11-12: Backlog -> Doing.
