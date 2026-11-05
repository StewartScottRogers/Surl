# Download

No release of Surl has been published yet. Surl is in Phase 1: `surl` builds and
publishes, and serves the files of a directory over plain HTTP; no other scheme yet.

## When there is a release

Pushing a `v*` tag runs `.github/workflows/release.yml`, which publishes the native
ahead-of-time `surl` and `surl-mcp` binaries for six platforms and attaches them, with a
`SHA256SUMS` file, to a GitHub release at https://github.com/StewartScottRogers/Surl/releases:

| Platform | `surl` package | `surl-mcp` package |
| --- | --- | --- |
| Windows x64 | `surl-win-x64.zip` | `surl-mcp-win-x64.zip` |
| Windows Arm64 | `surl-win-arm64.zip` | `surl-mcp-win-arm64.zip` |
| Linux x64 (glibc 2.35 or newer) | `surl-linux-x64.tar.gz` | `surl-mcp-linux-x64.tar.gz` |
| Linux Arm64 (glibc 2.35 or newer) | `surl-linux-arm64.tar.gz` | `surl-mcp-linux-arm64.tar.gz` |
| macOS x64 | `surl-osx-x64.tar.gz` | `surl-mcp-osx-x64.tar.gz` |
| macOS Arm64 | `surl-osx-arm64.tar.gz` | `surl-mcp-osx-arm64.tar.gz` |

`surl-mcp` is Surl's MCP server: an MCP client - an AI agent's host - starts it as a stdio
server, and its tools let the agent start, list and stop Surl protocol servers and see what
their clients sent. Each package holds the one executable, `LICENSE.txt` and `README.md`.

The installers in this repository fetch the package for the machine they run on, check it
against `SHA256SUMS`, and put `surl` in a directory of its own (they do not fetch `surl-mcp`):

```sh
curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.sh | sh    # Linux, macOS
```

```powershell
irm https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.ps1 | iex          # Windows
```

Neither works until the repository exists on GitHub and has a release. Tags and releases
are Stewart's call (`CLAUDE.md`).

## Build it yourself

```
dotnet publish Application/Surl.Console -c Release -r <rid> -o publish
```

`<rid>` is one of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` or
`osx-arm64`, and native ahead-of-time compilation cannot cross operating systems, so build
on the one you target. On Windows it needs the MSVC linker from Visual Studio's "Desktop
development with C++" workload and
`C:\Program Files (x86)\Microsoft Visual Studio\Installer` on `PATH` for vswhere; on
Linux, `clang` and `zlib1g-dev`.
