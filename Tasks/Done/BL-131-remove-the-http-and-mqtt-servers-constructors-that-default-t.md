---
id: BL-131
title: Remove the HTTP and MQTT servers' constructors that default to AnonymousAuthenticationPolicy
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-117, BL-125]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-131 — Remove the HTTP and MQTT servers' constructors that default to AnonymousAuthenticationPolicy

## Goal

`HttpProtocolServer` and `MqttProtocolServer` each have only the constructor that takes an
`IAuthenticationPolicy`, so no server can be composed without a policy.

## Context

ADR-0032 section 6: "BL-117 composes the servers with `Surl.Authentication`'s policy only and
removes the overloads". BL-117 did the composing (`Surl.Console` now calls only the
two-argument constructors) but could not remove the overloads: they live in
`Surl.Protocol.Http.UnitLibrary` and `Surl.Protocol.Mqtt.UnitLibrary`, outside its `touches`,
and BL-125 (in `Doing` at the time) touches both.

- `Surl.Protocol.Http.UnitLibrary/HttpProtocolServer.cs`: `HttpProtocolServer(ContentStore)`.
- `Surl.Protocol.Mqtt.UnitLibrary/MqttProtocolServer.cs`: `MqttProtocolServer(MqttRetainedMessages)`.
- Their tests (`HttpProtocolServerTests`, `MqttProtocolServerTests`, `MqttRetainedMessageFileTests`,
  `PacketLimitTests`, `PublishPayloadLimitTests`, ...) pass `new AnonymousAuthenticationPolicy()`
  explicitly instead. `AnonymousAuthenticationPolicy` itself stays, as the test double.

## Acceptance criteria

- [x] `grep -rn "public HttpProtocolServer(ContentStore contentStore)$\|public MqttProtocolServer(MqttRetainedMessages retainedMessages)$"`
      over the two libraries finds nothing.
- [x] Each server's CLAUDE.md and XML docs no longer mention the one-argument constructor.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and both libraries keep 100%
      line and branch coverage.

## Notes

- Removed both one-argument constructors and their XML docs; the MQTT CLAUDE.md now says
  the only constructor takes the policy. Every test that used them (including
  `HeadTimeoutTests` and `HttpServerHarness`, not listed in Context) passes
  `new AnonymousAuthenticationPolicy()` explicitly; `MqttProtocolServerTests`' helper now
  uses `policy ?? new AnonymousAuthenticationPolicy()` instead of choosing a constructor.
- `dotnet build -warnaserror` clean; fast tests green (HTTP 347, MQTT 242);
  `Measure-CodeQuality.ps1 -Library` reports 0 failing members for both libraries.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. HttpProtocolServer and MqttProtocolServer can only be built with an IAuthenticationPolicy
