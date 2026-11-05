---
id: BL-384
title: Answer HTTP/3 from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-379, BL-381, BL-383]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-384 — Answer HTTP/3 from the surl executable

## Goal

The `surl` executable answers HTTP/3 as BL-372's ADR decision 6 decides: it binds the UDP listener for
`https` listen URLs (or for the option the ADR names), serves HTTP/3 through BL-383's server, writes
any `Alt-Svc` field the ADR calls for, and `surl --aihelp` describes it.

## Context

- Decision: BL-372's ADR, decision 6 (default on or off, any option, the port, and `Alt-Svc`).
- `Application/Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` registers each server and its
  listeners. `Surl.Cli.UnitLibrary` holds the option table, `AiHelpProse` and `AiHelpExamples`. Root
  `CLAUDE.md`, "`surl --aihelp`": a new option needs its `OptionArgumentType` and a place on its
  categories' pages, and the behaviour that an `AiHelpProse` paragraph or `AiHelpExamples` entry
  describes changes in the same diff. The tests that enforce this are listed there.
- The status line (`Listening on ...`, read by `SurlOnLoopback`) must say what the UDP listener bound,
  as the ADR decides, so BL-385 can find the port.
- `Alt-Svc` (RFC 7838), if the ADR has responses carry it, is written by `Surl.Protocol.Http`.

## Acceptance criteria

- [x] `surl https://127.0.0.1:0/` (with `--self-signed`, or with the options the ADR names) binds TCP
      and UDP as the ADR says, and its status lines name both. A `Surl.Console.UnitTests` test pins
      the text.
- [x] Any new option has its help, `--aihelp` facts and topic text, and `AiHelpFactsTests`,
      `AiHelpTextTests` and `CommandLineRunnerAiHelpTests` pass.
- [x] If the ADR decides on `Alt-Svc`, an `https` response over HTTP/1.1 and over HTTP/2 carries the
      field the ADR specifies, pinned in a `Surl.Protocol.Http.UnitTests` test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, the touched projects keep 100%
      line and branch coverage, and `dotnet publish Application/Surl.Console -c Release -r win-x64` succeeds with
      no trim or AOT warning.

## Notes

What was built, by ADR-0082 decision 6:

- `--http3` (flag, negatable, `http` help category; `SurlCommandLine.Http3`). Status line unchanged
  (one `Listening on https://<host>:<port>/`, the port TCP and UDP share, so BL-385's
  `SurlOnLoopback` finds it as before); with `-v`, `* Serving HTTP/3 over QUIC on UDP port <port>`.
  Pinned in `CommandLineRunnerTlsTests.RunAsync_Http3WithSelfSigned_*` and `RunAsync_VerboseHttp3_*`.
- Contract: `IListenerFactory.StartSharedPortListenersAsync` (default method: TCP, then UDP on its
  port, TCP released if UDP fails) returning `SharedPortListeners`; `SocketListenerFactory` uses
  BL-381's `SharedPortListenerPair` (port-0 retry). `ServingEngine` accepts one connection server
  and one datagram server for the same scheme and binds both through it.
- `Http3ProtocolServer` (`https`, datagram) + `Http3ApplicationProtocol`; `Alt-Svc` via
  `AltSvcAdvertisingHandler`; composition adds the HTTP/3 server after server TLS from
  `ServerTlsComposition.QuicCertificate`.
- Live smoke with the published native `surl.exe -v --self-signed --http3 https://127.0.0.1:0/`:
  bound TCP and UDP on one port and wrote both lines. Upstream curl against it is BL-385's job.

Decisions (Decided by Claude under Stewart's delegation; to be written into ADR-0082 as
Amendment 2 by BL-711, because BL-417 holds `Documentation/Planning/Decisions` and rule 3 forbids
overlapping it):

1. `--http3` with `--cacert` is refused, exit 2 (`option --http3: cannot be used with --cacert`):
   `Surl.Quic` asks for no client certificate, so serving HTTP/3 would skip the check the TCP side
   enforces (secure by default). BL-713 lifts it.
2. Refusal order, after the no-URL refusal: no `https` listen URL (`needs an https listen URL`),
   `--tls-max` below 1.3 (`needs --tls-max 1.3: QUIC is TLS 1.3 only`), `--cacert`. All `FailedInit`
   (2) with the try-help line, as option refusals are. `--tls-max 1.0/1.1` with the default
   minimum hits the existing `--tls-max` refusal first.
3. `Alt-Svc: h3=":<port>"; ma=86400` goes on every answer the HTTP handler gives on a TLS
   connection (HTTP/1.x and HTTP/2), the port being the connection's local port; framing refusals
   written below the handler (400, 431, the 503 at a connection limit) do not carry it.
4. HTTP/3's authentication session is started with `TlsSession(Tls13, <agreed suite>, "h3", null,
   null)`, so Basic and Bearer are judged as over TLS; `QuicApplicationConnection.CipherSuite` was
   added to `Surl.Quic` for it.
5. Cancelling the exchange before the handshake completes stops the QUIC flow at once
   (`OperationCanceledException`, which the engine treats as a graceful end); after it, the
   HTTP/3 server answers with GOAWAY and `H3_NO_ERROR` (Amendment 1) and the flow ends after.
6. QUIC's `max_idle_timeout` is `--idle-timeout` (0 = none) and the handshake timeout is the head
   timeout (none when unlimited), as decision 7's table says.
7. No `IDatagramRefusalWriter` yet: a flow past a connection limit gets no bytes. BL-712 adds the
   `CONNECTION_REFUSED` Initial.

Touches added (rule 3; no task in Doing named any of them): `Foundation/Surl.Protocol.Abstractions.*`
(the shared-port contract), `Foundation/Surl.Core.*` (the engine binds the pair),
`Foundation/Surl.Networking.*` (`SocketListenerFactory`), `Application/Surl.Composition.*` (where
servers and TLS are composed; `ComposeProtocolServers` moved there from `Surl.Console`), and
`Protocols/Http/Surl.Quic.*` (the agreed cipher suite).

Quality: `Measure-CodeQuality.ps1` shows 100% line and branch coverage for all eight touched
libraries. It flags complexity on seven members that this task did not touch
(`ConfigurationItemReader.ReadAttribute`, six in `Surl.Quic`); the build's CA1502 gate passes.
`dotnet publish Application/Surl.Console -c Release -r win-x64` built with no warning.

Follow-ups filed: BL-711 (ADR amendment), BL-712 (refusal writer), BL-713 (client certificates over QUIC).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl --http3 serves HTTP/3 over QUIC on every https listen URL's port, with Alt-Svc on HTTP/1.x and HTTP/2
