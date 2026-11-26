---
id: BL-1760
title: Withhold API-key and session-token fields from the SOCKS tunnel's exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1717]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpWithheldHeaderNames.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1760 — Withhold API-key and session-token fields from the SOCKS tunnel's exchange log

## Goal

The SOCKS tunnel's exchange log withholds the value of every `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` field,
on HTTP/1.x and on cleartext HTTP/2.

## Context

- ADR-0182 amendment 13 (BL-1717) accepted this limit: an origin reached in cleartext through a SOCKS tunnel has these
  fields logged in clear in the tunnel's log, though the origin's own log withholds them for gRPC, DNS over HTTPS and
  the other HTTP-carried extended servers. This task closes the limit for SOCKS; the HTTP proxy is a separate task.
- In `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary`, `SocksTunnelPasswordAndAuthorizationLineRule` masks only the
  `Authorization` and `Proxy-Authorization` header lines, and `SocksTextTunnelBytesRedactor` makes
  `Http2RequestBytesRedactor` with no further names.
- SOCKS does not say what the tunnel carries, so the fields are withheld in every text tunnel. On a header line the
  whole value is `****` (no scheme word is kept), and so is a folded continuation of it. A line of another protocol that
  looks like such a header is masked too: that loses log detail only. If the rule's name no longer says what it does,
  rename it.
- The amendment replaces the SOCKS tunnel's part of amendment 13, point 3 and of its accepted limit. Update the doc
  comment of `HttpWithheldHeaderNames` and the abstractions library's `CLAUDE.md` where they say who withholds the list.
- If `AiHelpProse` or `ManualText` (`Application/Surl.Cli.UnitLibrary`) say the tunnel does not withhold these fields
  when this task runs, file a follow-up; help text is not part of this task (BL-1723 covers the withheld lists).
- Stand-in: only the levelled log and trace dumps change. The event stream keeps raw bytes (ADR-0182 decision 4), so a
  test still sees what the client sent; no store or fault is involved.

## Acceptance criteria

- [x] `SocksExchangeLogRedactionTests.TunnelledHttp1Request_ApiKeyFields_AreLoggedAsMasks` passes for each name in
      `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, a folded continuation line included.
- [x] `SocksTunnelHttp2RedactionTests.PriorKnowledgeTunnel_ApiKeyFields_WithholdTheHeaderBlock` passes for each of those
      names: each `HEADERS` payload is `****`.
- [x] The SOCKS library reads the field names from `HttpWithheldHeaderNames` and names none of the five itself.
- [x] A new ADR-0182 amendment (the next free number) states the above, and the redactors' doc comments, the
      `HttpWithheldHeaderNames` doc comment and both libraries' `CLAUDE.md` files agree with it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the SOCKS library stays at 100% line and
      branch coverage.

## Notes

- Plan: the task fixes the design (it mirrors BL-1759 for the HTTP proxy), so the feature stages ran in this session
  without `protocol-architect` or `protocol-implementer`: tests first (both new tests failed), implementation, verify,
  then a `code-reviewer` pass. No conformance run: no byte on the wire changes, only the log.
- ADR-0182 amendment 19. `SocksTunnelPasswordAndAuthorizationLineRule` is renamed to
  `SocksTunnelPasswordAndCredentialHeaderLineRule`; it now reads the credential names from
  `HttpWithheldHeaderNames.Credentials` too. `Http2RequestBytesRedactor` is given `ApiKeysAndSessionTokens`.
- Review: fixed the stale `SocksProtocolServer.CreateRedactor` doc comment, two field names, and the amendment's
  limits. The malformed shapes that can still be logged are listed there; BL-1772 filed for two of them.
- Help text: `AiHelpProse` and `ManualText` still say the SOCKS tunnel does not withhold these fields. The follow-up
  is already filed: BL-1764 depends on this task.
- Stand-in: only the levelled log and trace dumps change; the event stream keeps raw bytes.
- Verify (2026-11-25): build 0 warnings, format clean, fast tests 29566 passed, 0 failed, 3 skipped;
  `Surl.Protocol.Socks.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The SOCKS tunnel's exchange log withholds API-key and session-token fields on HTTP/1.x and cleartext HTTP/2
