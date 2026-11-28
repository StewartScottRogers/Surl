---
id: BL-2146
title: Fail a fast test when the solution gains a second NuGet package or package reference
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2147]
touches: [Foundation/Surl.Protocol.Abstractions.UnitTests, Documentation/Product/Requirements.md]
requirement: NFR-007
created: 2026-11-28
completed:
---
# BL-2146 — Fail a fast test when the solution gains a second NuGet package or package reference

## Goal

A fast test fails when the solution gains a package other than MSTest, or a production project gains a package reference, and NFR-007 says it is enforced by that test.

## Context

Found while working BL-2120 (2026-11-28): central package management fails a reference that has no version entry, but nothing limits the entries or the references. Add `PackageDependencyTests.cs` to `Foundation/Surl.Protocol.Abstractions.UnitTests`, which already reads project files from the repository in `ProtocolIsolationTests.cs` (its `RepositoryRoot` finds `Surl.slnx`). It reads the root `Directory.Packages.props`, every project `Surl.slnx` lists, and the MSBuild files that apply to them (`Directory.Build.props`, `CurlPortLibcurlReference.targets`, and any `Directory.Build.*` or `Directory.Packages.props` under the category folders). The projects and package files under `.github/coverage` and `.github/gource` are outside the solution and are not checked. Platform-neutral: normalize path separators; skip `bin` and `obj`. No package is added. The MSTest version number is not pinned by the test. Stand-in: nothing to add; a build rule.

## Acceptance criteria

- [ ] A test fails unless the root `Directory.Packages.props` declares exactly one `PackageVersion`, `MSTest`, and no `GlobalPackageReference`, and no second `Directory.Packages.props` overrides it inside the solution's folders.
- [ ] A test fails when a project in `Surl.slnx` whose name does not end `.UnitTests` has a `PackageReference`, when a test project references anything but `MSTest`, when a reference carries `Version` or `VersionOverride`, or when a shared MSBuild file adds a `PackageReference`.
- [ ] Tests feed the detector in-memory XML with a second package entry and with a second reference, and show each is reported; one more shows the set of projects read is not empty.
- [ ] NFR-007's status says `Enforced by` the `ci.yml` fast-test job and names `PackageDependencyTests`, with the file's `Last updated` date that of the change.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitTests -warnaserror` is clean, the fast tests pass with no `TestCategory=Integration` needed, and `Directory.Packages.props` is unchanged.

## Notes

## Log

- 2026-11-28: Created.
