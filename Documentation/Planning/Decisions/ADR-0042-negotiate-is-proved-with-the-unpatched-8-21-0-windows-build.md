# ADR-0042 — Negotiate is proved with the unpatched 8.21.0 Windows build

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-134. No build was downloaded: the build this ADR uses was already pinned by ADR-0030.

## Context

BL-134 has to prove that upstream curl's `--negotiate` logs in to `surl --auth negotiate`. The
Windows reference build (Git for Windows' curl 8.21.0, ADR-0003 decision 4) sends no token on a
machine that is not in a domain: SSPI's `InitializeSecurityContext` fails with
`SEC_E_NO_CREDENTIALS` (ADR-0040, "Measured").

BL-134 found why. Git for Windows' `libcurl-4.dll` hands `AcquireCredentialsHandle` for
`Negotiate` a `SEC_WINNT_AUTH_IDENTITY_EXA` whose `PackageList` is `!ntlm` - upstream curl's
commit `a8881e5e1d` ("spnego: block NTLM fallback in SPNEGO negotiation", 2026-07-27), which is
not in the tag `curl-8_21_0` and ships in 8.22.0. With NTLM excluded and no Kerberos realm to
reach, Negotiate has no mechanism left. curl.se's 8.22.0 build (ADR-0017) fails the same way.
stunnel/static-curl's 8.21.0 Windows build (ADR-0030), built from the tag unpatched, still lets
Negotiate fall back to NTLM and sends a bare NTLM `NEGOTIATE_MESSAGE` after `Negotiate`.

So a pinned build that logs in with Negotiate over NTLM exists, but it is a supplementary build
pinned for SMB only, and the reference build cannot log in with Negotiate at all without
Kerberos, which Surl does not serve yet (ADR-0032 section 11).

## Decision

1. **The unpatched 8.21.0 build is also used for HTTP Negotiate.** stunnel/static-curl's
   `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe` (SHA-256 `589C8E4D…2648`)
   measures and proves Negotiate carrying NTLM, besides SMB (ADR-0030). It is the reference
   release built from the tag unpatched, so its Negotiate is the reference release's own:
   it is the more faithful of the two 8.21.0 builds for this case, not a second opinion on the
   reference build. `UpstreamCurlBuilds.json` names the use in its `origin`.
2. **The conformance test picks the build by its SHA-256.** `PinnedUpstreamCurl.RunSupplementaryBuildAsync`
   locates the supplementary build pinned with a given hash. The locator's protocol lookup
   (ADR-0030 decision 5) cannot choose it: `http` is in the reference build's protocols, and
   the pins carry no features.
3. **The reference build's refusal stays pinned too.** A Windows conformance test proves the
   reference build sends no token against `surl` and exits 22 with `SEC_E_NO_CREDENTIALS`
   under `-f`, so the day a reference build with the `!ntlm` change logs in (with Kerberos,
   or on another machine) the test says so.

## Alternatives considered

- **Treat the Git for Windows build's behaviour as the answer and leave Negotiate unproved
  until Kerberos lands.** Rejected: the tag 8.21.0 falls back to NTLM, and the Curl port
  targets that release; proving it against the unpatched build compares like with like.
- **Pin another build.** Rejected: a download needs Stewart's approval, and the build needed
  is already pinned.
- **Add features to the pins and locate by `SPNEGO`.** Rejected for now: every Windows build
  lists `SPNEGO`, so the feature would not choose the build; what differs is a source change
  the version line does not show.

## Consequences

- `curl --negotiate -u tester:secret` from the unpatched build logs in to
  `surl --auth negotiate --user-file <f>`; a wrong password is refused with `401`, exit 22
  under `-f`. Both run on Windows only, the one platform whose pinned builds list `SPNEGO`.
- Upstream curl from 8.22.0 on, and Git for Windows' 8.21.0, never carry NTLM inside
  Negotiate; for them Negotiate works only once Surl serves Kerberos (ADR-0032 section 11).
- A machine without the static-curl build has no Negotiate proof; the test is Inconclusive,
  as for any absent pinned build.
