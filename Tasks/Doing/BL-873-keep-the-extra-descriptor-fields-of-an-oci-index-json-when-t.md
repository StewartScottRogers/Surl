---
id: BL-873
title: Keep the extra descriptor fields of an OCI index.json when the registry rewrites it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-437]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-873 — Keep the extra descriptor fields of an OCI index.json when the registry rewrites it

## Goal

When the OCI registry rewrites a repository's `index.json` after a push or delete, every
descriptor it keeps keeps every field the file held, such as `platform`, `urls` or `data`. Today
only `mediaType`, `digest`, `size`, `artifactType` and `annotations` survive.

## Context

- ADR-0130 decision 6 records the loss; `OciIndexFile.Render` writes only the fields `OciDescriptor`
  holds, and `OciManifestJson` reads only those.
- Image "OCI Image Layout Specification" and "Descriptors": a layout written by another tool (such
  as `oras` or `skopeo copy oci:`) carries `platform` on the descriptors of a multi-platform index.
- One option: keep each descriptor's raw JSON object beside the parsed fields, and write it back
  with only the `org.opencontainers.image.ref.name` annotation changed.

## Acceptance criteria

- [ ] A named test in `Surl.Extended.Protocol.OciRegistry.UnitTests` pushes a manifest by tag
      into a repository whose `index.json` lists a descriptor with `platform` and `urls`, and
      `index.json` afterwards still holds that descriptor's `platform` and `urls` byte for byte.
- [ ] A named test shows that deleting a tag keeps the other fields of the descriptor left untagged.
- [ ] ADR-0130 decision 6 is amended to say the extra fields are kept.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
