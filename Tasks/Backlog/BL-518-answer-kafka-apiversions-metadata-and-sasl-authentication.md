---
id: BL-518
title: Answer Kafka ApiVersions, Metadata and SASL authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-517]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-518 — Answer Kafka ApiVersions, Metadata and SASL authentication

## Goal

The Kafka broker answers ApiVersions with its served ranges. It answers Metadata with itself as the only broker
and controller and with the topics and partitions it holds. It runs SaslHandshake and SaslAuthenticate with
`PLAIN` against the accounts, and refuses other requests before login as BL-516's ADR says.

## Context

- Decision: BL-516's ADR, decisions 1, 2 and 5.
- Kafka protocol guide: ApiVersions (v0 to v3; the v0 fallback rule for an unsupported ApiVersions version),
  Metadata v4 (brokers, `cluster_id`, `controller_id`, topics with `is_internal`, partitions with leader, replicas
  and isr, and `allow_auto_topic_creation`), SaslHandshake v1 (`UNSUPPORTED_SASL_MECHANISM` 33 with the enabled
  mechanisms), and SaslAuthenticate v0 (`SASL_AUTHENTICATION_FAILED` 58). RFC 4616 for the `PLAIN` message.
- Logins go through the SASL contracts in `Surl.Protocol.Abstractions`.

## Acceptance criteria

- [ ] Named tests show:
      - ApiVersions listing exactly the ADR's ranges;
      - an ApiVersions version above the range answered with v0 and `UNSUPPORTED_VERSION`;
      - Metadata for all topics and for one missing topic (created or `UNKNOWN_TOPIC_OR_PARTITION`, as the ADR says);
      - SaslHandshake for `PLAIN` accepted and for `GSSAPI` refused with 33 and the mechanism list;
      - a good `PLAIN` accepted;
      - a bad one gets 58;
      - Metadata before login handled as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
