---
id: BL-254
title: Create the Blowfish, Cast128 and Ripemd160 cryptography projects
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-252]
touches: [Surl.slnx, Surl.Cryptography.Blowfish.UnitLibrary, Surl.Cryptography.Blowfish.UnitTests, Surl.Cryptography.Cast128.UnitLibrary, Surl.Cryptography.Cast128.UnitTests, Surl.Cryptography.Ripemd160.UnitLibrary, Surl.Cryptography.Ripemd160.UnitTests, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-254 — Create the Blowfish, Cast128 and Ripemd160 cryptography projects

## Goal

The six empty projects ADR-0061 names exist, are listed in `Surl.slnx`, and
`ProtocolIsolationTests` knows their three horizontal rows, so the three primitive tasks
(BL-255, BL-256, BL-257) can run in parallel without touching a shared file.

## Context

- ADR-0061 (`Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md`,
  written by BL-252) amends ADR-0051 decision 2: surl offers `blowfish-cbc`, `cast128-cbc`,
  `hmac-ripemd160` and `hmac-ripemd160@openssh.com` only with `--allow-weak-ssh-algorithms`.
  The BCL on .NET 10 has no Blowfish, CAST-128 or RIPEMD-160, so each is hand-built in its own
  library, and ADR-0061 adds their rows to ADR-0002 decision 3's table:
  - `Surl.Cryptography.Blowfish.UnitLibrary` - references nothing. The Blowfish now internal to
    `Surl.Cryptography.BcryptPbkdf.UnitLibrary` moves here (BL-255), after which BcryptPbkdf
    references Blowfish, so BcryptPbkdf's row becomes `[Blowfish]`.
  - `Surl.Cryptography.Cast128.UnitLibrary` - references nothing (RFC 2144).
  - `Surl.Cryptography.Ripemd160.UnitLibrary` - references nothing (RIPEMD-160, HMAC-RIPEMD-160
    per RFC 2286).
- The precedent is BL-149, commit `01e08b1` ("create the Curve25519, Ed25519, ChaCha20 and
  Poly1305 projects"): csproj files and a `CLAUDE.md` only, no `.cs` files in the libraries or
  the test projects. Follow `.claude/skills/new-project/SKILL.md`, then delete the template's
  `Class1.cs` and `Test1.cs`/`MSTestSettings.cs`, and shape each csproj like
  `Surl.Cryptography.Rc4.UnitLibrary`/`.UnitTests` (library: `InternalsVisibleTo` its tests if
  wanted, nothing else; tests: `PackageReference Include="MSTest"` without `Version`, the
  `Using` of `Microsoft.VisualStudio.TestTools.UnitTesting`, a `ProjectReference` to its
  library). Do not add `IsAotCompatible` or `PublishAot`.
- `Surl.slnx` lists projects as one flat alphabetical run with each `.UnitTests` right after its
  library: Blowfish goes between `BcryptPbkdf.UnitTests` and `ChaCha20.UnitLibrary`, Cast128
  after Blowfish, Ripemd160 between `Rc4.UnitTests` and `Surl.Cryptography.UnitLibrary`.
- `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` holds ADR-0002's table as
  `HorizontalLibraries`; its summary names the ADRs whose rows it carries.
- Each `CLAUDE.md` is modelled on `Surl.Cryptography.Rc4.UnitLibrary/CLAUDE.md`: what the library
  will hold (as intent, since it is empty now), that it references nothing (ADR-0061), "bytes
  in, bytes out", the specification its vectors come from, and that code may be copied from the
  Curl port but no expected value comes from it (ADR-0003).

## Acceptance criteria

- [ ] `Surl.Cryptography.Blowfish.UnitLibrary`, `Surl.Cryptography.Blowfish.UnitTests`,
      `Surl.Cryptography.Cast128.UnitLibrary`, `Surl.Cryptography.Cast128.UnitTests`,
      `Surl.Cryptography.Ripemd160.UnitLibrary` and `Surl.Cryptography.Ripemd160.UnitTests`
      exist at the repository root, each with its csproj and no `.cs` file; no library has a
      `ProjectReference`, each test project references only its own library, and no csproj
      has a `Version` attribute, `IsAotCompatible` or `PublishAot`.
- [ ] `Surl.slnx` lists the six in the alphabetical positions given in Context, with no
      solution folder around them.
- [ ] Each of the three `.UnitLibrary` folders has a `CLAUDE.md` stating its purpose, that it
      references nothing (ADR-0061), and the specification its test vectors come from
      (Blowfish: Eric Young's vectors in Schneier's `vectors-2.txt`; Cast128: RFC 2144
      Appendix B; Ripemd160: the RIPEMD-160 authors' published vectors and RFC 2286 section 2).
- [ ] `ProtocolIsolationTests`: constants `Blowfish`, `Cast128` and `Ripemd160`;
      `HorizontalLibraries` gains `[Blowfish] = []`, `[Cast128] = []`, `[Ripemd160] = []`, and
      `[BcryptPbkdf]` becomes `[Blowfish]`; its summary names ADR-0061 among the ADRs whose rows
      it holds; `ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden` gains a `DataRow`
      for each of the three; `ForbiddenHorizontalReferences_ReferenceInItsRow_IsNotForbidden`
      gains `(BcryptPbkdf, Blowfish)`; `ForbiddenHorizontalReferences_ReferenceOutsideItsRow_IsForbidden`
      gains `(Blowfish, BcryptPbkdf)` and `(Cast128, Blowfish)`.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` passes, `ProtocolIsolationTests` included.
- [ ] No package is added; nothing outside the `touches` list changes.

## Notes

- Quality gates (100% line and branch coverage, cyclomatic complexity at most 10, CRAP at most
  30) apply to every `.UnitLibrary`; they are met trivially here because the libraries hold no
  code yet. Any test added must be platform-neutral (Windows, Linux, macOS).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
