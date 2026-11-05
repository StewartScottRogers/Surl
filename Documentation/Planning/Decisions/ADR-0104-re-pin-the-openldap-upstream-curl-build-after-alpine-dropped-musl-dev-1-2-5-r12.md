# ADR-0104 — Re-pin the OpenLDAP upstream curl build after Alpine dropped `musl-dev-1.2.5-r12`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-690. Within Stewart's BL-282 approval as ADR-0076 and ADR-0078 apply it: the same tag
  `curl-8_21_0`, built from the same pinned sources by the same script. No curl binary was
  downloaded.
- **Amends:** [ADR-0076](ADR-0076-an-openldap-upstream-curl-8-21-0-build-built-reproducibly-in-ci-for-ldap-and-ldaps.md)
  decision 1's package list, and [ADR-0078](ADR-0078-ldap-sasl-gssapi-measured-against-an-openldap-build-with-mit-kerberos.md)
  decision 3's SHA-256 (`62061C58...7E55`).

## Context

CI run https://github.com/StewartScottRogers/Surl/actions/runs/41980108989 (commit `b1a280aa`)
failed `Obtain the pinned upstream curl` on ubuntu-latest five times with
`ERROR: unable to select packages: musl-1.2.5-r12: breaks: musl-dev-1.2.5-r13`. The Actions cache
no longer held the build, so CI ran `Build-OpenLdapUpstreamCurl.ps1`, and it could not install its
pinned packages.

Measured on 2026-11-04 and 2026-11-05 (BL-690):

- The pinned image `alpine@sha256:5291449c...8fa8` is Alpine 3.22.6 with `musl-1.2.5-r12`
  installed. Alpine's v3.22 repository now offers only `musl-dev-1.2.5-r13`;
  `musl-dev-1.2.5-r12` is gone (404 on `dl-cdn.alpinelinux.org`). The other five pinned packages
  (`binutils=2.44-r3 gcc=14.2.0-r6 linux-headers=6.14.2-r0 make=4.4.1-r3 perl=5.40.4-r0`) are
  still there at their pinned versions.
- The old pin cannot be reproduced any more. v3.21's `musl-dev-1.2.5-r12`, still on `dl-cdn`,
  builds `898C2743A890A9741DA89FC69E327ADDE59D63745E981BB2C20CB4A27C91364A`, not `62061C58...7E55`.

ADR-0076 decision 3 says what happens next: a build that no longer reproduces fails CI and is never
re-pinned there; the fix is a new ADR that re-pins it. This is that ADR.

## Decision

1. `Build-OpenLdapUpstreamCurl.ps1` installs `musl=1.2.5-r13 musl-dev=1.2.5-r13` in place of
   `musl-dev=1.2.5-r12`; `apk` upgrades the image's installed musl to the matching revision. The
   image digest, the other five packages, the four source tarballs and their SHA-256s, the
   configure flags and `SOURCE_DATE_EPOCH` are unchanged. Pinning `musl` as well as `musl-dev`
   keeps the rule that every package deciding the binary's bytes is pinned by version: when Alpine
   drops r13 too, `apk` refuses and the build fails rather than changing.
2. The supplementary `linux-x64` entry in `UpstreamCurlBuilds.json` is re-pinned to
   `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480` (`pinned` 2026-11-05), at the
   same path. Built three times from clean on Docker Desktop (linux/amd64): twice on 2026-11-04,
   once on 2026-11-05, the same SHA-256 every time.
3. The rebuilt binary's `curl -V` reports exactly the old pin's `version`, `protocols` and
   `features`, so those fields are unchanged. musl's r12 to r13 is an Alpine packaging revision of
   the same upstream musl 1.2.5; curl, OpenSSL, OpenLDAP and MIT Kerberos are byte-for-byte the
   same sources. Nothing measured through the build in ADR-0076 or ADR-0078 is re-run for this
   re-pin: the LDAP bytes curl sends come from curl's and libldap's code, which did not change.
   If a later measurement through the new build disagrees with one recorded under the old pin,
   that is a defect to file, not a reason to restore the old pin.
4. Rejected: pinning a different Alpine image (a newer 3.22 or 3.23 digest). It would move more
   than musl - gcc, binutils and the rest would need new pins too - for the same outcome, a new
   SHA-256.

## Consequences

- The first CI run after this change misses the cache (its key holds the new SHA-256), builds,
  verifies the new pin and caches it.
- The next time Alpine drops a pinned revision the same failure returns, and the fix is again a
  new ADR. Pinning an image digest cannot stop that: the repository, not the image, serves the
  packages.
