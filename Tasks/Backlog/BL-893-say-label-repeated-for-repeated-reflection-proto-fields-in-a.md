---
id: BL-893
title: Say LABEL_REPEATED for repeated reflection.proto fields in ADR-0116 decision 9
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-444]
touches: [Documentation/Planning/Decisions/ADR-0116-how-the-grpc-server-answers-grpc-and-grpcs.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-893 — Say LABEL_REPEATED for repeated reflection.proto fields in ADR-0116 decision 9

## Goal

ADR-0116 decision 9's "Descriptors" bullet says what the code does: singular fields `LABEL_OPTIONAL`,
`repeated` fields `LABEL_REPEATED`.

## Context

- The bullet says every `FieldDescriptorProto` has `label` `LABEL_OPTIONAL`. That holds for
  `surl/test/echo.proto`, but `reflection.proto`'s `file_descriptor_proto`, `extension_number` and `service`
  are `repeated`, and descriptor.proto makes them `LABEL_REPEATED`; BL-444 builds them so
  (`ServerReflectionFiles`, `ProtoFieldDescriptor.IsRepeated`).
- Also worth stating there: `client_streaming`/`server_streaming` are written only when true, and
  `valid_host` is left out when the request has no `host`.

## Acceptance criteria

- [ ] ADR-0116 decision 9 names `LABEL_REPEATED` for `repeated` fields, and its other descriptor details
      match `ProtoFieldDescriptor`, `ProtoMethodDescriptor` and `ServerReflectionService`.

## Notes

## Log

- 2026-11-05: Created.
