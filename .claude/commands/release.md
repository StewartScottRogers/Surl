---
description: Propose the next Semantic Versioning number from the commits and create a draft release.
---
Delegate to the `github-operator` subagent. Arguments: **$ARGUMENTS**

1. Orient, and find the last tag for the project being released.
2. List the commits since that tag, grouped by Conventional Commit type.
3. Propose the next version: `feat` means minor, `fix` means patch, a `!` or
   `BREAKING CHANGE` means major. Show the arithmetic.
4. Propose the tag - `<scope>/vX.Y.Z`, or `surl/vX.Y.Z` for the shipped executable - and
   the notes, grouped under Features / Fixes / Other.
5. **Wait for Stewart to confirm the version.** Then create it as a **draft** only:
   `gh release create <tag> --draft --title ... --notes ...`

Never publish a release. Never tag without confirmation.

End with the Did / Result / Needs you block.
