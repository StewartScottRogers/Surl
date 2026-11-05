---
id: BL-543
title: Decide how Surl serves the Curl port's extended protocols and how they are tested
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, CLAUDE.md, .claude/rules/testing.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-543 — Decide how Surl serves the Curl port's extended protocols and how they are tested

## Goal

An ADR records Stewart's approved decisions (2026-11-04) for a new family of servers: one Surl server for
each "extended protocol" the Curl port adds beyond upstream curl. It records the narrow exception to ADR-0003
that these servers need, and the project, registration, security and testing rules that every
extended-server task builds on.

## Context

- The request, approved by Stewart on 2026-11-04. His decisions are not reopened here; this task records
  them and decides only the details they leave open.
  - The Curl port (https://github.com/StewartScottRogers/Curl, local clone `Z:\repos\Curl`; read it with
    `git -C Z:\repos\Curl show origin/master:<path>`) adds URL schemes that upstream curl refuses. They are
    listed in its `Documentation/Protocols.md`, section "Extended protocols". Each has a requirement matrix in
    `Documentation/Conformance/Extended/<Name>.md` and client code in `Curl.Extended.Protocol.<Name>.UnitLibrary`.
  - **Oracle.** Each extended server is judged by its protocol's specification. Expected bytes come from the
    specification and its examples, never from the Curl port. The port's extended client MAY be used as a
    live test client in integration tests, but its output is never proof. This is a narrow exception to
    ADR-0003. The original protocols are unchanged: pinned upstream curl stays their only oracle.
  - **Copying code.** Copying code from the port's client, and from its reference servers under
    `Curl.Conformance.Extended.UnitLibrary/<Name>/`, is allowed: code is not a verdict.
  - **Layout.** Each server is `Surl.Extended.Protocol.<Name>.UnitLibrary` with its `.UnitTests` twin, in
    the ADR-0079 category folder for its job:
    - `Protocols/FileTransfer/`: S3 (`s3`), AzureStorage (`az`, `abfs`, `abfss`), GoogleCloudStorage (`gs`),
      OciRegistry (`oci`).
    - `Protocols/Http/`: Grpc (`grpc`, `grpcs`), SignalR (`signalr`, `signalrs`), SocketIo (`socketio`,
      `socketios`), WebTransport (`webtransport`), McpServer (`mcp`, `mcps`, `mcp+stdio`; the server side of
      the port's McpClient, named for what it is), Coap (`coap`, `coaps`).
    - `Protocols/Lookup/`: DnsQuery (`dns`).
    - `Protocols/Messaging/`: Mqtt5 (`mqtt5`, `mqtt5s`, `mqtt5+ws`), Amqp0 (`amqp`, `amqps`), Amqp1 (`amqp1`,
      `amqp1s`), Kafka (`kafka`, `kafkas`), Nats (`nats`, `natss`), Stomp (`stomp`, `stomps`), Xmpp (`xmpp`,
      `xmpps`).
    - `Protocols/DataStore/`: Redis (`redis`, `rediss`). This is a new category, which BL-400 adds by
      amending ADR-0079.
  - **Plugin is skipped.** The port's Plugin protocol hands a URL to a local helper program. It has no wire
    protocol for a server to answer.
  - **Isolation.** An extended server references only `Surl.Protocol.Abstractions` and ADR-0002's horizontal
    libraries, never an original protocol server. No original server references an extended one.
  - **One switch.** Every extended server is registered so that one switch turns them all off, mirroring the
    port's `CURL_EXTENSIONS=0` (the port's ADR-0999 decision 5). Planning name: `SURL_EXTENSIONS=0`.
  - **Unchanged original schemes.** An extended server never changes how an original scheme is served.
  - **Same rules.** It follows Surl's secure-by-default rule: authentication is loosened only by an option
    given explicitly. It meets the same four quality gates, uses the BCL only (no packages), and hand-builds
    whatever the BCL lacks.
- The port's own rules for its extensions are in the port's `Documentation/Planning/Decisions/`:
  `ADR-0999-extensions-live-apart-from-curls-own-protocols.md` and
  `ADR-1188-extended-protocols-conform-to-their-own-specifications.md`. They are background only, not evidence.
