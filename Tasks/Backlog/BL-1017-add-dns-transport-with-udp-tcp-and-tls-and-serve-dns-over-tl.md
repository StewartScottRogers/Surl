---
id: BL-1017
title: Add --dns-transport with udp, tcp and tls and serve DNS over TLS with ALPN dot
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1032]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1017 — Add --dns-transport with udp, tcp and tls and serve DNS over TLS with ALPN dot

## Goal

`surl --dns-transport udp,tcp,tls --self-signed dns://127.0.0.1:5300` serves DNS over UDP, TCP and TLS (DoT,
RFC 7858) on that port's protocols as ADR-0092 decision 5 says, with the DoT handshake agreeing ALPN `dot`.

## Context

- Second of the four tasks BL-485 was split into (see BL-1032). This task adds the option with the values `udp`,
  `tcp` and `tls`; BL-1018 adds `https` and BL-1019 adds `quic` to its allowed values.
- Decision: ADR-0092 decision 5 - `--dns-transport <list>`, default `udp,tcp`, categories `dns` and `tls`; ports
  (a listen URL with a port puts every transport on it, without one each on its default: 53, 53, 853; port 0
  gives each its own ephemeral port and status line); `tcp` with `tls` on one explicit non-zero port refused
  `FailedInit` (2) with `surl: --dns-transport <a> and <b> cannot share port <n>`; a value outside the list, an
  empty list or a repeated value refused `FailedInit` as ADR-0007 section 5 refuses any bad value; `tls` needs
  `--cert` or `--self-signed`, else `CertificateProblem` (58).
- DoT ALPN: the handshake agrees `dot` when offered, accepts a client offering no ALPN, and ends with
  `no_application_protocol` (120) a client whose list lacks `dot` (RFC 7301 section 3.2). The per-listener ALPN
  list is `TlsApplicationProtocols` in `Foundation/Surl.Networking.UnitLibrary`; keep every other scheme's offer,
  which the existing Networking tests pin.
- After the handshake a DoT connection is `DnsTcpProtocolServer`'s connection exactly (RFC 7858 section 3.3).
- `--aihelp`: the option's `OptionArgumentType` and allowed values, and the `dns` topic's paragraph names DoT.
- Stand-in: no store; each transport's exchanges are the engine's events with their own listen URL.

## Acceptance criteria

- [ ] Named `Surl.Cli.UnitTests` tests read `--dns-transport udp,tcp,tls` and refuse `bogus`, an empty list and
      `tcp,tcp`, each with `FailedInit` (2) and the option-error text.
- [ ] Named `Surl.Composition.UnitTests` tests show `tls` composing a TLS-from-the-first-byte listener on 853 or
      the given port, `tcp,tls` on one explicit port refused with the decision's text, and `tls` without a
      certificate refused `CertificateProblem` (58).
- [ ] A named `Surl.Networking.UnitTests` test shows a `dns` TLS listener agreeing `dot`, accepting a client with
      no ALPN, and refusing one offering only `h2` with alert 120.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass; `dotnet build -warnaserror` is clean, the
      fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