- Surl rules this amends or sits beside:
  - ADR-0002 (the project map and isolation), ADR-0003 (upstream curl is the only oracle), ADR-0004 (the
    listener seam), ADR-0006 (hardening) and ADR-0079 (category folders).
  - The product overview's "Non-goals" ("Protocols upstream curl does not request").
  - Root `CLAUDE.md`, "Upstream curl and the Curl port".
  - `.claude/rules/testing.md` ("replaying request bytes measured from pinned upstream curl - never bytes
    from the Curl port").

Decide and record:

1. **The oracle exception, worded narrowly.** It covers only `Surl.Extended.Protocol.*` servers. Their unit
   tests replay bytes written from the specification and its examples. The port's client is a live client in
   `Integration` tests only, and its output is never cited as proof in an ADR, a test name or an expected value.
2. **How an integration test locates and pins the port's client binary**: by path and SHA-256 as
   `UpstreamCurlBuilds.json` does, or built from a pinned commit of the port. Name the pin file (planning name
   `CurlPortBuilds.json` at the repository root). Say who produces the binary and how, whether CI runs these
   tests, and how the test refuses a binary that does not match. The Curl port is never listed in
   `UpstreamCurlBuilds.json`.
3. **Where the extended integration tests live.** Planning assumption: in `Conformance/Surl.Conformance.UnitTests`,
   one test class per server named `CurlPortClient<Verb>SurlOver<Name>Tests`, with the locator in
   `Conformance/Surl.Conformance.UnitLibrary` (BL-406 builds it).
4. **Copied code.** Copying code from the port is allowed. A copied file is then held to Surl's rules: its
   names, the quality gates, and tests written from the specification, not copied along with the code.
5. **Plugin** is skipped, and why.
6. **The switch.** The variable's name and values; where it is read (in `Surl.Console`, through an injected
   environment reader, not a static); and what `surl` does with an extended scheme while the switch is off.
   Planning assumption: the scheme is refused exactly as an unknown scheme is refused today, with the same
   `SurlExitCode`.
7. **Default ports for extended schemes.** `Application/Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs` holds
   "upstream curl 8.21.0's default port per scheme" and must stay that. Extended schemes get their own table,
   each port taken from the protocol's specification or IANA registration, with each source cited.
8. **The `--aihelp` shape.** Whether extended servers get one `extended` topic plus one topic per server, and
   how the topics say these servers are not validated against upstream curl. Also decide whether
   `surl --version`'s protocol line changes.
9. **Requirement traceability.** Planning assumption, as in the port: one server-side requirement matrix per
   protocol in `Documentation/Conformance/Extended/<Name>.md`, with columns ID, level, requirement and covered
   by, holding only the requirements that bind a server. Each server's ADR task creates its matrix, and its
   integration task fills in the last "covered by" entries.
10. **Roadmap placement.** Either a new milestone after Phase 7, or a parallel track that waits only on the
    Phase 6 pieces it needs (HTTP/2, HTTP/3, QUIC). Name it.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It cites Stewart's approval of 2026-11-04 for the decisions that are
      his, answers decisions 1 to 10, lists the 20 servers with their schemes and folders, and is listed in
      that folder's `README.md`.
- [x] ADR-0003 carries an "Amended by" line pointing to the new ADR, and its text states the scope of the
      exception.
- [x] Root `CLAUDE.md`, section "Upstream curl and the Curl port", gains one paragraph that names the exception
      and points to the ADR, so it no longer says without qualification that the port is never a test client.
- [x] `.claude/rules/testing.md`'s protocol-test rule names the exception for `Surl.Extended.Protocol.*`.
- [x] `Documentation/Product/Requirements.md` has one functional requirement row stating that each extended
      server is judged by its specification and that the switch turns every extended server off.
- [x] `Documentation/Wiki/Glossary.md` defines "extended protocol", "extended server" and "original protocol".

## Notes

- 2026-11-04: Recorded as ADR-0083. Decisions taken: the specification is the oracle and the port's client
  runs only in `Integration` tests; the client is pinned by path and SHA-256 in `CurlPortBuilds.json` with its
  port commit and publish command, built locally from Stewart's own repository (no approval needed),
  Inconclusive when missing, not run in CI; tests `CurlPortClient<Verb>SurlOver<Name>Tests` in
  `Surl.Conformance.UnitTests`; `SURL_EXTENSIONS=0` turns them off (any other value leaves them on), read
  through an injected reader in `Surl.Console`, an extended scheme then refused `UnsupportedProtocol` (1); a
  separate `ExtendedSchemeDefaultPorts` table, ports from RFCs and IANA, conventions marked (Kafka, NATS,
  STOMP, `xmpps`); one `extended` `--aihelp` topic plus one per server; `--version` keeps `Protocols:` and
  adds an `Extended:` line; one server-side matrix per protocol; a parallel "Extended servers" track, not a
  phase after Phase 7.
- Added `Documentation/Product/Product-Overview.md` to `touches`: two of its non-goals (validate only
  against upstream curl; no protocols upstream curl does not request) contradicted the approved decision.
  No task in `Doing` names it.
- The ADR's server table carries Plugin as a 20th row marked skipped, so all 20 port protocols appear.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0083 records how Surl serves the Curl port's extended protocols: spec as oracle, port client in Integration tests only, CurlPortBuilds.json, SURL_EXTENSIONS=0, extended ports, --aihelp shape, parallel track
